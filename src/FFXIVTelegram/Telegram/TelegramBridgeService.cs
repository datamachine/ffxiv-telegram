namespace FFXIVTelegram.Telegram;

using FFXIVTelegram.Configuration;

public sealed class TelegramBridgeService
{
    private readonly ITelegramClientAdapter clientAdapter;
    private readonly ConfigurationStore configurationStore;
    private long nextUpdateOffset;

    public TelegramBridgeService(
        FfxivTelegramConfiguration configuration,
        ITelegramClientAdapter clientAdapter,
        ConfigurationStore configurationStore)
    {
        this.Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        this.clientAdapter = clientAdapter ?? throw new ArgumentNullException(nameof(clientAdapter));
        this.configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        this.ConnectionState = this.ResolveConnectionState(configuration);
        this.nextUpdateOffset = 0;
    }

    public FfxivTelegramConfiguration Configuration { get; }

    public TelegramConnectionState ConnectionState { get; private set; }

    public async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        if (!this.Configuration.HasTelegramBotToken)
        {
            this.ConnectionState = TelegramConnectionState.NotConfigured;
            return;
        }

        try
        {
            var updates = await this.clientAdapter.GetUpdatesAsync(this.nextUpdateOffset, cancellationToken).ConfigureAwait(false);
            foreach (var update in updates)
            {
                this.nextUpdateOffset = Math.Max(this.nextUpdateOffset, update.UpdateId + 1);
                if (update.Text is not null)
                {
                    await this.HandleIncomingTextAsync(update.ChatId, update.IsPrivateChat, update.Text, cancellationToken).ConfigureAwait(false);
                }
            }

            this.ConnectionState = this.ResolveConnectionState(this.Configuration);
        }
        catch
        {
            this.ConnectionState = TelegramConnectionState.Error;
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
            this.ConnectionState = TelegramConnectionState.NotConfigured;
            return Task.FromResult(TelegramInboundResult.IgnoredUnsupportedChatType);
        }

        if (!isPrivateChat)
        {
            return Task.FromResult(TelegramInboundResult.IgnoredUnsupportedChatType);
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(TelegramInboundResult.IgnoredEmptyText);
        }

        if (this.Configuration.AuthorizedChatId is null)
        {
            if (text == "/start")
            {
                this.Configuration.AuthorizedChatId = chatId;
                this.configurationStore.Save(this.Configuration);
                this.ConnectionState = TelegramConnectionState.Connected;
                return Task.FromResult(TelegramInboundResult.Authorized);
            }

            this.ConnectionState = TelegramConnectionState.WaitingForStart;
            return Task.FromResult(TelegramInboundResult.IgnoredUnauthorizedChat);
        }

        if (this.Configuration.AuthorizedChatId.Value != chatId)
        {
            return Task.FromResult(TelegramInboundResult.IgnoredUnauthorizedChat);
        }

        this.ConnectionState = TelegramConnectionState.Connected;
        return Task.FromResult(TelegramInboundResult.Accepted);
    }

    public async Task<TelegramSendResult> SendToAuthorizedChatAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var chatId = this.Configuration.AuthorizedChatId;
        if (chatId is null)
        {
            return TelegramSendResult.Failure("no authorized chat yet");
        }

        return await this.clientAdapter.SendTextAsync(chatId.Value, text, cancellationToken).ConfigureAwait(false);
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
