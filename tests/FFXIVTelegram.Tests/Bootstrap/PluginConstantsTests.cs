namespace FFXIVTelegram.Tests.Bootstrap;

using FFXIVTelegram;
using Xunit;

public sealed class PluginConstantsTests
{
    [Fact]
    public void UsesApprovedPluginNameAndCommand()
    {
        Assert.Equal("FFXIVTelegram.Tests.Bootstrap", typeof(PluginConstantsTests).Namespace);
        Assert.Equal("FFXIVTelegram", typeof(PluginConstants).Namespace);
        Assert.Equal("FFXIV Telegram", PluginConstants.PluginName);
        Assert.Equal("/ffxivtelegram", PluginConstants.CommandName);
    }
}
