namespace FFXIVTelegram.Telegram;

using FFXIVTelegram.Configuration;

public sealed class TelegramBridgeService
{
    private readonly ITelegramClientAdapter clientAdapter;
    private readonly ConfigurationStore configurationStore;
    private TransportErrorContext? transportErrorContext;
    private string? pollingToken;
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

    public TelegramConnectionState ConnectionState
    {
        get
        {
            var liveState = this.ResolveConnectionState(this.Configuration);

            if (liveState == TelegramConnectionState.NotConfigured)
            {
                return liveState;
            }

            return this.transportErrorContext is not null && this.transportErrorContext.Value.Matches(this.Configuration)
                ? TelegramConnectionState.Error
                : liveState;
        }
    }

    public async Task<TelegramPollResult> PollOnceAsync(CancellationToken cancellationToken)
    {
        this.RefreshPollingCursorIfBotTokenChanged();

        if (!this.Configuration.HasTelegramBotToken)
        {
            this.transportErrorContext = null;
            return TelegramPollResult.Empty(this.nextUpdateOffset);
        }

        try
        {
            var pollingToken = this.Configuration.TelegramBotToken;
            var pollingOffset = this.nextUpdateOffset;
            var updates = await this.clientAdapter.GetUpdatesAsync(pollingOffset, cancellationToken).ConfigureAwait(false);
            if (!string.Equals(pollingToken, this.Configuration.TelegramBotToken, StringComparison.Ordinal))
            {
                this.RefreshPollingCursorIfBotTokenChanged();
                this.transportErrorContext = null;
                return TelegramPollResult.Empty(this.nextUpdateOffset);
            }

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

            this.transportErrorContext = null;
            return new TelegramPollResult(this.nextUpdateOffset, acceptedMessages);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TelegramPollResult.Empty(this.nextUpdateOffset);
        }
        catch (OperationCanceledException)
        {
            this.transportErrorContext = TransportErrorContext.Capture(this.Configuration);
            throw;
        }
        catch
        {
            this.transportErrorContext = TransportErrorContext.Capture(this.Configuration);
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
                var previousAuthorizedChatId = this.Configuration.AuthorizedChatId;
                this.Configuration.AuthorizedChatId = chatId;

                try
                {
                    this.configurationStore.Save(this.Configuration);
                    return Task.FromResult(TelegramInboundResult.Authorized);
                }
                catch
                {
                    this.Configuration.AuthorizedChatId = previousAuthorizedChatId;
                    throw;
                }
            }

            return Task.FromResult(TelegramInboundResult.IgnoredUnauthorizedChat);
        }

        if (this.Configuration.AuthorizedChatId.Value != chatId)
        {
            return Task.FromResult(TelegramInboundResult.IgnoredUnauthorizedChat);
        }

        this.transportErrorContext = null;
        return Task.FromResult(TelegramInboundResult.Accepted);
    }

    public async Task<TelegramSendResult> SendToAuthorizedChatAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!this.Configuration.HasTelegramBotToken)
        {
            this.transportErrorContext = null;
            return TelegramSendResult.Failure("bot token not configured");
        }

        var chatId = this.Configuration.AuthorizedChatId;
        if (chatId is null)
        {
            this.transportErrorContext = null;
            return TelegramSendResult.Failure("no authorized chat yet");
        }

        try
        {
            var result = await this.clientAdapter.SendTextAsync(chatId.Value, text, cancellationToken).ConfigureAwait(false);
            this.transportErrorContext = null;
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            this.transportErrorContext = TransportErrorContext.Capture(this.Configuration);
            throw;
        }
        catch
        {
            this.transportErrorContext = TransportErrorContext.Capture(this.Configuration);
            throw;
        }
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

    private void RefreshPollingCursorIfBotTokenChanged()
    {
        var currentToken = this.Configuration.HasTelegramBotToken
            ? this.Configuration.TelegramBotToken
            : null;

        if (string.Equals(this.pollingToken, currentToken, StringComparison.Ordinal))
        {
            return;
        }

        this.pollingToken = currentToken;
        this.nextUpdateOffset = 0;
    }

    private readonly record struct TransportErrorContext(string TelegramBotToken, long? AuthorizedChatId)
    {
        public static TransportErrorContext Capture(FfxivTelegramConfiguration configuration)
        {
            return new TransportErrorContext(configuration.TelegramBotToken, configuration.AuthorizedChatId);
        }

        public bool Matches(FfxivTelegramConfiguration configuration)
        {
            return string.Equals(this.TelegramBotToken, configuration.TelegramBotToken, StringComparison.Ordinal)
                && this.AuthorizedChatId == configuration.AuthorizedChatId;
        }
    }
}
