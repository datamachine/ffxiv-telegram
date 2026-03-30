namespace FFXIVTelegram.Tests.Bootstrap;

using System.Text.Json;
using Xunit;

public sealed class PluginManifestTests
{
    [Fact]
    public void ManifestIncludesRequiredPublicDistributionFields()
    {
        var manifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/FFXIVTelegram/FFXIVTelegram.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));

        Assert.Equal("vcastellano", document.RootElement.GetProperty("Author").GetString());
        Assert.Equal("FFXIV Telegram", document.RootElement.GetProperty("Name").GetString());
        Assert.Equal("Bridges FFXIV chat and Telegram.", document.RootElement.GetProperty("Description").GetString());
        Assert.Equal("Bridging Eorzea and Telegram.", document.RootElement.GetProperty("Punchline").GetString());
        Assert.Equal("FFXIVTelegram", document.RootElement.GetProperty("InternalName").GetString());
        Assert.Equal("any", document.RootElement.GetProperty("ApplicableVersion").GetString());
        Assert.Equal(14, document.RootElement.GetProperty("DalamudApiLevel").GetInt32());
        Assert.Equal(0, document.RootElement.GetProperty("LoadPriority").GetInt32());
        Assert.Equal("https://github.com/datamachine/ffxiv-telegram", document.RootElement.GetProperty("RepoUrl").GetString());

        var tagsElement = document.RootElement.GetProperty("Tags");
        Assert.Equal(JsonValueKind.Array, tagsElement.ValueKind);
        Assert.NotEmpty(tagsElement.EnumerateArray());
    }
}
