namespace FFXIVTelegram.Tests.Bootstrap;

using Xunit;

public sealed class ReleaseVersionParserTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3.0")]
    [InlineData("v10.20.30", "10.20.30.0")]
    public void ParseStableTagReturnsAssemblyVersion(string tag, string expectedAssemblyVersion)
    {
        var actualAssemblyVersion = ReleaseVersionParser.ParseStableTag(tag);

        Assert.Equal(expectedAssemblyVersion, actualAssemblyVersion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1.2.3")]
    [InlineData("v1.2")]
    [InlineData("v1.2.3.4")]
    [InlineData("v1.2.x")]
    [InlineData("v1.2.3-beta.1")]
    [InlineData("release-1.2.3")]
    public void ParseStableTagRejectsInvalidTagFormats(string tag)
    {
        Assert.Throws<ArgumentException>(() => ReleaseVersionParser.ParseStableTag(tag));
    }
}
