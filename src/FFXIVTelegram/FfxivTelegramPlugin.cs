namespace FFXIVTelegram;

using Dalamud.Plugin;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.UI;

public sealed class FfxivTelegramPlugin : IDalamudPlugin
{
    private readonly UiController uiController;

    public string Name => PluginConstants.PluginName;

    public FfxivTelegramPlugin(IDalamudPluginInterface pluginInterface)
    {
        var configurationStore = new ConfigurationStore(pluginInterface);
        var configuration = configurationStore.Load();
        var configWindow = new ConfigWindow(configuration, configurationStore);
        this.uiController = new UiController(pluginInterface, configWindow);
    }

    public void Dispose()
    {
        this.uiController.Dispose();
    }
}
