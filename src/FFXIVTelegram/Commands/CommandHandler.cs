namespace FFXIVTelegram.Commands;

using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

public sealed class CommandHandler : IDisposable
{
    private readonly ICommandManager commandManager;
    private readonly Action openConfigWindow;

    public CommandHandler(
        ICommandManager commandManager,
        Action openConfigWindow)
    {
        this.commandManager = commandManager ?? throw new ArgumentNullException(nameof(commandManager));
        this.openConfigWindow = openConfigWindow ?? throw new ArgumentNullException(nameof(openConfigWindow));

        this.commandManager.AddHandler(PluginConstants.CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open the FFXIV Telegram settings window.",
            ShowInHelp = true,
        });
    }

    public void Dispose()
    {
        this.commandManager.RemoveHandler(PluginConstants.CommandName);
    }

    private void OnCommand(string command, string arguments)
    {
        this.openConfigWindow();
    }
}
