using System.Globalization;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: FFXIVTelegram.ReleaseTool repo-json <manifest-path> <stable-tag> <published-at>");
    return 1;
}

if (string.Equals(args[0], "repo-json", StringComparison.Ordinal))
{
    if (args.Length != 4)
    {
        Console.Error.WriteLine("Usage: FFXIVTelegram.ReleaseTool repo-json <manifest-path> <stable-tag> <published-at>");
        return 1;
    }

    var manifestPath = args[1];
    var stableTag = args[2];
    var publishedAtText = args[3];

    if (!DateTimeOffset.TryParse(
            publishedAtText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var publishedAt))
    {
        Console.Error.WriteLine("The published-at value must be a valid ISO-8601 timestamp.");
        return 1;
    }

    var manifestJson = File.ReadAllText(manifestPath);
    var manifest = PluginManifestReader.ReadFromJson(manifestJson);
    var assetUrl = BuildReleaseAssetUrl(manifest.RepoUrl, stableTag, manifest.InternalName);
    var repoJson = RepoJsonWriter.WriteSinglePluginRepoJson(manifest, stableTag, publishedAt, assetUrl);

    Console.Out.WriteLine(repoJson);
    return 0;
}

Console.Error.WriteLine($"Unknown command '{args[0]}'.");
return 1;

static string BuildReleaseAssetUrl(string repoUrl, string stableTag, string internalName)
{
    return $"{repoUrl.TrimEnd('/')}/releases/download/{stableTag}/{internalName}.zip";
}
