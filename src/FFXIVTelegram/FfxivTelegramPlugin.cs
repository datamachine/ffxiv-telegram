namespace FFXIVTelegram;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Chat;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Telegram;
using FFXIVTelegram.UI;
using System;

public sealed class FfxivTelegramPlugin : IDalamudPlugin
{
    private readonly TelegramHttpClientAdapter telegramClientAdapter;
    private readonly UiController uiController;
    private readonly GameChatMonitor gameChatMonitor;

    public string Name => PluginConstants.PluginName;

    public FfxivTelegramPlugin(IDalamudPluginInterface pluginInterface, IChatGui chatGui)
    {
        var configurationStore = new ConfigurationStore(pluginInterface);
        var configuration = configurationStore.Load();
        var configWindow = new ConfigWindow(configuration, configurationStore);
        this.telegramClientAdapter = new TelegramHttpClientAdapter(configuration);
        var replyMap = new TelegramReplyMap(capacity: 100, maxAge: TimeSpan.FromMinutes(30));
        var telegramBridge = new TelegramBridgeService(configuration, this.telegramClientAdapter, configurationStore);
        this.gameChatMonitor = new GameChatMonitor(chatGui, configuration, telegramBridge, replyMap);
        this.uiController = new UiController(pluginInterface, configWindow);
    }

    public void Dispose()
    {
        this.gameChatMonitor.Dispose();
        this.telegramClientAdapter.Dispose();
        this.uiController.Dispose();
    }
}
