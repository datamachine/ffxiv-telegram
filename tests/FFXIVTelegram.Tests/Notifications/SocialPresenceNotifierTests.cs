namespace FFXIVTelegram.Tests.Notifications;

using System;
using System.Threading.Tasks;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Notifications;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class SocialPresenceNotifierTests
{
    [Fact]
    public void SubscribesAndUnsubscribesToChatMessages()
    {
        var fixture = CreateFixture();

        Assert.Equal(1, fixture.ChatGuiProxy.ChatMessageSubscriberCount);

        fixture.Notifier.Dispose();

        Assert.Equal(0, fixture.ChatGuiProxy.ChatMessageSubscriberCount);
    }

    [Fact]
    public async Task FriendLoginDispatchesFriendLoggedInWithTellRoute()
    {
        var fixture = CreateFixture();

        RaiseSystemMessage(fixture, "Alice Example has logged in.");
        await Task.Yield();

        var call = Assert.Single(fixture.Dispatcher.Calls);
        Assert.Equal("[Friend] Alice Example logged in", call.Text);
        Assert.Equal(FFXIVTelegram.Chat.ChatRoute.Tell("Alice Example"), call.ReplyRoute);
    }

    [Fact]
    public async Task FriendLogoutDispatchesFriendLoggedOutWithTellRoute()
    {
        var fixture = CreateFixture();

        RaiseSystemMessage(fixture, "Alice Example has logged out.");
        await Task.Yield();

        var call = Assert.Single(fixture.Dispatcher.Calls);
        Assert.Equal("[Friend] Alice Example logged out", call.Text);
        Assert.Equal(FFXIVTelegram.Chat.ChatRoute.Tell("Alice Example"), call.ReplyRoute);
    }

    [Fact]
    public async Task FreeCompanyLoginDispatchesFcLoggedInWithTellRoute()
    {
        var fixture = CreateFixture();

        RaiseSystemMessage(fixture, "[FC]Bob Example has logged in.");
        await Task.Yield();

        var call = Assert.Single(fixture.Dispatcher.Calls);
        Assert.Equal("[FC] Bob Example logged in", call.Text);
        Assert.Equal(FFXIVTelegram.Chat.ChatRoute.Tell("Bob Example"), call.ReplyRoute);
    }

    [Fact]
    public async Task FreeCompanyLogoutDispatchesFcLoggedOutWithTellRoute()
    {
        var fixture = CreateFixture();

        RaiseSystemMessage(fixture, "[FC]Bob Example has logged out.");
        await Task.Yield();

        var call = Assert.Single(fixture.Dispatcher.Calls);
        Assert.Equal("[FC] Bob Example logged out", call.Text);
        Assert.Equal(FFXIVTelegram.Chat.ChatRoute.Tell("Bob Example"), call.ReplyRoute);
    }

    [Fact]
    public async Task NonSystemChatTypeIsIgnored()
    {
        var fixture = CreateFixture();

        var isHandled = false;
        fixture.ChatGuiProxy.RaiseChatMessage(
            XivChatType.FreeCompany,
            timestamp: 0,
            sender: "Alice Example",
            message: "Alice Example has logged in.",
            ref isHandled);
        await Task.Yield();

        Assert.Empty(fixture.Dispatcher.Calls);
    }

    [Fact]
    public async Task UnrecognizedSystemMessageIsIgnored()
    {
        var fixture = CreateFixture();

        RaiseSystemMessage(fixture, "Welcome to Eorzea.");
        await Task.Yield();

        Assert.Empty(fixture.Dispatcher.Calls);
    }

    private static Fixture CreateFixture(Action<FfxivTelegramConfiguration>? configure = null)
    {
        var configuration = new FfxivTelegramConfiguration
        {
            EnableFriendPresenceNotifications = true,
            EnableFreeCompanyPresenceNotifications = true,
        };
        configure?.Invoke(configuration);

        var chatGui = ChatGuiTestDouble.Create(out var chatGuiProxy);
        var dispatcher = new FakeNotificationDispatcher();
        var time = new ManualTimeProvider();
        var notifier = new SocialPresenceNotifier(chatGui, dispatcher, configuration, time);

        return new Fixture(notifier, chatGuiProxy, dispatcher, time, configuration);
    }

    private sealed record Fixture(
        SocialPresenceNotifier Notifier,
        ChatGuiTestDouble ChatGuiProxy,
        FakeNotificationDispatcher Dispatcher,
        ManualTimeProvider Time,
        FfxivTelegramConfiguration Configuration);

    private static void RaiseSystemMessage(Fixture fixture, string text)
    {
        var isHandled = false;
        SeString sender = string.Empty;
        SeString message = text;
        fixture.ChatGuiProxy.RaiseChatMessage(
            XivChatType.SystemMessage,
            timestamp: 0,
            sender: sender,
            message: message,
            ref isHandled);
    }
}
