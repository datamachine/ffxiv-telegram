namespace FFXIVTelegram.Tests.Configuration;

using FFXIVTelegram.Configuration;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class ConfigurationStoreTests
{
    [Fact]
    public void LoadReturnsSavedConfiguration()
    {
        var saved = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
            AuthorizedChatId = 42,
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(saved, out var pluginProxy, out _);
        var store = new ConfigurationStore(plugin);

        var loaded = store.Load();

        Assert.Same(saved, loaded);
    }

    [Fact]
    public void SavePersistsProvidedConfiguration()
    {
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out var pluginProxy, out _);
        var store = new ConfigurationStore(plugin);
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "token",
        };

        store.Save(configuration);

        Assert.Same(configuration, pluginProxy.SavedConfiguration);
    }
}
