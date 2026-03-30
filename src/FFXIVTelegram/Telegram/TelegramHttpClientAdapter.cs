namespace FFXIVTelegram.Telegram;

using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using FFXIVTelegram.Configuration;

internal sealed class TelegramHttpClientAdapter : ITelegramClientAdapter, IDisposable
{
    private static readonly Uri TelegramBaseUri = new("https://api.telegram.org/");
    private static readonly TimeSpan DefaultHttpTimeout = TimeSpan.FromSeconds(35);

    private readonly FfxivTelegramConfiguration configuration;
    private readonly HttpClient httpClient;

    public TelegramHttpClientAdapter(FfxivTelegramConfiguration configuration)
        : this(configuration, new HttpClient { BaseAddress = TelegramBaseUri, Timeout = DefaultHttpTimeout })
    {
    }

    internal TelegramHttpClientAdapter(FfxivTelegramConfiguration configuration, HttpClient httpClient)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string token;
        lock (this.configuration)
        {
            if (string.IsNullOrWhiteSpace(this.configuration.TelegramBotToken))
            {
                return Array.Empty<TelegramUpdate>();
            }

            token = Uri.EscapeDataString(this.configuration.TelegramBotToken);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"bot{token}/getUpdates?offset={offset.ToString(CultureInfo.InvariantCulture)}&timeout=30");
        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"telegram http {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("ok", out var okElement) || !okElement.GetBoolean())
        {
            var errorMessage = document.RootElement.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString() ?? "telegram request failed"
                : "telegram request failed";
            throw new InvalidOperationException(errorMessage);
        }

        var updates = new List<TelegramUpdate>();
        foreach (var updateElement in document.RootElement.GetProperty("result").EnumerateArray())
        {
            if (!updateElement.TryGetProperty("update_id", out var updateIdElement))
            {
                continue;
            }

            if (!updateElement.TryGetProperty("message", out var messageElement))
            {
                continue;
            }

            if (!messageElement.TryGetProperty("message_id", out var messageIdElement))
            {
                continue;
            }

            if (!messageElement.TryGetProperty("chat", out var chatElement))
            {
                continue;
            }

            if (!chatElement.TryGetProperty("id", out var chatIdElement))
            {
                continue;
            }

            var replyToMessageId = messageElement.TryGetProperty("reply_to_message", out var replyMessageElement)
                && replyMessageElement.TryGetProperty("message_id", out var replyToMessageIdElement)
                    ? (long?)replyToMessageIdElement.GetInt64()
                    : null;

            var text = messageElement.TryGetProperty("text", out var textElement)
                ? textElement.GetString()
                : null;
            var fromUserId = messageElement.TryGetProperty("from", out var fromElement)
                && fromElement.TryGetProperty("id", out var fromIdElement)
                    ? (long?)fromIdElement.GetInt64()
                    : null;
            var isFromBot = messageElement.TryGetProperty("from", out var botElement)
                && botElement.TryGetProperty("is_bot", out var isBotElement)
                    && isBotElement.GetBoolean();
            var chatType = chatElement.TryGetProperty("type", out var chatTypeElement)
                ? chatTypeElement.GetString()
                : null;

            updates.Add(new TelegramUpdate(
                updateIdElement.GetInt64(),
                messageIdElement.GetInt64(),
                replyToMessageId,
                chatIdElement.GetInt64(),
                string.Equals(chatType, "private", StringComparison.Ordinal),
                text,
                fromUserId,
                isFromBot));
        }

        return updates;
    }

    public async Task<TelegramSendResult> SendTextAsync(long chatId, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        string token;
        lock (this.configuration)
        {
            if (string.IsNullOrWhiteSpace(this.configuration.TelegramBotToken))
            {
                return TelegramSendResult.Failure("bot token not configured");
            }

            token = Uri.EscapeDataString(this.configuration.TelegramBotToken);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"bot{token}/sendMessage")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["chat_id"] = chatId.ToString(CultureInfo.InvariantCulture),
                ["text"] = text,
            }),
        };

        using var response = await this.httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return TelegramSendResult.Failure($"telegram http {(int)response.StatusCode}: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        if (!document.RootElement.TryGetProperty("ok", out var okElement) || !okElement.GetBoolean())
        {
            var errorMessage = document.RootElement.TryGetProperty("description", out var descriptionElement)
                ? descriptionElement.GetString() ?? "telegram request failed"
                : "telegram request failed";
            return TelegramSendResult.Failure(errorMessage);
        }

        var result = document.RootElement.GetProperty("result");
        var messageId = result.GetProperty("message_id").GetInt64();
        return TelegramSendResult.Ok(messageId);
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
    }
}
