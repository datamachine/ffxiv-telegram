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
    private static readonly string? ZipPath = TryResolveCommandPath("zip");
    private static readonly string? BsdtarPath = TryResolveCommandPath("bsdtar");
    private static readonly string? Python3Path = TryResolveCommandPath("python3");
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
        "rm",
    ];

    [Fact]
    public void BuildReleaseScriptCreatesVersionedZipAndRepoJsonWhenZipIsAvailable()
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        WriteRequiredPluginFiles(inputDirectory);

        var result = RunReleaseScriptWithPath(
            CreateScriptEnvironmentPath(tempDirectory, includeZipShim: true),
            "--tag",
            "v0.1.0",
            "--input",
            inputDirectory,
            "--output",
            outputDirectory);

        AssertArchiveAndRepoJson(outputDirectory, result);
    }

    [Fact]
    public void BuildReleaseScriptFallsBackToBsdtarWhenZipIsUnavailable()
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        WriteRequiredPluginFiles(inputDirectory);

        var result = RunReleaseScriptWithPath(
            CreateScriptEnvironmentPath(tempDirectory, includeZipShim: false, includeBsdtar: true),
            "--tag",
            "v0.1.0",
            "--input",
            inputDirectory,
            "--output",
            outputDirectory);

        AssertArchiveAndRepoJson(outputDirectory, result);
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
    public void BuildReleaseScriptFailsClearlyWhenArchiveToolsAreUnavailable()
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        WriteRequiredPluginFiles(inputDirectory);

        var result = RunReleaseScriptWithPath(
            CreateScriptEnvironmentPath(tempDirectory, includeZipShim: false, includeBsdtar: false),
            "--tag",
            "v0.1.0",
            "--input",
            inputDirectory,
            "--output",
            outputDirectory);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("zip or bsdtar is required", result.StandardError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildReleaseScriptAcceptsRelativeInputAndOutputPaths()
    {
        using var tempDirectory = new TempDirectory();
        var inputDirectory = tempDirectory.CreateDirectory("input");
        var outputDirectory = tempDirectory.CreateDirectory("output");

        WriteRequiredPluginFiles(inputDirectory);

        var result = RunReleaseScriptWithPathAndWorkingDirectory(
            CreateScriptEnvironmentPath(tempDirectory, includeZipShim: false, includeBsdtar: true),
            tempDirectory.RootPath,
            "--tag",
            "v0.1.0",
            "--input",
            "input",
            "--output",
            "output");

        AssertArchiveAndRepoJson(outputDirectory, result);
    }

    private static void AssertArchiveAndRepoJson(string outputDirectory, ReleaseScriptResult result)
    {
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

    private static ReleaseScriptResult RunReleaseScript(params string[] arguments)
    {
        return RunReleaseScriptCore(Environment.GetEnvironmentVariable("PATH") ?? string.Empty, RepoRoot, arguments);
    }

    private static ReleaseScriptResult RunReleaseScriptWithPath(string pathEnvironment, params string[] arguments)
    {
        return RunReleaseScriptCore(pathEnvironment, RepoRoot, arguments);
    }

    private static ReleaseScriptResult RunReleaseScriptWithPathAndWorkingDirectory(string pathEnvironment, string workingDirectory, params string[] arguments)
    {
        return RunReleaseScriptCore(pathEnvironment, workingDirectory, arguments);
    }

    private static ReleaseScriptResult RunReleaseScriptCore(string pathEnvironment, string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = BashPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
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

    private static string CreateScriptEnvironmentPath(TempDirectory tempDirectory, bool includeZipShim, bool includeBsdtar = true)
    {
        var toolDirectory = tempDirectory.CreateDirectory(includeZipShim ? "tools-with-zip" : "tools-without-zip");

        foreach (var command in RequiredScriptCommands)
        {
            File.CreateSymbolicLink(
                Path.Combine(toolDirectory, command),
                ResolveRequiredCommandPath(command));
        }

        if (includeBsdtar)
        {
            WriteBsdtarShim(toolDirectory);
        }

        if (includeZipShim)
        {
            WriteZipShim(toolDirectory);
        }

        return toolDirectory;
    }

    private static void WriteZipShim(string toolDirectory)
    {
        var zipShimPath = Path.Combine(toolDirectory, "zip");

        if (ZipPath is not null)
        {
            File.CreateSymbolicLink(zipShimPath, ZipPath);
            return;
        }

        if (BsdtarPath is not null)
        {
            WriteExecutableScript(
                zipShimPath,
                $$"""
                #!/usr/bin/env bash
                set -euo pipefail

                if [[ "${1:-}" == "-q" ]]; then
                  shift
                fi

                archive_path="$1"
                shift

                exec "{{BsdtarPath}}" -a -cf "$archive_path" "$@"
                """);
            return;
        }

        if (Python3Path is not null)
        {
            WriteExecutableScript(
                zipShimPath,
                $$"""
                #!/usr/bin/env bash
                set -euo pipefail

                if [[ "${1:-}" == "-q" ]]; then
                  shift
                fi

                archive_path="$1"
                shift

                exec "{{Python3Path}}" - "$archive_path" "$@" <<'PY'
                import sys
                import zipfile

                archive_path = sys.argv[1]
                files = sys.argv[2:]

                with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
                    for file_name in files:
                        archive.write(file_name, arcname=file_name)
                PY
                """);
            return;
        }

        throw new InvalidOperationException("Could not create a zip shim because neither zip, bsdtar, nor python3 is available.");
    }

    private static void WriteBsdtarShim(string toolDirectory)
    {
        var bsdtarShimPath = Path.Combine(toolDirectory, "bsdtar");

        if (BsdtarPath is not null)
        {
            File.CreateSymbolicLink(bsdtarShimPath, BsdtarPath);
            return;
        }

        if (ZipPath is not null)
        {
            WriteExecutableScript(
                bsdtarShimPath,
                $$"""
                #!/usr/bin/env bash
                set -euo pipefail

                [[ "${1:-}" == "-a" ]] || exit 2
                [[ "${2:-}" == "-cf" ]] || exit 2

                archive_path="$3"
                shift 3

                exec "{{ZipPath}}" -q "$archive_path" "$@"
                """);
            return;
        }

        if (Python3Path is not null)
        {
            WriteExecutableScript(
                bsdtarShimPath,
                $$"""
                #!/usr/bin/env bash
                set -euo pipefail

                [[ "${1:-}" == "-a" ]] || exit 2
                [[ "${2:-}" == "-cf" ]] || exit 2

                archive_path="$3"
                shift 3

                exec "{{Python3Path}}" - "$archive_path" "$@" <<'PY'
                import sys
                import zipfile

                archive_path = sys.argv[1]
                files = sys.argv[2:]

                with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
                    for file_name in files:
                        archive.write(file_name, arcname=file_name)
                PY
                """);
            return;
        }

        throw new InvalidOperationException("Could not create a bsdtar shim because neither bsdtar, zip, nor python3 is available.");
    }

    private static void WriteExecutableScript(string path, string contents)
    {
        File.WriteAllText(path, contents, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private static string ResolveRequiredCommandPath(string command)
    {
        return TryResolveCommandPath(command)
            ?? throw new InvalidOperationException($"Could not resolve required command '{command}' from PATH.");
    }

    private static string? TryResolveCommandPath(string command)
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

        return null;
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

        public string RootPath => rootPath;

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
