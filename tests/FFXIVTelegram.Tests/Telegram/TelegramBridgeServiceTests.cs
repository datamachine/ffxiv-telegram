namespace FFXIVTelegram.Tests.Telegram;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Telegram;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class TelegramBridgeServiceTests
{
    [Fact]
    public async Task FirstPrivateStartClaimsAuthorizedChatAndPersistsConfiguration()
    {
        var fixture = this.CreateService();

        var result = await fixture.Service.HandleIncomingTextAsync(chatId: 42, isPrivateChat: true, text: "/start");

        Assert.Equal(TelegramInboundResult.Authorized, result);
        Assert.Equal(42, fixture.Service.Configuration.AuthorizedChatId);
        Assert.Same(fixture.Service.Configuration, fixture.PluginProxy.SavedConfiguration);
        Assert.Equal(42, ((FfxivTelegramConfiguration)fixture.PluginProxy.SavedConfiguration!).AuthorizedChatId);
        Assert.Equal(TelegramConnectionState.Connected, fixture.Service.ConnectionState);
    }

    [Fact]
    public void ConnectionStateReflectsLiveConfigurationChanges()
    {
        var fixture = this.CreateService(authorizedChatId: 42);

        Assert.Equal(TelegramConnectionState.Connected, fixture.Service.ConnectionState);

        fixture.Service.Configuration.TelegramBotToken = string.Empty;
        Assert.Equal(TelegramConnectionState.NotConfigured, fixture.Service.ConnectionState);

        fixture.Service.Configuration.TelegramBotToken = "token";
        fixture.Service.Configuration.AuthorizedChatId = null;
        Assert.Equal(TelegramConnectionState.WaitingForStart, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task ConnectionStateRecoversFromTransportErrorWhenConfigurationChanges()
    {
        var fixture = this.CreateService(authorizedChatId: 42, adapterThrowsTaskCanceledOnPoll: true);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => fixture.Service.PollOnceAsync(CancellationToken.None));
        Assert.Equal(TelegramConnectionState.Error, fixture.Service.ConnectionState);

        fixture.Service.Configuration.TelegramBotToken = string.Empty;
        Assert.Equal(TelegramConnectionState.NotConfigured, fixture.Service.ConnectionState);

        fixture.Service.Configuration.TelegramBotToken = "token";
        fixture.Service.Configuration.AuthorizedChatId = null;
        Assert.Equal(TelegramConnectionState.WaitingForStart, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task SuccessfulDirectInboundClearsTransportErrorSnapshot()
    {
        var fixture = this.CreateService(authorizedChatId: 42, adapterThrowsTaskCanceledOnPoll: true);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => fixture.Service.PollOnceAsync(CancellationToken.None));
        Assert.Equal(TelegramConnectionState.Error, fixture.Service.ConnectionState);

        var result = await fixture.Service.HandleIncomingTextAsync(chatId: 42, isPrivateChat: true, text: "hello");

        Assert.Equal(TelegramInboundResult.Accepted, result);
        Assert.Equal(TelegramConnectionState.Connected, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task PollFailureBeforeAuthorizationReportsError()
    {
        var fixture = this.CreateService(adapterThrowsTaskCanceledOnPoll: true);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => fixture.Service.PollOnceAsync(CancellationToken.None));

        Assert.Equal(TelegramConnectionState.Error, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task TransportErrorClearsWhenTokenOrAuthorizationChanges()
    {
        var fixture = this.CreateService(authorizedChatId: 42, adapterThrowsTaskCanceledOnPoll: true);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => fixture.Service.PollOnceAsync(CancellationToken.None));
        Assert.Equal(TelegramConnectionState.Error, fixture.Service.ConnectionState);

        fixture.Service.Configuration.TelegramBotToken = "replacement-token";
        Assert.Equal(TelegramConnectionState.Connected, fixture.Service.ConnectionState);

        fixture.Service.Configuration.AuthorizedChatId = null;
        Assert.Equal(TelegramConnectionState.WaitingForStart, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task StartAuthorizationRollsBackWhenSaveFails()
    {
        var fixture = this.CreateService(saveThrows: true);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.HandleIncomingTextAsync(chatId: 42, isPrivateChat: true, text: "/start"));

        Assert.Null(fixture.Service.Configuration.AuthorizedChatId);
        Assert.Null(fixture.PluginProxy.SavedConfiguration);
        Assert.Equal(TelegramConnectionState.WaitingForStart, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task IgnoresMessagesFromUnauthorizedChat()
    {
        var fixture = this.CreateService(authorizedChatId: 42);

        var result = await fixture.Service.HandleIncomingTextAsync(chatId: 99, isPrivateChat: true, text: "hello");

        Assert.Equal(TelegramInboundResult.IgnoredUnauthorizedChat, result);
    }

    [Fact]
    public async Task RejectsNonPrivateStartWithoutClaimingAuthorization()
    {
        var fixture = this.CreateService();

        var result = await fixture.Service.HandleIncomingTextAsync(chatId: 42, isPrivateChat: false, text: "/start");

        Assert.Equal(TelegramInboundResult.IgnoredUnsupportedChatType, result);
        Assert.Null(fixture.Service.Configuration.AuthorizedChatId);
        Assert.Null(fixture.PluginProxy.SavedConfiguration);
        Assert.Equal(TelegramConnectionState.WaitingForStart, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task PollOnceReturnsAcceptedInboundMessagesAndAdvancesOffset()
    {
        var fixture = this.CreateService(
            authorizedChatId: 42,
            updateBatches:
            [
                [
                    new TelegramUpdate(UpdateId: 10, MessageId: 100, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "accepted"),
                    new TelegramUpdate(UpdateId: 11, MessageId: 101, ReplyToMessageId: 100, ChatId: 42, IsPrivateChat: false, Text: "ignored"),
                ],
                [],
            ]);

        var firstPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);
        var secondPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(new[] { 0L, 12L }, fixture.Adapter.RequestedOffsets);
        Assert.Equal(12, firstPoll.NextOffset);
        Assert.Single(firstPoll.AcceptedMessages);

        var accepted = Assert.Single(firstPoll.AcceptedMessages);
        Assert.Equal(10, accepted.UpdateId);
        Assert.Equal(100, accepted.MessageId);
        Assert.Null(accepted.ReplyToMessageId);
        Assert.Equal(42, accepted.ChatId);
        Assert.Equal("accepted", accepted.Text);

        Assert.Equal(12, secondPoll.NextOffset);
        Assert.Empty(secondPoll.AcceptedMessages);
    }

    [Fact]
    public async Task PollOnceIgnoresBotAuthoredMessages()
    {
        var fixture = this.CreateService(
            authorizedChatId: 42,
            updateBatches:
            [
                [
                    new TelegramUpdate(
                        UpdateId: 10,
                        MessageId: 100,
                        ReplyToMessageId: null,
                        ChatId: 42,
                        IsPrivateChat: true,
                        Text: "echo",
                        FromUserId: 999,
                        IsFromBot: true),
                ],
                [],
            ]);

        var firstPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);
        var secondPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(11, firstPoll.NextOffset);
        Assert.Empty(firstPoll.AcceptedMessages);
        Assert.Equal(new[] { 0L, 11L }, fixture.Adapter.RequestedOffsets);
        Assert.Equal(11, secondPoll.NextOffset);
        Assert.Empty(secondPoll.AcceptedMessages);
    }

    [Fact]
    public async Task PollOnceTreatsCancellationAsNormalStop()
    {
        var fixture = this.CreateService(authorizedChatId: 42);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var result = await fixture.Service.PollOnceAsync(cancellationTokenSource.Token);

        Assert.Empty(result.AcceptedMessages);
        Assert.Equal(0, result.NextOffset);
        Assert.Equal(TelegramConnectionState.Connected, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task PollOncePropagatesAdapterCancellationWhenCallerDidNotCancel()
    {
        var fixture = this.CreateService(authorizedChatId: 42, adapterThrowsTaskCanceledOnPoll: true);

        var exception = await Assert.ThrowsAsync<TaskCanceledException>(
            () => fixture.Service.PollOnceAsync(CancellationToken.None));

        Assert.IsType<TaskCanceledException>(exception);
        Assert.Equal(TelegramConnectionState.Error, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task SendToAuthorizedChatReturnsMessageId()
    {
        var fixture = this.CreateService(authorizedChatId: 42, sendResult: TelegramSendResult.Ok(987));

        var result = await fixture.Service.SendToAuthorizedChatAsync("hello");

        Assert.True(result.Success);
        Assert.Equal(987, result.MessageId);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(1, fixture.Adapter.SendCallCount);
        Assert.Equal(42, fixture.Adapter.LastSendChatId);
        Assert.Equal("hello", fixture.Adapter.LastSendText);
    }

    [Fact]
    public async Task SendToAuthorizedChatWithoutAuthorizationDoesNotCallAdapter()
    {
        var fixture = this.CreateService();

        var result = await fixture.Service.SendToAuthorizedChatAsync("hello");

        Assert.False(result.Success);
        Assert.Null(result.MessageId);
        Assert.Equal("no authorized chat yet", result.ErrorMessage);
        Assert.Equal(0, fixture.Adapter.SendCallCount);
    }

    [Fact]
    public async Task SendToAuthorizedChatWithoutBotTokenDoesNotCallAdapter()
    {
        var fixture = this.CreateService(authorizedChatId: 42, telegramBotToken: string.Empty);

        var result = await fixture.Service.SendToAuthorizedChatAsync("hello");

        Assert.False(result.Success);
        Assert.Null(result.MessageId);
        Assert.Equal("bot token not configured", result.ErrorMessage);
        Assert.Equal(0, fixture.Adapter.SendCallCount);
    }

    [Fact]
    public async Task SendToAuthorizedChatAdapterFailureSetsConnectionStateToError()
    {
        var fixture = this.CreateService(authorizedChatId: 42, adapterThrowsTaskCanceledOnSend: true);

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => fixture.Service.SendToAuthorizedChatAsync("hello"));

        Assert.Equal(TelegramConnectionState.Error, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task SendToAuthorizedChatCancellationByCallerDoesNotSetError()
    {
        var fixture = this.CreateService(authorizedChatId: 42);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Service.SendToAuthorizedChatAsync("hello", cancellationTokenSource.Token));

        Assert.Equal(TelegramConnectionState.Connected, fixture.Service.ConnectionState);
    }

    [Fact]
    public async Task PollOncePreservesPrivateReplyMessageIds()
    {
        var fixture = this.CreateService(
            authorizedChatId: 42,
            updateBatches:
            [
                [
                    new TelegramUpdate(UpdateId: 20, MessageId: 200, ReplyToMessageId: 150, ChatId: 42, IsPrivateChat: true, Text: "reply"),
                ],
            ]);

        var result = await fixture.Service.PollOnceAsync(CancellationToken.None);

        var accepted = Assert.Single(result.AcceptedMessages);
        Assert.Equal(200, accepted.MessageId);
        Assert.Equal(150, accepted.ReplyToMessageId);
    }

    [Fact]
    public async Task PollStartThenForeignChatAdvancesOffsetAndRejectsForeignMessages()
    {
        var fixture = this.CreateService(
            updateBatches:
            [
                [
                    new TelegramUpdate(UpdateId: 50, MessageId: 500, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "/start"),
                ],
                [
                    new TelegramUpdate(UpdateId: 51, MessageId: 501, ReplyToMessageId: null, ChatId: 99, IsPrivateChat: true, Text: "hello"),
                ],
            ]);

        var firstPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);
        var secondPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(42, fixture.Service.Configuration.AuthorizedChatId);
        Assert.Same(fixture.Service.Configuration, fixture.PluginProxy.SavedConfiguration);
        Assert.Equal(new[] { 0L, 51L }, fixture.Adapter.RequestedOffsets);
        Assert.Equal(51, firstPoll.NextOffset);
        Assert.Empty(firstPoll.AcceptedMessages);
        Assert.Equal(52, secondPoll.NextOffset);
        Assert.Empty(secondPoll.AcceptedMessages);
    }

    [Fact]
    public async Task PollOnceResetsOffsetWhenBotTokenChanges()
    {
        var fixture = this.CreateService(
            authorizedChatId: 42,
            updateBatches:
            [
                [
                    new TelegramUpdate(UpdateId: 10, MessageId: 100, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "accepted"),
                ],
                [],
            ]);

        var firstPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);
        fixture.Service.Configuration.TelegramBotToken = "replacement-token";
        fixture.Service.Configuration.AuthorizedChatId = null;
        var secondPoll = await fixture.Service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(11, firstPoll.NextOffset);
        Assert.Equal(0, secondPoll.NextOffset);
        Assert.Equal(new[] { 0L, 0L }, fixture.Adapter.RequestedOffsets);
    }

    [Fact]
    public async Task PollOnceDropsUpdatesReturnedForOldTokenAfterLiveTokenChange()
    {
        var fixture = this.CreateService(authorizedChatId: 42);
        fixture.Adapter.OnGetUpdates = _ =>
        {
            fixture.Service.Configuration.TelegramBotToken = "replacement-token";
            fixture.Service.Configuration.AuthorizedChatId = null;

            return
            [
                new TelegramUpdate(UpdateId: 10, MessageId: 100, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "/start"),
            ];
        };

        var result = await fixture.Service.PollOnceAsync(CancellationToken.None);

        Assert.Equal(0, result.NextOffset);
        Assert.Empty(result.AcceptedMessages);
        Assert.Null(fixture.Service.Configuration.AuthorizedChatId);
        Assert.Equal(new[] { 0L }, fixture.Adapter.RequestedOffsets);
    }

    [Fact]
    public async Task ConcurrentPollOnceCallsAreSerialized()
    {
        var fixture = this.CreateService(authorizedChatId: 42);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var adapterCallCount = 0;

        fixture.Adapter.OnGetUpdatesAsync = async (_, _) =>
        {
            var callNumber = Interlocked.Increment(ref adapterCallCount);
            if (callNumber == 1)
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
                return
                [
                    new TelegramUpdate(UpdateId: 10, MessageId: 100, ReplyToMessageId: null, ChatId: 42, IsPrivateChat: true, Text: "accepted"),
                ];
            }

            return Array.Empty<TelegramUpdate>();
        };

        var firstPollTask = fixture.Service.PollOnceAsync(CancellationToken.None);
        await firstEntered.Task;

        var secondPollTask = fixture.Service.PollOnceAsync(CancellationToken.None);
        await Task.Delay(50);

        Assert.Equal(new[] { 0L }, fixture.Adapter.RequestedOffsets);

        releaseFirst.SetResult();

        var firstPoll = await firstPollTask;
        var secondPoll = await secondPollTask;

        Assert.Equal(11, firstPoll.NextOffset);
        Assert.Equal(11, secondPoll.NextOffset);
        Assert.Equal(new[] { 0L, 11L }, fixture.Adapter.RequestedOffsets);
    }

    private ServiceFixture CreateService(
        long? authorizedChatId = null,
        string telegramBotToken = "token",
        TelegramSendResult? sendResult = null,
        bool saveThrows = false,
        bool adapterThrowsTaskCanceledOnPoll = false,
        bool adapterThrowsTaskCanceledOnSend = false,
        params IReadOnlyList<TelegramUpdate>[] updateBatches)
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = telegramBotToken,
            AuthorizedChatId = authorizedChatId,
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(configuration, out var pluginProxy, out _);
        pluginProxy.ThrowOnSave = saveThrows;
        var store = new ConfigurationStore(plugin);
        var adapter = new StubTelegramClientAdapter(updateBatches)
        {
            SendResult = sendResult ?? TelegramSendResult.Ok(123),
            ThrowTaskCanceledOnPoll = adapterThrowsTaskCanceledOnPoll,
            ThrowTaskCanceledOnSend = adapterThrowsTaskCanceledOnSend,
        };
        var service = new TelegramBridgeService(configuration, adapter, store);

        return new ServiceFixture(service, adapter, pluginProxy);
    }

    private sealed record ServiceFixture(
        TelegramBridgeService Service,
        StubTelegramClientAdapter Adapter,
        DalamudPluginInterfaceTestDouble PluginProxy);

    private sealed class StubTelegramClientAdapter : ITelegramClientAdapter
    {
        private readonly Queue<IReadOnlyList<TelegramUpdate>> updateBatches = new();

        public StubTelegramClientAdapter(IEnumerable<IReadOnlyList<TelegramUpdate>> updateBatches)
        {
            foreach (var batch in updateBatches)
            {
                this.updateBatches.Enqueue(batch);
            }
        }

        public List<long> RequestedOffsets { get; } = new();

        public int SendCallCount { get; private set; }

        public long? LastSendChatId { get; private set; }

        public string? LastSendText { get; private set; }

        public TelegramSendResult SendResult { get; set; } = TelegramSendResult.Ok(123);

        public bool ThrowTaskCanceledOnPoll { get; set; }

        public bool ThrowTaskCanceledOnSend { get; set; }

        public Func<long, IReadOnlyList<TelegramUpdate>>? OnGetUpdates { get; set; }

        public Func<long, CancellationToken, Task<IReadOnlyList<TelegramUpdate>>>? OnGetUpdatesAsync { get; set; }

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
        {
            this.RequestedOffsets.Add(offset);

            if (this.ThrowTaskCanceledOnPoll)
            {
                throw new TaskCanceledException("adapter timeout");
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (this.OnGetUpdatesAsync is not null)
            {
                return this.OnGetUpdatesAsync(offset, cancellationToken);
            }

            if (this.OnGetUpdates is not null)
            {
                return Task.FromResult(this.OnGetUpdates(offset));
            }

            if (this.updateBatches.Count > 0)
            {
                return Task.FromResult(this.updateBatches.Dequeue());
            }

            return Task.FromResult<IReadOnlyList<TelegramUpdate>>(Array.Empty<TelegramUpdate>());
        }

        public Task<TelegramSendResult> SendTextAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            this.SendCallCount++;
            this.LastSendChatId = chatId;
            this.LastSendText = text;

            if (this.ThrowTaskCanceledOnSend)
            {
                throw new TaskCanceledException("adapter timeout");
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(this.SendResult);
        }
    }
}
