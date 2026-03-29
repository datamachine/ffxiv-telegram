namespace FFXIVTelegram.Telegram;

using FFXIVTelegram.Configuration;

public sealed class TelegramBridgeService
{
    private readonly ITelegramClientAdapter clientAdapter;
    private readonly ConfigurationStore configurationStore;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object stateGate = new();
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
            var configuration = this.SnapshotConfiguration();
            var liveState = this.ResolveConnectionState(configuration);

            if (liveState == TelegramConnectionState.NotConfigured)
            {
                return liveState;
            }

            var transportErrorContext = this.GetTransportErrorContext();
            return transportErrorContext is not null && transportErrorContext.Value.Matches(configuration)
                ? TelegramConnectionState.Error
                : liveState;
        }
    }

    public async Task<TelegramPollResult> PollOnceAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return TelegramPollResult.Empty(this.nextUpdateOffset);
        }

        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var configuration = this.SnapshotConfiguration();
            this.RefreshPollingCursorIfBotTokenChanged(configuration.TelegramBotToken);

            if (!configuration.HasTelegramBotToken)
            {
                this.SetTransportErrorContext(null);
                return TelegramPollResult.Empty(this.nextUpdateOffset);
            }

            try
            {
                var pollingToken = configuration.TelegramBotToken;
                var pollingOffset = this.nextUpdateOffset;
                var updates = await this.clientAdapter.GetUpdatesAsync(pollingOffset, cancellationToken).ConfigureAwait(false);
                var liveConfiguration = this.SnapshotConfiguration();
                if (!string.Equals(pollingToken, liveConfiguration.TelegramBotToken, StringComparison.Ordinal))
                {
                    this.RefreshPollingCursorIfBotTokenChanged(liveConfiguration.TelegramBotToken);
                    this.SetTransportErrorContext(null);
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

                this.SetTransportErrorContext(null);
                return new TelegramPollResult(this.nextUpdateOffset, acceptedMessages);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return TelegramPollResult.Empty(this.nextUpdateOffset);
            }
            catch (OperationCanceledException)
            {
                this.SetTransportErrorContext(TransportErrorContext.Capture(this.SnapshotConfiguration()));
                throw;
            }
            catch
            {
                this.SetTransportErrorContext(TransportErrorContext.Capture(this.SnapshotConfiguration()));
                throw;
            }
        }
        finally
        {
            this.operationGate.Release();
        }
    }

    public Task<TelegramInboundResult> HandleIncomingTextAsync(
        long chatId,
        bool isPrivateChat,
        string text,
        CancellationToken cancellationToken = default)
    {
        return this.HandleIncomingTextCoreAsync(chatId, isPrivateChat, text, cancellationToken);
    }

    public async Task<TelegramSendResult> SendToAuthorizedChatAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var configuration = this.SnapshotConfiguration();
            if (!configuration.HasTelegramBotToken)
            {
                this.SetTransportErrorContext(null);
                return TelegramSendResult.Failure("bot token not configured");
            }

            if (configuration.AuthorizedChatId is not long chatId)
            {
                this.SetTransportErrorContext(null);
                return TelegramSendResult.Failure("no authorized chat yet");
            }

            var result = await this.clientAdapter.SendTextAsync(chatId, text, cancellationToken).ConfigureAwait(false);
            this.SetTransportErrorContext(null);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            this.SetTransportErrorContext(TransportErrorContext.Capture(this.SnapshotConfiguration()));
            throw;
        }
        catch
        {
            this.SetTransportErrorContext(TransportErrorContext.Capture(this.SnapshotConfiguration()));
            throw;
        }
        finally
        {
            this.operationGate.Release();
        }
    }

    private async Task<TelegramInboundMessage?> HandleIncomingUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken)
    {
        var inboundResult = this.HandleIncomingTextCore(update.ChatId, update.IsPrivateChat, update.Text ?? string.Empty);
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

    private TelegramConnectionState ResolveConnectionState(ConfigurationSnapshot configuration)
    {
        if (!configuration.HasTelegramBotToken)
        {
            return TelegramConnectionState.NotConfigured;
        }

        return configuration.HasAuthorizedChat
            ? TelegramConnectionState.Connected
            : TelegramConnectionState.WaitingForStart;
    }

    private void RefreshPollingCursorIfBotTokenChanged(string? currentToken)
    {
        if (string.Equals(this.pollingToken, currentToken, StringComparison.Ordinal))
        {
            return;
        }

        this.pollingToken = currentToken;
        this.nextUpdateOffset = 0;
    }

    private async Task<TelegramInboundResult> HandleIncomingTextCoreAsync(
        long chatId,
        bool isPrivateChat,
        string text,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        cancellationToken.ThrowIfCancellationRequested();
        await this.operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return this.HandleIncomingTextCore(chatId, isPrivateChat, text);
        }
        finally
        {
            this.operationGate.Release();
        }
    }

    private TelegramInboundResult HandleIncomingTextCore(long chatId, bool isPrivateChat, string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var configuration = this.SnapshotConfiguration();
        if (!configuration.HasTelegramBotToken)
        {
            return TelegramInboundResult.IgnoredUnsupportedChatType;
        }

        if (!isPrivateChat)
        {
            return TelegramInboundResult.IgnoredUnsupportedChatType;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return TelegramInboundResult.IgnoredEmptyText;
        }

        if (configuration.AuthorizedChatId is null)
        {
            if (text == "/start")
            {
                lock (this.Configuration)
                {
                    this.Configuration.AuthorizedChatId = chatId;
                }

                try
                {
                    this.configurationStore.Save(this.Configuration);
                    return TelegramInboundResult.Authorized;
                }
                catch
                {
                    lock (this.Configuration)
                    {
                        this.Configuration.AuthorizedChatId = configuration.AuthorizedChatId;
                    }

                    throw;
                }
            }

            return TelegramInboundResult.IgnoredUnauthorizedChat;
        }

        if (configuration.AuthorizedChatId.Value != chatId)
        {
            return TelegramInboundResult.IgnoredUnauthorizedChat;
        }

        this.SetTransportErrorContext(null);
        return TelegramInboundResult.Accepted;
    }

    private ConfigurationSnapshot SnapshotConfiguration()
    {
        lock (this.Configuration)
        {
            return ConfigurationSnapshot.Capture(this.Configuration);
        }
    }

    private TransportErrorContext? GetTransportErrorContext()
    {
        lock (this.stateGate)
        {
            return this.transportErrorContext;
        }
    }

    private void SetTransportErrorContext(TransportErrorContext? value)
    {
        lock (this.stateGate)
        {
            this.transportErrorContext = value;
        }
    }

    private readonly record struct ConfigurationSnapshot(string TelegramBotToken, long? AuthorizedChatId)
    {
        public bool HasTelegramBotToken => !string.IsNullOrWhiteSpace(this.TelegramBotToken);

        public bool HasAuthorizedChat => this.AuthorizedChatId.HasValue;

        public static ConfigurationSnapshot Capture(FfxivTelegramConfiguration configuration)
        {
            return new ConfigurationSnapshot(configuration.TelegramBotToken, configuration.AuthorizedChatId);
        }
    }

    private readonly record struct TransportErrorContext(string TelegramBotToken, long? AuthorizedChatId)
    {
        public static TransportErrorContext Capture(ConfigurationSnapshot configuration)
        {
            return new TransportErrorContext(configuration.TelegramBotToken, configuration.AuthorizedChatId);
        }

        public bool Matches(ConfigurationSnapshot configuration)
        {
            return string.Equals(this.TelegramBotToken, configuration.TelegramBotToken, StringComparison.Ordinal)
                && this.AuthorizedChatId == configuration.AuthorizedChatId;
        }
    }
}
