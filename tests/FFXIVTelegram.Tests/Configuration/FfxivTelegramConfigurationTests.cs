namespace FFXIVTelegram.Tests.Configuration;

using FFXIVTelegram.Configuration;
using Xunit;

public sealed class FfxivTelegramConfigurationTests
{
    [Fact]
    public void DefaultsEnableOnlyApprovedChannels()
    {
        var config = new FfxivTelegramConfiguration();

        Assert.True(config.EnableTellForwarding);
        Assert.True(config.EnablePartyForwarding);
        Assert.True(config.EnableFreeCompanyForwarding);
        Assert.Null(config.AuthorizedChatId);
    }
}
