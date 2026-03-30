namespace FFXIVTelegram.Tests.UI;

using FFXIVTelegram.Telegram;
using FFXIVTelegram.UI;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class UiControllerTests
{
    [Fact]
    public void ConstructorWiresUiBuilderEventsAndDisposeUnwiresThem()
    {
        var fakeWindow = new FakeConfigWindow();
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out var pluginProxy, out var uiBuilder);

        using (var controller = new UiController(plugin, fakeWindow))
        {
            uiBuilder.RaiseDraw();
            uiBuilder.RaiseOpenConfigUi();
            uiBuilder.RaiseOpenMainUi();

            Assert.Equal(1, fakeWindow.DrawCount);
            Assert.True(fakeWindow.IsOpen);
        }

        uiBuilder.RaiseDraw();

        Assert.Equal(1, fakeWindow.DrawCount);
    }

    [Fact]
    public void OpenConfigUiSetsWindowOpen()
    {
        var fakeWindow = new FakeConfigWindow();
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out _, out var uiBuilder);

        _ = new UiController(plugin, fakeWindow);

        uiBuilder.RaiseOpenConfigUi();

        Assert.True(fakeWindow.IsOpen);
    }

    [Fact]
    public void OpenMainUiSetsWindowOpen()
    {
        var fakeWindow = new FakeConfigWindow();
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out _, out var uiBuilder);

        _ = new UiController(plugin, fakeWindow);

        uiBuilder.RaiseOpenMainUi();

        Assert.True(fakeWindow.IsOpen);
    }

    private sealed class FakeConfigWindow : IConfigWindow
    {
        public int DrawCount { get; private set; }

        public bool IsOpen { get; set; }

        public TelegramConnectionState ConnectionState { get; set; }

        public void Draw()
        {
            this.DrawCount++;
        }
    }
}
