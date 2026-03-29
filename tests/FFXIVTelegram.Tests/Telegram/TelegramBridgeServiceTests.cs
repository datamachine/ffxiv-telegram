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

    private ServiceFixture CreateService(
        long? authorizedChatId = null,
        TelegramSendResult? sendResult = null,
        params IReadOnlyList<TelegramUpdate>[] updateBatches)
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
            AuthorizedChatId = authorizedChatId,
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(configuration, out var pluginProxy, out _);
        var store = new ConfigurationStore(plugin);
        var adapter = new StubTelegramClientAdapter(updateBatches)
        {
            SendResult = sendResult ?? TelegramSendResult.Ok(123),
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

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
        {
            this.RequestedOffsets.Add(offset);
            cancellationToken.ThrowIfCancellationRequested();

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
            return Task.FromResult(this.SendResult);
        }
    }
}
