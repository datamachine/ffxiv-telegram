namespace FFXIVTelegram.Interop;

using System.Runtime.InteropServices;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;

public sealed unsafe class XivChatGameChatExecutor : IGameChatExecutor
{
    private delegate void ProcessChatBoxDelegate(nint uiModule, nint message, nint unused, byte a4);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 20 48 8B FA 48 8B D9 45 84 C9")]
    private readonly ProcessChatBoxDelegate? processChatBox = null;

    public XivChatGameChatExecutor(IGameInteropProvider gameInteropProvider)
    {
        ArgumentNullException.ThrowIfNull(gameInteropProvider);
        gameInteropProvider.InitializeFromAttributes(this);
    }

    public void Execute(string inputText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputText);

        if (this.processChatBox is null)
        {
            throw new InvalidOperationException("Chat executor signature is not available.");
        }

        var framework = Framework.Instance();
        if (framework is null)
        {
            throw new InvalidOperationException("Game framework is not available.");
        }

        UIModule* uiModule = framework->UIModule;
        if (uiModule is null)
        {
            throw new InvalidOperationException("UI module is not available.");
        }

        using var payload = new ChatPayload(inputText);
        var payloadPtr = Marshal.AllocHGlobal(Marshal.SizeOf<ChatPayload>());
        try
        {
            Marshal.StructureToPtr(payload, payloadPtr, false);
            this.processChatBox((nint)uiModule, payloadPtr, nint.Zero, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(payloadPtr);
        }
    }
}
