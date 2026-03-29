namespace FFXIVTelegram.UI;

using System;
using Dalamud.Plugin;
using FFXIVTelegram.Telegram;

public sealed class UiController : IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;

    private readonly ConfigWindow configWindow;

    public UiController(IDalamudPluginInterface pluginInterface, ConfigWindow configWindow)
    {
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.configWindow = configWindow ?? throw new ArgumentNullException(nameof(configWindow));

        this.pluginInterface.UiBuilder.Draw += this.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigWindow;
    }

    public TelegramConnectionState ConnectionState
    {
        get => this.configWindow.ConnectionState;
        set => this.configWindow.ConnectionState = value;
    }

    public void Dispose()
    {
        this.pluginInterface.UiBuilder.Draw -= this.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigWindow;
    }

    private void Draw()
    {
        this.configWindow.Draw();
    }

    private void OpenConfigWindow()
    {
        this.configWindow.IsOpen = true;
    }
}
