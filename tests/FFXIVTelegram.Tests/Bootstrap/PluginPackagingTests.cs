namespace FFXIVTelegram.Tests.Bootstrap;

using System.IO;
using System.Text.Json;
using Xunit;

public sealed class PluginPackagingTests
{
    [Fact]
    public void PluginDoesNotBundleDalamudBindingsImGui()
    {
        var outputDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/FFXIVTelegram/bin/Debug"));
        var depsPath = Path.Combine(outputDirectory, "FFXIVTelegram.deps.json");
        var bundledBindingsPath = Path.Combine(outputDirectory, "Dalamud.Bindings.ImGui.dll");

        using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
        var libraries = document.RootElement.GetProperty("libraries");

        Assert.False(libraries.TryGetProperty("Dalamud.Bindings.ImGui/1.0.0.0", out _));
        Assert.False(File.Exists(bundledBindingsPath));
    }
}
