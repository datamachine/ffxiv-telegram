namespace FFXIVTelegram.Commands;

using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Interop;

public sealed class CommandHandler : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly IChatInjectionCommandTarget chatInjectionService;
    private readonly Action<string>? notifyFailure;

    public CommandHandler(
        ICommandManager commandManager,
        IChatInjectionCommandTarget chatInjectionService,
        Action<string>? notifyFailure = null)
    {
        this.commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        this.chatInjectionService = chatInjectionService ?? throw new ArgumentNullException(nameof(chatInjectionService));
        this.notifyFailure = notifyFailure;

        this.commandManager.AddHandler(PluginConstants.CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "/ffxivtelegram testinject <text>",
            ShowInHelp = true,
        });
    }

    public void Dispose()
    {
        this.commandManager.RemoveHandler(PluginConstants.CommandName);
    }

    private void OnCommand(string command, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return;
        }

        const string testInjectPrefix = "testinject ";
        if (!arguments.StartsWith(testInjectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var payload = arguments[testInjectPrefix.Length..].Trim();
        if (payload.Length == 0)
        {
            return;
        }

        _ = this.EnqueueRawAsync(payload);
    }

    private async Task EnqueueRawAsync(string payload)
    {
        try
        {
            await this.chatInjectionService.EnqueueRawAsync(payload).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            this.notifyFailure?.Invoke($"Test injection failed: {exception.Message}");
        }
    }
}
