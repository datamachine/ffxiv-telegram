public sealed class PluginManifestModel
{
    public string Author { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string InternalName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Punchline { get; init; } = string.Empty;
    public string ApplicableVersion { get; init; } = string.Empty;
    public string RepoUrl { get; init; } = string.Empty;
    public int DalamudApiLevel { get; init; }
    public int LoadPriority { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}
