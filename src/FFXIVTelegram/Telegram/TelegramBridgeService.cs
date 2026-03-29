namespace FFXIVTelegram.Telegram;

using FFXIVTelegram.Configuration;

public sealed class TelegramBridgeService
{
    private readonly ITelegramClientAdapter clientAdapter;
    private readonly ConfigurationStore configurationStore;
    private bool hasTransportError;
    private long nextUpdateOffset;

    public TelegramBridgeService(
        FfxivTelegramConfiguration configuration,
        ITelegramClientAdapter clientAdapter,
        ConfigurationStore configurationStore)
    {
        this.Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.clientAdapter = clientAdapter ?? throw new ArgumentNullException(nameof(clientAdapter));
        this.configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        this.nextUpdateOffset = 0;
    }

    public FfxivTelegramConfiguration Configuration { get; }

    public TelegramConnectionState ConnectionState => this.hasTransportError
        ? TelegramConnectionState.Error
        : this.ResolveConnectionState(this.Configuration);

    public async Task<TelegramPollResult> PollOnceAsync(CancellationToken cancellationToken)
    {
        if (!this.Configuration.HasTelegramBotToken)
        {
            this.hasTransportError = false;
            return TelegramPollResult.Empty(this.nextUpdateOffset);
        }

        try
        {
            var updates = await this.clientAdapter.GetUpdatesAsync(this.nextUpdateOffset, cancellationToken).ConfigureAwait(false);
            var acceptedMessages = new List<TelegramInboundMessage>();

            foreach (var update in updates)
            {
                var inbound = await this.HandleIncomingUpdateAsync(update, cancellationToken).ConfigureAwait(false);
                this.nextUpdateOffset = Math.Max(this.nextUpdateOffset, update.UpdateId + 1);

                if (inbound is not null)
                {
                    acceptedMessages.Add(inbound);
                }
            }

            this.hasTransportError = false;
            return new TelegramPollResult(this.nextUpdateOffset, acceptedMessages);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TelegramPollResult.Empty(this.nextUpdateOffset);
        }
        catch (OperationCanceledException)
        {
            this.hasTransportError = true;
            throw;
        }
        catch
        {
            this.hasTransportError = true;
            throw;
        }
    }

    public Task<TelegramInboundResult> HandleIncomingTextAsync(
        long chatId,
        bool isPrivateChat,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!this.Configuration.HasTelegramBotToken)
        {
            this.hasTransportError = false;
            return Task.FromResult(TelegramInboundResult.IgnoredUnsupportedChatType);
        }

        if (!isPrivateChat)
        {
            this.hasTransportError = false;
            return Task.FromResult(TelegramInboundResult.IgnoredUnsupportedChatType);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            this.hasTransportError = false;
            return Task.FromResult(TelegramInboundResult.IgnoredEmptyText);
        }

        if (this.Configuration.AuthorizedChatId is null)
        {
            if (text == "/start")
            {
                var previousAuthorizedChatId = this.Configuration.AuthorizedChatId;
                this.Configuration.AuthorizedChatId = chatId;

                try
                {
                    this.configurationStore.Save(this.Configuration);
                    this.hasTransportError = false;
                    return Task.FromResult(TelegramInboundResult.Authorized);
                }
                catch
                {
                    this.Configuration.AuthorizedChatId = previousAuthorizedChatId;
                    throw;
                }
            }

            this.hasTransportError = false;
            return Task.FromResult(TelegramInboundResult.IgnoredUnauthorizedChat);
        }

        if (this.Configuration.AuthorizedChatId.Value != chatId)
        {
            this.hasTransportError = false;
            return Task.FromResult(TelegramInboundResult.IgnoredUnauthorizedChat);
        }

        this.hasTransportError = false;
        return Task.FromResult(TelegramInboundResult.Accepted);
    }

    public async Task<TelegramSendResult> SendToAuthorizedChatAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!this.Configuration.HasTelegramBotToken)
        {
            this.hasTransportError = false;
            return TelegramSendResult.Failure("bot token not configured");
        }

        var chatId = this.Configuration.AuthorizedChatId;
        if (chatId is null)
        {
            this.hasTransportError = false;
            return TelegramSendResult.Failure("no authorized chat yet");
        }

        var result = await this.clientAdapter.SendTextAsync(chatId.Value, text, cancellationToken).ConfigureAwait(false);
        this.hasTransportError = false;
        return result;
    }

    private async Task<TelegramInboundMessage?> HandleIncomingUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        var inboundResult = await this.HandleIncomingTextAsync(update.ChatId, update.IsPrivateChat, update.Text ?? string.Empty, cancellationToken).ConfigureAwait(false);
        if (inboundResult != TelegramInboundResult.Accepted)
        {
            return null;
        }

        return new TelegramInboundMessage(
            update.UpdateId,
            update.MessageId,
            update.ReplyToMessageId,
            update.ChatId,
            update.IsPrivateChat,
            update.Text ?? string.Empty);
    }

    private TelegramConnectionState ResolveConnectionState(FfxivTelegramConfiguration configuration)
    {
        if (!configuration.HasTelegramBotToken)
        {
            return TelegramConnectionState.NotConfigured;
        }

        return configuration.HasAuthorizedChat
            ? TelegramConnectionState.Connected
            : TelegramConnectionState.WaitingForStart;
    }
}
