using System.Text.Json;

public static class RepoJsonWriter
{
    public static string WriteSinglePluginRepoJson(
        PluginManifestModel manifest,
        string stableTag,
        DateTimeOffset publishedAt,
        string assetUrl)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetUrl);

        var assemblyVersion = ReleaseVersionParser.ParseStableTag(stableTag);

        var plugins = new[]
        {
            new
            {
                manifest.Author,
                manifest.Name,
                manifest.InternalName,
                manifest.Description,
                manifest.Punchline,
                AssemblyVersion = assemblyVersion,
                manifest.RepoUrl,
                manifest.ApplicableVersion,
                manifest.DalamudApiLevel,
                manifest.LoadPriority,
                LastUpdate = publishedAt.ToUnixTimeSeconds(),
                DownloadLinkInstall = assetUrl,
                DownloadLinkUpdate = assetUrl,
                DownloadLinkTesting = assetUrl,
                manifest.Tags,
            },
        };

        return JsonSerializer.Serialize(
            plugins,
            new JsonSerializerOptions
            {
                WriteIndented = true,
            });
    }
}
