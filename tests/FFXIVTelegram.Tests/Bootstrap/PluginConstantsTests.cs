using Xunit;

public sealed class PluginConstantsTests
{
    [Fact]
    public void UsesApprovedPluginNameAndCommand()
    {
        Assert.Equal("FFXIV Telegram", PluginConstants.PluginName);
        Assert.Equal("/ffxivtelegram", PluginConstants.CommandName);
    }
}
