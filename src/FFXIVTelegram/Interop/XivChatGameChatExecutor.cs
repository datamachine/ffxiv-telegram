namespace FFXIVTelegram.Interop;

using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;

public sealed unsafe class XivChatGameChatExecutor : IGameChatExecutor
{
    private readonly IGameGui gameGui;

    public XivChatGameChatExecutor(IGameGui gameGui)
    {
        this.gameGui = gameGui ?? throw new ArgumentNullException(nameof(gameGui));
    }

    public void Execute(string inputText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputText);

        var uiModuleAddress = this.gameGui.GetUIModule().Address;
        if (uiModuleAddress == nint.Zero)
        {
            throw new InvalidOperationException("UI module is not available.");
        }

        var uiModule = (UIModule*)uiModuleAddress;
        Utf8String command = new(inputText);
        try
        {
            uiModule->ProcessChatBoxEntry(&command, nint.Zero, false);
        }
        finally
        {
            command.Dtor();
        }
    }
}
