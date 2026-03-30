using System.Globalization;

if (args.Length == 0)
{
    Console.Error.WriteLine(RepoJsonCommandArguments.UsageText);
    return 1;
}

if (string.Equals(args[0], "repo-json", StringComparison.Ordinal))
{
    return RunRepoJsonCommand(args[1..]);
}

Console.Error.WriteLine($"Unknown command '{args[0]}'.");
return 1;

static int RunRepoJsonCommand(string[] args)
{
    if (!RepoJsonCommandArguments.TryParse(args, out var commandArguments, out var errorMessage))
    {
        Console.Error.WriteLine(errorMessage);
        return 1;
    }

    try
    {
        var manifestJson = File.ReadAllText(commandArguments.ManifestPath);
        var manifest = PluginManifestReader.ReadFromJson(manifestJson);

        ReleaseVersionParser.ParseStableTag(commandArguments.StableTag);

        var repoJson = RepoJsonWriter.WriteSinglePluginRepoJson(
            manifest,
            commandArguments.StableTag,
            DateTimeOffset.FromUnixTimeSeconds(commandArguments.PublishedUnixSeconds),
            commandArguments.DownloadUrl);

        var outputDirectory = Path.GetDirectoryName(commandArguments.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        File.WriteAllText(commandArguments.OutputPath, repoJson);
        return 0;
    }
    catch (ArgumentException exception)
    {
        Console.Error.WriteLine(exception.Message);
        return 1;
    }
}

internal sealed record RepoJsonCommandArguments(
    string ManifestPath,
    string StableTag,
    string DownloadUrl,
    string OutputPath,
    long PublishedUnixSeconds)
{
    public const string UsageText =
        "Usage: FFXIVTelegram.ReleaseTool repo-json --manifest <manifest-path> --tag <stable-tag> --download-url <asset-url> --output <repo-json-path> --published-unix-seconds <unix-seconds>";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out RepoJsonCommandArguments commandArguments,
        out string errorMessage)
    {
        commandArguments = null!;
        errorMessage = UsageText;

        if (args.Count != 10)
        {
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Count; index += 2)
        {
            var option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal))
            {
                errorMessage = $"Unexpected argument '{option}'.";
                return false;
            }

            if (index + 1 >= args.Count)
            {
                errorMessage = $"Missing value for '{option}'.";
                return false;
            }

            if (!values.TryAdd(option, args[index + 1]))
            {
                errorMessage = $"Duplicate option '{option}'.";
                return false;
            }
        }

        if (!TryGetRequiredValue(values, "--manifest", out var manifestPath, out errorMessage)
            || !TryGetRequiredValue(values, "--tag", out var stableTag, out errorMessage)
            || !TryGetRequiredValue(values, "--download-url", out var downloadUrl, out errorMessage)
            || !TryGetRequiredValue(values, "--output", out var outputPath, out errorMessage)
            || !TryGetRequiredValue(values, "--published-unix-seconds", out var publishedUnixSecondsText, out errorMessage))
        {
            return false;
        }

        if (!long.TryParse(publishedUnixSecondsText, NumberStyles.None, CultureInfo.InvariantCulture, out var publishedUnixSeconds))
        {
            errorMessage = "The --published-unix-seconds value must be a valid unix timestamp in seconds.";
            return false;
        }

        commandArguments = new RepoJsonCommandArguments(
            manifestPath,
            stableTag,
            downloadUrl,
            outputPath,
            publishedUnixSeconds);

        return true;
    }

    private static bool TryGetRequiredValue(
        IReadOnlyDictionary<string, string> values,
        string optionName,
        out string value,
        out string errorMessage)
    {
        errorMessage = UsageText;
        value = string.Empty;

        if (!values.TryGetValue(optionName, out var candidateValue) || string.IsNullOrWhiteSpace(candidateValue))
        {
            errorMessage = $"Missing required option '{optionName}'.";
            return false;
        }

        value = candidateValue;
        return true;
    }
}
