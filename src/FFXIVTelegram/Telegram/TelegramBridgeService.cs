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

    public async Task<TelegramPollResult> PollOnceAsync(CancellationToken cancellationToken)
    {
        if (!this.Configuration.HasTelegramBotToken)
        {
            this.ConnectionState = TelegramConnectionState.NotConfigured;
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

            this.ConnectionState = this.ResolveConnectionState(this.Configuration);
            return new TelegramPollResult(this.nextUpdateOffset, acceptedMessages);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return TelegramPollResult.Empty(this.nextUpdateOffset);
        }
        catch (OperationCanceledException)
        {
            this.ConnectionState = TelegramConnectionState.Error;
            throw;
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
                var previousAuthorizedChatId = this.Configuration.AuthorizedChatId;
                this.Configuration.AuthorizedChatId = chatId;

                try
                {
                    this.configurationStore.Save(this.Configuration);
                    this.ConnectionState = TelegramConnectionState.Connected;
                    return Task.FromResult(TelegramInboundResult.Authorized);
                }
                catch
                {
                    this.Configuration.AuthorizedChatId = previousAuthorizedChatId;
                    this.ConnectionState = this.ResolveConnectionState(this.Configuration);
                    throw;
                }
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

        if (!this.Configuration.HasTelegramBotToken)
        {
            return TelegramSendResult.Failure("bot token not configured");
        }

        var chatId = this.Configuration.AuthorizedChatId;
        if (chatId is null)
        {
            return TelegramSendResult.Failure("no authorized chat yet");
        }

        return await this.clientAdapter.SendTextAsync(chatId.Value, text, cancellationToken).ConfigureAwait(false);
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
