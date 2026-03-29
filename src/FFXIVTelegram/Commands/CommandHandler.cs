namespace FFXIVTelegram.Commands;

using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using FFXIVTelegram.Interop;

public sealed class CommandHandler : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly ChatInjectionService chatInjectionService;

    public CommandHandler(ICommandManager commandManager, ChatInjectionService chatInjectionService)
    {
        this.commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        this.chatInjectionService = chatInjectionService ?? throw new ArgumentNullException(nameof(chatInjectionService));

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

        _ = this.chatInjectionService.EnqueueRawAsync(payload);
    }
}
