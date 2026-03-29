namespace FFXIVTelegram;

using Dalamud.Plugin;

public sealed class FfxivTelegramPlugin : IDalamudPlugin
{
    public string Name => PluginConstants.PluginName;

    public void Dispose()
    {
    }
}
