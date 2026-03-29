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
    private static readonly TimeSpan PollingIdleDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PollingErrorDelay = TimeSpan.FromSeconds(5);

    private readonly CommandHandler commandHandler;
    private readonly TelegramBridgeService telegramBridge;
    private readonly TelegramHttpClientAdapter telegramClientAdapter;
    private readonly ChatInjectionService chatInjectionService;
    private readonly UiController uiController;
    private readonly GameChatMonitor gameChatMonitor;
    private readonly TelegramInboundPipeline inboundPipeline;
    private readonly CancellationTokenSource shutdownTokenSource = new();
    private readonly Task pollingTask;

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
        this.inboundPipeline = new TelegramInboundPipeline(
            new RouteResolver(new RouteTagParser()),
            replyMap,
            () => this.gameChatMonitor.CurrentRouteContext,
            this.chatInjectionService,
            this.gameChatMonitor.RecordRouteUsage,
            async (message, cancellationToken) =>
            {
                await this.telegramBridge.SendToAuthorizedChatAsync(message, cancellationToken).ConfigureAwait(false);
            });
        this.commandHandler = new CommandHandler(commandManager, this.chatInjectionService);
        this.uiController = new UiController(pluginInterface, configWindow);
        this.uiController.ConnectionState = this.telegramBridge.ConnectionState;
        this.pollingTask = Task.Run(() => this.RunPollingLoopAsync(this.shutdownTokenSource.Token));
    }

    public void Dispose()
    {
        this.shutdownTokenSource.Cancel();
        try
        {
            this.pollingTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        this.shutdownTokenSource.Dispose();
        this.commandHandler.Dispose();
        this.gameChatMonitor.Dispose();
        this.chatInjectionService.Dispose();
        this.telegramClientAdapter.Dispose();
        this.uiController.Dispose();
    }

    private async Task RunPollingLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            this.uiController.ConnectionState = this.telegramBridge.ConnectionState;

            try
            {
                var pollResult = await this.telegramBridge.PollOnceAsync(cancellationToken).ConfigureAwait(false);
                this.uiController.ConnectionState = this.telegramBridge.ConnectionState;

                foreach (var message in pollResult.AcceptedMessages)
                {
                    await this.inboundPipeline.HandleAsync(message, cancellationToken).ConfigureAwait(false);
                    this.uiController.ConnectionState = this.telegramBridge.ConnectionState;
                }

                await Task.Delay(PollingIdleDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                this.uiController.ConnectionState = this.telegramBridge.ConnectionState;

                try
                {
                    await Task.Delay(PollingErrorDelay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }
}
