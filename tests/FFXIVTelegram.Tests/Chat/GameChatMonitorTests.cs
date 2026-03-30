namespace FFXIVTelegram.Tests.Chat;

using System;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Chat;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Telegram;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class GameChatMonitorTests
{
    [Fact]
    public async Task SubscribesToChatMessagesAndForwardsEnabledChannels()
    {
        var fixture = this.CreateFixture(sendResult: TelegramSendResult.Ok(321));

        var isHandled = false;
        fixture.ChatGuiProxy.RaiseChatMessage(
            XivChatType.FreeCompany,
            timestamp: 0,
            sender: "Alice Example",
            message: "Hello!",
            ref isHandled);

        await Task.Yield();

        Assert.Equal(1, fixture.Adapter.SendCallCount);
        Assert.Equal("[FC] <Alice Example>: Hello!", fixture.Adapter.LastSendText);
        Assert.True(fixture.ReplyMap.TryGetRoute(321, out var route));
        Assert.Equal(ChatRoute.FreeCompany(), route);
        Assert.Equal(1, fixture.ChatGuiProxy.ChatMessageSubscriberCount);

        fixture.Monitor.Dispose();

        isHandled = false;
        fixture.ChatGuiProxy.RaiseChatMessage(
            XivChatType.FreeCompany,
            timestamp: 0,
            sender: "Alice Example",
            message: "Hello again!",
            ref isHandled);

        await Task.Yield();

        Assert.Equal(1, fixture.Adapter.SendCallCount);
        Assert.Equal(0, fixture.ChatGuiProxy.ChatMessageSubscriberCount);
    }

    [Fact]
    public async Task SkipsDisabledChannels()
    {
        var fixture = this.CreateFixture(configure: configuration =>
        {
            configuration.EnablePartyForwarding = false;
        });

        var result = await fixture.Monitor.ForwardAsync(XivChatType.Party, "Alice Example", "Hello!");

        Assert.Null(result);
        Assert.Equal(0, fixture.Adapter.SendCallCount);
        Assert.False(fixture.ReplyMap.TryGetRoute(123, out _));
    }

    [Fact]
    public async Task TracksLastActiveTellRouteEvenWhenTellForwardingIsDisabled()
    {
        var fixture = this.CreateFixture(configure: configuration =>
        {
            configuration.EnableTellForwarding = false;
        });

        var result = await fixture.Monitor.ForwardAsync(XivChatType.TellIncoming, "Alice Example", "Hello!");

        Assert.Null(result);
        Assert.Equal(0, fixture.Adapter.SendCallCount);
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastActiveRoute);
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastTellRoute);
    }

    [Fact]
    public async Task StoresReplyMapOnlyAfterSuccessfulSend()
    {
        var successFixture = this.CreateFixture(sendResult: TelegramSendResult.Ok(444));

        var successResult = await successFixture.Monitor.ForwardAsync(XivChatType.Party, "Alice Example", "Hello!");

        Assert.True(successResult!.Success);
        Assert.True(successFixture.ReplyMap.TryGetRoute(444, out var successRoute));
        Assert.Equal(ChatRoute.Party(), successRoute);

        var failureFixture = this.CreateFixture(sendResult: TelegramSendResult.Failure("nope"));

        var failureResult = await failureFixture.Monitor.ForwardAsync(XivChatType.Party, "Alice Example", "Hello!");

        Assert.False(failureResult!.Success);
        Assert.False(failureFixture.ReplyMap.TryGetRoute(123, out _));
    }

    [Fact]
    public async Task PartyMessageFromLocalPlayerIsNotForwarded()
    {
        var fixture = this.CreateFixture(localPlayerName: "Alice Example");

        var result = await fixture.Monitor.ForwardAsync(XivChatType.Party, "Alice Example", "Hello!");

        Assert.Null(result);
        Assert.Empty(fixture.Adapter.SentTexts);
        Assert.Equal(ChatRoute.Party(), fixture.Monitor.CurrentRouteContext.LastActiveRoute);
        Assert.Null(fixture.Monitor.CurrentRouteContext.LastTellRoute);
    }

    [Fact]
    public async Task OutgoingTellFromLocalPlayerIsNotForwarded()
    {
        var fixture = this.CreateFixture(localPlayerName: "Alice Example");

        var result = await fixture.Monitor.ForwardAsync(XivChatType.TellOutgoing, "Alice Example", "Hello!");

        Assert.Null(result);
        Assert.Empty(fixture.Adapter.SentTexts);
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastActiveRoute);
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastTellRoute);
    }

    [Fact]
    public async Task OutgoingTellIsNotForwardedWhenPlayerStateIsUnavailable()
    {
        var fixture = this.CreateFixture(localPlayerName: null);

        var result = await fixture.Monitor.ForwardAsync(XivChatType.TellOutgoing, "Alice Example", "Hello!");

        Assert.Null(result);
        Assert.Empty(fixture.Adapter.SentTexts);
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastActiveRoute);
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastTellRoute);
    }

    [Fact]
    public async Task FreeCompanyMessageFromLocalPlayerIsNotForwarded()
    {
        var fixture = this.CreateFixture(localPlayerName: "Alice Example");

        var result = await fixture.Monitor.ForwardAsync(XivChatType.FreeCompany, "Alice Example", "Hello!");

        Assert.Null(result);
        Assert.Empty(fixture.Adapter.SentTexts);
        Assert.Equal(ChatRoute.FreeCompany(), fixture.Monitor.CurrentRouteContext.LastActiveRoute);
        Assert.Null(fixture.Monitor.CurrentRouteContext.LastTellRoute);
    }

    [Fact]
    public void RecordRouteUsageUpdatesCurrentRouteContext()
    {
        var fixture = this.CreateFixture();

        fixture.Monitor.RecordRouteUsage(ChatRoute.Party());
        Assert.Equal(ChatRoute.Party(), fixture.Monitor.CurrentRouteContext.LastActiveRoute);
        Assert.Null(fixture.Monitor.CurrentRouteContext.LastTellRoute);

        fixture.Monitor.RecordRouteUsage(ChatRoute.Tell("Alice Example"));
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastActiveRoute);
        Assert.Equal(ChatRoute.Tell("Alice Example"), fixture.Monitor.CurrentRouteContext.LastTellRoute);
    }

    private Fixture CreateFixture(
        TelegramSendResult? sendResult = null,
        string? localPlayerName = null,
        Action<FfxivTelegramConfiguration>? configure = null)
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
            AuthorizedChatId = 42,
        };
        configure?.Invoke(configuration);

        var plugin = DalamudPluginInterfaceTestDouble.Create(configuration, out _, out _);
        var store = new ConfigurationStore(plugin);
        var adapter = new StubTelegramClientAdapter
        {
            SendResult = sendResult ?? TelegramSendResult.Ok(123),
        };
        var bridge = new TelegramBridgeService(configuration, adapter, store);
        var replyMap = new TelegramReplyMap(capacity: 10, maxAge: TimeSpan.FromMinutes(30));
        var chatGui = ChatGuiTestDouble.Create(out var chatGuiProxy);
        var playerState = PlayerStateTestDouble.Create(localPlayerName, out _);
        var monitor = new GameChatMonitor(chatGui, playerState, configuration, bridge, replyMap);

        return new Fixture(monitor, bridge, replyMap, adapter, chatGuiProxy);
    }

    private sealed record Fixture(
        GameChatMonitor Monitor,
        TelegramBridgeService Bridge,
        TelegramReplyMap ReplyMap,
        StubTelegramClientAdapter Adapter,
        ChatGuiTestDouble ChatGuiProxy);

    private sealed class StubTelegramClientAdapter : ITelegramClientAdapter
    {
        public int SendCallCount { get; private set; }

        public string? LastSendText { get; private set; }

        public List<string> SentTexts { get; } = [];

        public TelegramSendResult SendResult { get; set; } = TelegramSendResult.Ok(123);

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TelegramUpdate>>(Array.Empty<TelegramUpdate>());
        }

        public Task<TelegramSendResult> SendTextAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            this.SendCallCount++;
            this.LastSendText = text;
            this.SentTexts.Add(text);
            return Task.FromResult(this.SendResult);
        }
    }
}
