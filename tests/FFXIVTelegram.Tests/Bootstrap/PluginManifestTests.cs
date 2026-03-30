namespace FFXIVTelegram.Tests.Bootstrap;

using System.Text.Json;
using Xunit;

public sealed class PluginManifestTests
{
    [Fact]
    public void ManifestIncludesNonEmptyTagsArray()
    {
        var manifestPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/FFXIVTelegram/FFXIVTelegram.json"));
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));

        Assert.True(document.RootElement.TryGetProperty("Tags", out var tagsElement));
        Assert.Equal(JsonValueKind.Array, tagsElement.ValueKind);
        Assert.NotEmpty(tagsElement.EnumerateArray());
    }
}
