namespace FFXIVTelegram.Tests.Notifications;

using System;
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
}
