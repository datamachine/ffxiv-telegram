namespace FFXIVTelegram.Tests.UI;

using Dalamud.Plugin;
using FFXIVTelegram.Configuration;
using FFXIVTelegram.UI;
using FFXIVTelegram.Tests.TestDoubles;
using Xunit;

public sealed class ConfigWindowTests
{
    [Fact]
    public void FinalizingTelegramTokenEditTrimsAndPersistsBuffer()
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "old-token",
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out var pluginProxy, out _);
        var store = new ConfigurationStore(plugin);
        var window = new ConfigWindow(configuration, store);

        window.UpdateTelegramBotTokenBuffer("  new-token  ");
        window.FinalizeTelegramBotTokenEdit();

        Assert.Equal("new-token", configuration.TelegramBotToken);
        Assert.Same(configuration, pluginProxy.SavedConfiguration);
    }

    [Fact]
    public void UpdatingTelegramTokenBufferDoesNotPersistYet()
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "old-token",
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out var pluginProxy, out _);
        var store = new ConfigurationStore(plugin);
        var window = new ConfigWindow(configuration, store);

        window.UpdateTelegramBotTokenBuffer("  partial-token  ");

        Assert.Equal("old-token", configuration.TelegramBotToken);
        Assert.Null(pluginProxy.SavedConfiguration);
    }

    [Fact]
    public void FinalizingTelegramTokenEditClearsAuthorizedChat()
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "old-token",
            AuthorizedChatId = 42,
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out var pluginProxy, out _);
        var store = new ConfigurationStore(plugin);
        var window = new ConfigWindow(configuration, store);

        window.UpdateTelegramBotTokenBuffer("new-token");
        window.FinalizeTelegramBotTokenEdit();

        Assert.Equal("new-token", configuration.TelegramBotToken);
        Assert.Null(configuration.AuthorizedChatId);
        Assert.Same(configuration, pluginProxy.SavedConfiguration);
    }

    [Fact]
    public void FinalizingTelegramTokenEditRollsBackWhenSaveFails()
    {
        var configuration = new FfxivTelegramConfiguration
        {
            TelegramBotToken = "old-token",
            AuthorizedChatId = 42,
        };
        var plugin = DalamudPluginInterfaceTestDouble.Create(null, out var pluginProxy, out _);
        pluginProxy.ThrowOnSave = true;
        var store = new ConfigurationStore(plugin);
        var window = new ConfigWindow(configuration, store);

        window.UpdateTelegramBotTokenBuffer("new-token");

        Assert.Throws<InvalidOperationException>(() => window.FinalizeTelegramBotTokenEdit());
        Assert.Equal("old-token", configuration.TelegramBotToken);
        Assert.Equal(42, configuration.AuthorizedChatId);
        Assert.Null(pluginProxy.SavedConfiguration);
    }
}
