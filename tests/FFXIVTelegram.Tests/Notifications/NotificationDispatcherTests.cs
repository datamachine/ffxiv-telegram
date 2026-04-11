namespace FFXIVTelegram.Tests.Notifications;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFXIVTelegram.Chat;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Notifications;
using FFXIVTelegram.Telegram;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class NotificationDispatcherTests
{
    [Fact]
    public async Task SuccessfulSendWithReplyRouteStoresMessageIdInReplyMap()
    {
        var fixture = CreateFixture(TelegramSendResult.Ok(777));

        await fixture.Dispatcher.SendAsync("hello", ChatRoute.Tell("Alice Example"), CancellationToken.None);

        Assert.Equal(1, fixture.Adapter.SendCallCount);
        Assert.Equal("hello", fixture.Adapter.LastSendText);
        Assert.True(fixture.ReplyMap.TryGetRoute(777, out var route));
        Assert.Equal(ChatRoute.Tell("Alice Example"), route);
    }

    [Fact]
    public async Task SuccessfulSendWithNullReplyRouteDoesNotTouchReplyMap()
    {
        var fixture = CreateFixture(TelegramSendResult.Ok(778));

        await fixture.Dispatcher.SendAsync("hello", replyRoute: null, CancellationToken.None);

        Assert.Equal(1, fixture.Adapter.SendCallCount);
        Assert.False(fixture.ReplyMap.TryGetRoute(778, out _));
    }

    [Fact]
    public async Task FailedSendDoesNotTouchReplyMap()
    {
        var fixture = CreateFixture(TelegramSendResult.Failure("nope"));

        await fixture.Dispatcher.SendAsync("hello", ChatRoute.Tell("Alice Example"), CancellationToken.None);

        Assert.Equal(1, fixture.Adapter.SendCallCount);
        Assert.False(fixture.ReplyMap.TryGetRoute(123, out _));
    }

    [Fact]
    public async Task SuccessfulSendWithNullMessageIdDoesNotTouchReplyMap()
    {
        var fixture = CreateFixture(new TelegramSendResult(Success: true, MessageId: null));

        await fixture.Dispatcher.SendAsync("hello", ChatRoute.Tell("Alice Example"), CancellationToken.None);

        Assert.Equal(1, fixture.Adapter.SendCallCount);
        Assert.Empty(EnumerateStoredKeys(fixture.ReplyMap));
    }

    private static IEnumerable<long> EnumerateStoredKeys(TelegramReplyMap replyMap)
    {
        for (long i = 0; i < 1000; i++)
        {
            if (replyMap.TryGetRoute(i, out _))
            {
                yield return i;
            }
        }
    }

    private static Fixture CreateFixture(TelegramSendResult sendResult)
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
            AuthorizedChatId = 42,
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(configuration, out _, out _);
        var store = new ConfigurationStore(plugin);
        var adapter = new StubAdapter { SendResult = sendResult };
        var bridge = new TelegramBridgeService(configuration, adapter, store);
        var replyMap = new TelegramReplyMap(capacity: 10, maxAge: TimeSpan.FromMinutes(30));
        var dispatcher = new NotificationDispatcher(bridge, replyMap);

        return new Fixture(dispatcher, bridge, replyMap, adapter);
    }

    private sealed record Fixture(
        NotificationDispatcher Dispatcher,
        TelegramBridgeService Bridge,
        TelegramReplyMap ReplyMap,
        StubAdapter Adapter);

    private sealed class StubAdapter : ITelegramClientAdapter
    {
        public int SendCallCount { get; private set; }

        public string? LastSendText { get; private set; }

        public TelegramSendResult SendResult { get; set; } = TelegramSendResult.Ok(123);

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TelegramUpdate>>(Array.Empty<TelegramUpdate>());
        }

        public Task<TelegramSendResult> SendTextAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            this.SendCallCount++;
            this.LastSendText = text;
            return Task.FromResult(this.SendResult);
        }
    }
}
