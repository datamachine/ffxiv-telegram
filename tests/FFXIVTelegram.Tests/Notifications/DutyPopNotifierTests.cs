namespace FFXIVTelegram.Tests.Notifications;

using System;
using System.Threading;
using System.Threading.Tasks;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Notifications;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class DutyPopNotifierTests
{
    [Fact]
    public void RegistersAddonListenerOnConstructionAndUnregistersOnDispose()
    {
        var fixture = CreateFixture();

        Assert.Equal(1, fixture.AddonLifecycleProxy.RegisterCallCount);

        fixture.Notifier.Dispose();

        Assert.Equal(1, fixture.AddonLifecycleProxy.UnregisterCallCount);
    }

    [Fact]
    public async Task FireWithDutyNameDispatchesReadyMessageWithNoReplyRoute()
    {
        var fixture = CreateFixture();

        await fixture.Notifier.TryFireAsync("The Aetherfont", CancellationToken.None);

        var call = Assert.Single(fixture.Dispatcher.Calls);
        Assert.Equal("[Duty] The Aetherfont is ready", call.Text);
        Assert.Null(call.ReplyRoute);
    }

    [Fact]
    public async Task FireWithNullNameDispatchesFallbackMessage()
    {
        var fixture = CreateFixture();

        await fixture.Notifier.TryFireAsync(null, CancellationToken.None);

        var call = Assert.Single(fixture.Dispatcher.Calls);
        Assert.Equal("[Duty] A duty is ready", call.Text);
        Assert.Null(call.ReplyRoute);
    }

    [Fact]
    public async Task FireWithinSixtySecondsOfPreviousFireIsDeduped()
    {
        var fixture = CreateFixture();

        await fixture.Notifier.TryFireAsync("The Aetherfont", CancellationToken.None);

        fixture.Time.Advance(TimeSpan.FromSeconds(45));
        await fixture.Notifier.TryFireAsync("The Aetherfont", CancellationToken.None);

        Assert.Single(fixture.Dispatcher.Calls);
    }

    [Fact]
    public async Task FireAfterSixtySecondsDispatchesAgain()
    {
        var fixture = CreateFixture();

        await fixture.Notifier.TryFireAsync("The Aetherfont", CancellationToken.None);

        fixture.Time.Advance(TimeSpan.FromSeconds(61));
        await fixture.Notifier.TryFireAsync("The Aetherfont", CancellationToken.None);

        Assert.Equal(2, fixture.Dispatcher.Calls.Count);
    }

    [Fact]
    public async Task ToggleOffPreventsDispatch()
    {
        var fixture = CreateFixture(c => c.EnableDutyPopNotifications = false);

        await fixture.Notifier.TryFireAsync("The Aetherfont", CancellationToken.None);

        Assert.Empty(fixture.Dispatcher.Calls);
    }

    private static Fixture CreateFixture(Action<FfxivTelegramConfiguration>? configure = null)
    {
        var configuration = new FfxivTelegramConfiguration
        {
            EnableDutyPopNotifications = true,
        };
        configure?.Invoke(configuration);

        var addonLifecycle = AddonLifecycleTestDouble.Create(out var addonLifecycleProxy);
        var dispatcher = new FakeNotificationDispatcher();
        var time = new ManualTimeProvider();
        var notifier = new DutyPopNotifier(addonLifecycle, dispatcher, configuration, time);

        return new Fixture(notifier, addonLifecycleProxy, dispatcher, time, configuration);
    }

    private sealed record Fixture(
        DutyPopNotifier Notifier,
        AddonLifecycleTestDouble AddonLifecycleProxy,
        FakeNotificationDispatcher Dispatcher,
        ManualTimeProvider Time,
        FfxivTelegramConfiguration Configuration);
}
