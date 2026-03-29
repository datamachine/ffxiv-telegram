namespace FFXIVTelegram;

using Dalamud.Plugin;
using FFXIVTelegram.Chat;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.UI;
using System;

public sealed class FfxivTelegramPlugin : IDalamudPlugin
{
    private readonly UiController uiController;
    private readonly GameChatMonitor gameChatMonitor;

    public string Name => PluginConstants.PluginName;

    public FfxivTelegramPlugin(IDalamudPluginInterface pluginInterface)
    {
        var configurationStore = new ConfigurationStore(pluginInterface);
        var configuration = configurationStore.Load();
        var configWindow = new ConfigWindow(configuration, configurationStore);
        var replyMap = new TelegramReplyMap(capacity: 100, maxAge: TimeSpan.FromMinutes(30));
        this.gameChatMonitor = new GameChatMonitor(replyMap);
        this.uiController = new UiController(pluginInterface, configWindow);
    }

    public void Dispose()
    {
        this.gameChatMonitor.Dispose();
        this.uiController.Dispose();
    }
}
