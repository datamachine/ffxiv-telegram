namespace FFXIVTelegram.Tests.Bootstrap;

using System.Text.Json;
using Xunit;

public sealed class RepoJsonWriterTests
{
    private const string StableTag = "v0.1.0";
    private const string ExpectedAssemblyVersion = "0.1.0";
    private const string ExpectedAssetUrl = "https://github.com/datamachine/ffxiv-telegram/releases/download/v0.1.0/FFXIVTelegram-0.1.0.zip";
    private const long PublishedUnixSeconds = 1_711_756_800;
    private static readonly DateTimeOffset PublishedAt = DateTimeOffset.FromUnixTimeSeconds(PublishedUnixSeconds);

    [Fact]
    public void WriteSinglePluginRepoJsonEmitsRequiredStableFields()
    {
        var manifestJson = """
            {
              "Author": "Surye",
              "Name": "FFXIV Telegram",
              "InternalName": "FFXIVTelegram",
              "AssemblyVersion": "0.0.1.0",
              "Description": "Bridges FFXIV chat and Telegram.",
              "Tags": [
                "chat",
                "telegram",
                "bridge"
              ],
              "ApplicableVersion": "any",
              "RepoUrl": "https://github.com/datamachine/ffxiv-telegram",
              "DalamudApiLevel": 14,
              "LoadPriority": 0,
              "Punchline": "Bridging Eorzea and Telegram."
            }
            """;

        var manifest = PluginManifestReader.ReadFromJson(manifestJson);
        var repoJson = RepoJsonWriter.WriteSinglePluginRepoJson(manifest, StableTag, PublishedAt, ExpectedAssetUrl);

        using var document = JsonDocument.Parse(repoJson);
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);

        var plugins = document.RootElement.EnumerateArray().ToArray();
        Assert.Single(plugins);

        var plugin = plugins[0];
        Assert.Equal("Surye", plugin.GetProperty("Author").GetString());
        Assert.Equal("FFXIV Telegram", plugin.GetProperty("Name").GetString());
        Assert.Equal("FFXIVTelegram", plugin.GetProperty("InternalName").GetString());
        Assert.Equal("Bridges FFXIV chat and Telegram.", plugin.GetProperty("Description").GetString());
        Assert.Equal("Bridging Eorzea and Telegram.", plugin.GetProperty("Punchline").GetString());
        Assert.Equal(ExpectedAssemblyVersion, plugin.GetProperty("AssemblyVersion").GetString());
        Assert.Equal("https://github.com/datamachine/ffxiv-telegram", plugin.GetProperty("RepoUrl").GetString());
        Assert.Equal("any", plugin.GetProperty("ApplicableVersion").GetString());
        Assert.Equal(14, plugin.GetProperty("DalamudApiLevel").GetInt32());
        Assert.Equal(0, plugin.GetProperty("LoadPriority").GetInt32());
        Assert.Equal(PublishedUnixSeconds, plugin.GetProperty("LastUpdate").GetInt64());
        Assert.Equal(ExpectedAssetUrl, plugin.GetProperty("DownloadLinkInstall").GetString());
        Assert.Equal(ExpectedAssetUrl, plugin.GetProperty("DownloadLinkUpdate").GetString());
        Assert.Equal(ExpectedAssetUrl, plugin.GetProperty("DownloadLinkTesting").GetString());

        var tags = plugin.GetProperty("Tags").EnumerateArray().Select(static item => item.GetString()).ToArray();
        Assert.Equal(new[] { "chat", "telegram", "bridge" }, tags);
    }

    [Theory]
    [InlineData("""{ "Author": "Surye", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "DalamudApiLevel": 14, "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "LoadPriority": 0, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "Tags": ["chat"] }""")]
    [InlineData("""{ "Author": "Surye", "InternalName": "FFXIVTelegram", "Name": "FFXIV Telegram", "Description": "Bridges FFXIV chat and Telegram.", "Punchline": "Bridging Eorzea and Telegram.", "ApplicableVersion": "any", "RepoUrl": "https://github.com/datamachine/ffxiv-telegram", "DalamudApiLevel": 14, "LoadPriority": 0 }""")]
    public void ReadFromJsonRejectsMissingRequiredFields(string manifestJson)
    {
        Assert.Throws<ArgumentException>(() => PluginManifestReader.ReadFromJson(manifestJson));
    }

    [Fact]
    public void RepoJsonCommandAcceptsNamedArgumentsAndWritesRequestedOutputFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ffxivtelegram-repojson-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var manifestPath = Path.Combine(tempRoot, "FFXIVTelegram.json");
            var outputPath = Path.Combine(tempRoot, "public", "repo.json");

            File.WriteAllText(
                manifestPath,
                """
                {
                  "Author": "Surye",
                  "Name": "FFXIV Telegram",
                  "InternalName": "FFXIVTelegram",
                  "AssemblyVersion": "0.0.1.0",
                  "Description": "Bridges FFXIV chat and Telegram.",
                  "Tags": [
                    "chat",
                    "telegram",
                    "bridge"
                  ],
                  "ApplicableVersion": "any",
                  "RepoUrl": "https://github.com/datamachine/ffxiv-telegram",
                  "DalamudApiLevel": 14,
                  "LoadPriority": 0,
                  "Punchline": "Bridging Eorzea and Telegram."
                }
                """);

            var exitCode = InvokeReleaseTool(
                "repo-json",
                "--manifest",
                manifestPath,
                "--tag",
                StableTag,
                "--download-url",
                ExpectedAssetUrl,
                "--output",
                outputPath,
                "--published-unix-seconds",
                PublishedUnixSeconds.ToString());

            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(outputPath));

            using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
            var plugin = Assert.Single(document.RootElement.EnumerateArray());
            Assert.Equal("Surye", plugin.GetProperty("Author").GetString());
            Assert.Equal(ExpectedAssemblyVersion, plugin.GetProperty("AssemblyVersion").GetString());
            Assert.Equal(PublishedUnixSeconds, plugin.GetProperty("LastUpdate").GetInt64());
            Assert.Equal(ExpectedAssetUrl, plugin.GetProperty("DownloadLinkInstall").GetString());
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static int InvokeReleaseTool(params string[] args)
    {
        var entryPoint = typeof(ReleaseVersionParser).Assembly.EntryPoint;
        Assert.NotNull(entryPoint);

        var invocationResult = entryPoint!.Invoke(null, new object?[] { args });
        return invocationResult switch
        {
            int exitCode => exitCode,
            Task<int> exitCodeTask => exitCodeTask.GetAwaiter().GetResult(),
            null => 0,
            _ => throw new InvalidOperationException($"Unexpected entry point return type '{invocationResult.GetType().FullName}'."),
        };
    }
}
