namespace FFXIVTelegram.Tests.Bootstrap;

using System.IO;
using System.Text.Json;
using Xunit;

public sealed class PluginPackagingTests
{
    [Fact]
    public void ReleaseBuildScriptExistsAtExpectedPath()
    {
        var scriptPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../scripts/release/build-release.sh"));

        Assert.True(File.Exists(scriptPath), $"Expected release script at '{scriptPath}'.");
    }

    [Fact]
    public void SolutionDoesNotReferenceReleaseToolProject()
    {
        var solutionPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../FFXIVTelegram.sln"));
        var solutionText = File.ReadAllText(solutionPath);

        Assert.DoesNotContain("FFXIVTelegram.ReleaseTool", solutionText);
    }

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
