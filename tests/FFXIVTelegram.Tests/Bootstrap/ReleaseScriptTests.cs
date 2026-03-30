namespace FFXIVTelegram.Tests.Bootstrap;

using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Xunit;

public sealed class ReleaseScriptTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    private static readonly string ScriptPath = Path.Combine(RepoRoot, "scripts", "release", "build-release.sh");
    private static readonly string BashPath = ResolveRequiredCommandPath("bash");
    private static readonly string[] RequiredScriptCommands =
    [
        "bash",
        "mkdir",
        "mktemp",
        "cp",
        "sed",
        "mv",
        "grep",
        "date",
        "bsdtar",
        "rm",
    ];

    [Fact]
    public void BuildReleaseScriptCreatesVersionedZipAndRepoJson()
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        WriteRequiredPluginFiles(inputDirectory);

        var result = RunReleaseScript(
            CreateScriptEnvironmentPath(tempDirectory, includeZipShim: true),
            "--tag",
            "v0.1.0",
            "--input",
            inputDirectory,
            "--output",
            outputDirectory);
        var zipPath = Path.Combine(outputDirectory, "FFXIVTelegram-0.1.0.zip");
        var repoJsonPath = Path.Combine(outputDirectory, "repo.json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(zipPath), $"Expected '{zipPath}' to exist.{Environment.NewLine}{result}");
        Assert.True(File.Exists(repoJsonPath), $"Expected '{repoJsonPath}' to exist.{Environment.NewLine}{result}");

        using var archive = ZipFile.OpenRead(zipPath);

        Assert.Contains(archive.Entries, entry => entry.FullName == "FFXIVTelegram.dll");
        Assert.Contains(archive.Entries, entry => entry.FullName == "FFXIVTelegram.deps.json");
        Assert.Contains(archive.Entries, entry => entry.FullName == "FFXIVTelegram.json");

        using (var manifestStream = archive.GetEntry("FFXIVTelegram.json")!.Open())
        using (var manifestDocument = JsonDocument.Parse(manifestStream))
        {
            Assert.Equal("0.1.0", manifestDocument.RootElement.GetProperty("AssemblyVersion").GetString());
        }

        using var repoDocument = JsonDocument.Parse(File.ReadAllText(repoJsonPath));
        Assert.Equal(JsonValueKind.Array, repoDocument.RootElement.ValueKind);

        var entries = repoDocument.RootElement.EnumerateArray().ToArray();
        var entry = Assert.Single(entries);

        Assert.Equal("Surye", entry.GetProperty("Author").GetString());
        Assert.Equal("FFXIV Telegram", entry.GetProperty("Name").GetString());
        Assert.Equal("FFXIVTelegram", entry.GetProperty("InternalName").GetString());
        Assert.Equal("Bridges FFXIV chat and Telegram.", entry.GetProperty("Description").GetString());
        Assert.Equal("Bridging Eorzea and Telegram.", entry.GetProperty("Punchline").GetString());
        Assert.Equal("0.1.0", entry.GetProperty("AssemblyVersion").GetString());
        Assert.Equal("https://github.com/datamachine/ffxiv-telegram", entry.GetProperty("RepoUrl").GetString());
        Assert.Equal("any", entry.GetProperty("ApplicableVersion").GetString());
        Assert.Equal(14, entry.GetProperty("DalamudApiLevel").GetInt32());
        Assert.Equal(0, entry.GetProperty("LoadPriority").GetInt32());
        Assert.Equal(JsonValueKind.Number, entry.GetProperty("LastUpdate").ValueKind);
        Assert.True(entry.GetProperty("LastUpdate").GetInt64() > 0);

        var tags = entry.GetProperty("Tags").EnumerateArray().Select(tag => tag.GetString()).ToArray();
        Assert.Equal(new[] { "chat", "telegram", "bridge" }, tags);

        const string expectedDownloadLink = "https://github.com/datamachine/ffxiv-telegram/releases/download/v0.1.0/FFXIVTelegram-0.1.0.zip";

        Assert.Equal(expectedDownloadLink, entry.GetProperty("DownloadLinkInstall").GetString());
        Assert.Equal(expectedDownloadLink, entry.GetProperty("DownloadLinkUpdate").GetString());
        Assert.Equal(expectedDownloadLink, entry.GetProperty("DownloadLinkTesting").GetString());
    }

    [Theory]
    [InlineData("v01.2.3")]
    [InlineData("1.2.3")]
    public void BuildReleaseScriptRejectsInvalidTagShapes(string tag)
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        WriteRequiredPluginFiles(inputDirectory);

        var result = RunReleaseScript("--tag", tag, "--input", inputDirectory, "--output", outputDirectory);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void BuildReleaseScriptFailsWhenDepsJsonIsMissing()
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        File.WriteAllBytes(Path.Combine(inputDirectory, "FFXIVTelegram.dll"), []);
        File.WriteAllText(Path.Combine(inputDirectory, "FFXIVTelegram.json"), CreateManifestJson(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var result = RunReleaseScript("--tag", "v0.1.0", "--input", inputDirectory, "--output", outputDirectory);

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public void BuildReleaseScriptFailsClearlyWhenZipIsUnavailable()
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        WriteRequiredPluginFiles(inputDirectory);

        var result = RunReleaseScript(
            CreateScriptEnvironmentPath(tempDirectory, includeZipShim: false),
            "--tag",
            "v0.1.0",
            "--input",
            inputDirectory,
            "--output",
            outputDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("zip is required", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteRequiredPluginFiles(string inputDirectory)
    {
        Directory.CreateDirectory(inputDirectory);
        File.WriteAllBytes(Path.Combine(inputDirectory, "FFXIVTelegram.dll"), []);
        File.WriteAllText(Path.Combine(inputDirectory, "FFXIVTelegram.deps.json"), "{}", new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(Path.Combine(inputDirectory, "FFXIVTelegram.json"), CreateManifestJson(), new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string CreateManifestJson()
    {
        return """
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
    }

    private static ReleaseScriptResult RunReleaseScript(string pathEnvironment, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = BashPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["PATH"] = pathEnvironment;

        startInfo.ArgumentList.Add(ScriptPath);

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ReleaseScriptResult(process.ExitCode, standardOutput, standardError);
    }

    private static string CreateScriptEnvironmentPath(TempDirectory tempDirectory, bool includeZipShim)
    {
        var toolDirectory = tempDirectory.CreateDirectory(includeZipShim ? "tools-with-zip" : "tools-without-zip");

        foreach (var command in RequiredScriptCommands)
        {
            File.CreateSymbolicLink(
                Path.Combine(toolDirectory, command),
                ResolveRequiredCommandPath(command));
        }

        if (includeZipShim)
        {
            var zipShimPath = Path.Combine(toolDirectory, "zip");
            File.WriteAllText(
                zipShimPath,
                """
                #!/usr/bin/env bash
                set -euo pipefail

                if [[ "${1:-}" == "-q" ]]; then
                  shift
                fi

                archive_path="$1"
                shift

                exec bsdtar -a -cf "$archive_path" "$@"
                """,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    zipShimPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }
        }

        return toolDirectory;
    }

    private static string ResolveRequiredCommandPath(string command)
    {
        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pathEntry in pathEntries)
        {
            var candidate = Path.Combine(pathEntry, command);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException($"Could not resolve required command '{command}' from PATH.");
    }

    private sealed record ReleaseScriptResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public override string ToString()
        {
            return $"ExitCode: {ExitCode}{Environment.NewLine}StdOut:{Environment.NewLine}{StandardOutput}{Environment.NewLine}StdErr:{Environment.NewLine}{StandardError}";
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string rootPath = Path.Combine(Path.GetTempPath(), $"ffxivtelegram-release-tests-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(rootPath);
        }

        public string CreateDirectory(string name)
        {
            var path = Path.Combine(rootPath, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
