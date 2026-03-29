namespace FFXIVTelegram.Configuration;

using Dalamud.Plugin;

public sealed class ConfigurationStore
{
    private readonly IDalamudPluginInterface pluginInterface;

    public ConfigurationStore(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public FfxivTelegramConfiguration Load()
    {
        return this.pluginInterface.GetPluginConfig() as FfxivTelegramConfiguration ?? new FfxivTelegramConfiguration();
    }

    public void Save(FfxivTelegramConfiguration configuration)
    {
        this.pluginInterface.SavePluginConfig(configuration);
    }
}
