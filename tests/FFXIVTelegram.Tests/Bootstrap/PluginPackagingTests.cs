namespace FFXIVTelegram.Tests.Bootstrap;

using System.IO;
using System.Text.Json;
using Xunit;

public sealed class PluginPackagingTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void ReleaseBuildScriptExistsAtExpectedPath()
    {
        var scriptPath = Path.Combine(RepoRoot, "scripts", "release", "build-release.sh");

        Assert.True(File.Exists(scriptPath), $"Expected release script at '{scriptPath}'.");
    }

    [Fact]
    public void SolutionDoesNotReferenceReleaseToolProject()
    {
        var solutionPath = Path.Combine(RepoRoot, "FFXIVTelegram.sln");
        var solutionText = File.ReadAllText(solutionPath);

        Assert.DoesNotContain("FFXIVTelegram.ReleaseTool", solutionText);
    }

    [Fact]
    public void PluginDoesNotBundleDalamudBindingsImGui()
    {
        var outputDirectory = Path.Combine(RepoRoot, "src", "FFXIVTelegram", "bin", ResolveCurrentBuildConfiguration());
        var depsPath = Path.Combine(outputDirectory, "FFXIVTelegram.deps.json");
        var bundledBindingsPath = Path.Combine(outputDirectory, "Dalamud.Bindings.ImGui.dll");

        Assert.True(File.Exists(depsPath), $"Expected plugin deps file at '{depsPath}'.");

        using var document = JsonDocument.Parse(File.ReadAllText(depsPath));
        var libraries = document.RootElement.GetProperty("libraries");

        Assert.False(libraries.TryGetProperty("Dalamud.Bindings.ImGui/1.0.0.0", out _));
        Assert.False(File.Exists(bundledBindingsPath));
    }

    private static string ResolveCurrentBuildConfiguration()
    {
        var pathSegments = AppContext.BaseDirectory
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var binIndex = Array.LastIndexOf(pathSegments, "bin");

        Assert.True(binIndex >= 0 && binIndex + 1 < pathSegments.Length, $"Failed to resolve the current test build configuration from '{AppContext.BaseDirectory}'.");

        return pathSegments[binIndex + 1];
    }
}
