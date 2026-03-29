namespace FFXIVTelegram;

using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Chat;
using FFXIVTelegram.Commands;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.Interop;
using FFXIVTelegram.Telegram;
using FFXIVTelegram.UI;
using System;

public sealed class FfxivTelegramPlugin : IDalamudPlugin
{
    private readonly CommandHandler commandHandler;
    private readonly TelegramBridgeService telegramBridge;
    private readonly TelegramHttpClientAdapter telegramClientAdapter;
    private readonly ChatInjectionService chatInjectionService;
    private readonly UiController uiController;
    private readonly GameChatMonitor gameChatMonitor;

    public string Name => PluginConstants.PluginName;

    public FfxivTelegramPlugin(
        IDalamudPluginInterface pluginInterface,
        IChatGui chatGui,
        ICommandManager commandManager,
        IFramework framework,
        IGameInteropProvider gameInteropProvider)
    {
        var configurationStore = new ConfigurationStore(pluginInterface);
        var configuration = configurationStore.Load();
        var configWindow = new ConfigWindow(configuration, configurationStore);
        this.telegramClientAdapter = new TelegramHttpClientAdapter(configuration);
        var replyMap = new TelegramReplyMap(capacity: 100, maxAge: TimeSpan.FromMinutes(30));
        this.telegramBridge = new TelegramBridgeService(configuration, this.telegramClientAdapter, configurationStore);
        this.gameChatMonitor = new GameChatMonitor(chatGui, configuration, this.telegramBridge, replyMap);
        var frameworkDispatcher = new FrameworkDispatcher(framework);
        var gameChatExecutor = new XivChatGameChatExecutor(gameInteropProvider);
        this.chatInjectionService = new ChatInjectionService(frameworkDispatcher, gameChatExecutor, TimeSpan.FromMilliseconds(500));
        this.commandHandler = new CommandHandler(commandManager, this.chatInjectionService);
        this.uiController = new UiController(pluginInterface, configWindow);
    }

    public void Dispose()
    {
        this.commandHandler.Dispose();
        this.gameChatMonitor.Dispose();
        this.telegramClientAdapter.Dispose();
        this.uiController.Dispose();
    }
}
