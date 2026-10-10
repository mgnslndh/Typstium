using Cake.Common.Tools.DotNet.MSBuild;

public static partial class Program
{
    // docs/specs/runtime-packages.md §8. Not part of Default: they download about 120 MB.
    static void RegisterRuntimeTasks()
    {
        Task("UpdateTypstRuntimes")
            .Does(() =>
            {
                var version = Argument<string>("typst-version");
                var digests = GitHubReleaseDigests.Read(Context, TypstRuntimes.Repository, "v" + version);
                var assets = new Dictionary<string, PinnedAsset>(StringComparer.Ordinal);
                foreach (var rid in TypstRuntimes.RustTargets.Keys)
                {
                    var name = TypstRuntimes.AssetName(rid);
                    assets[rid] = digests.TryGetValue(name, out var sha256)
                        ? new PinnedAsset(name, sha256)
                        : throw new CakeException($"Typst {version} has no {name} with a SHA-256 digest; {rid} can't be packed.");
                }

                System.IO.File.WriteAllText(TypstRuntimes.LockFile, new PinnedAssets(version, assets).ToJson());
                Information("Pinned Typst {0} in {1}. Review the diff before committing.", version, TypstRuntimes.LockFile);
            });

        Task("DownloadTypstRuntimes")
            .Does(() =>
            {
                var pinned = PinnedAssets.Parse(System.IO.File.ReadAllText(TypstRuntimes.LockFile));
                var missing = TypstRuntimes.RustTargets.Keys.Except(pinned.Assets.Keys).ToArray();
                if (missing.Length > 0)
                {
                    throw new CakeException($"{TypstRuntimes.LockFile} doesn't pin {string.Join(", ", missing)}. Run --target=UpdateTypstRuntimes.");
                }

                using var downloader = VerifiedDownloader.CreateDefault();
                foreach (var (rid, asset) in pinned.Assets)
                {
                    var archive = $"artifacts/cache/typst/{pinned.Version}/{asset.Name}";
                    downloader.DownloadAsync(TypstRuntimes.AssetUrl(pinned.Version, asset.Name), archive, asset.Sha256).GetAwaiter().GetResult();
                    var folder = RuntimeFolder(pinned.Version, rid);
                    var executable = TypstRuntimes.ExecutableName(rid);
                    ArchiveFiles.Extract(archive, new Dictionary<string, string>
                    {
                        [executable] = System.IO.Path.Combine(folder, executable),
                        ["LICENSE"] = System.IO.Path.Combine(folder, "LICENSE"),
                    });
                    Information("{0}: {1} verified and extracted.", rid, asset.Name);
                }

                CheckHostBinary(pinned.Version);
            });

        Task("PackTypstRuntimes")
            .IsDependentOn("DownloadTypstRuntimes")
            .Does(() =>
            {
                var version = PinnedAssets.Parse(System.IO.File.ReadAllText(TypstRuntimes.LockFile)).Version;
                var packages = MakeAbsolute(Directory("artifacts/packages")).FullPath;
                // Restoring Typstium.Runtime.All pulls the fresh packages into a cache of their own, not the user's.
                var environment = new Dictionary<string, string> { ["NUGET_PACKAGES"] = MakeAbsolute(Directory("artifacts/cache/nuget")).FullPath };
                foreach (var rid in TypstRuntimes.RustTargets.Keys)
                {
                    DotNetPack("src/Typstium.Runtime/Typstium.Runtime.csproj", new DotNetPackSettings
                    {
                        Configuration = "Release",
                        OutputDirectory = packages,
                        EnvironmentVariables = environment,
                        MSBuildSettings = new DotNetMSBuildSettings()
                            .WithProperty("TypstiumRuntimeIdentifier", rid)
                            .WithProperty("TypstiumRuntimeSource", MakeAbsolute(Directory(RuntimeFolder(version, rid))).FullPath)
                            .WithProperty("TypstVersion", version),
                    });
                }

                DotNetPack("src/Typstium.Runtime.All/Typstium.Runtime.All.csproj", new DotNetPackSettings
                {
                    Configuration = "Release",
                    OutputDirectory = packages,
                    EnvironmentVariables = environment,
                    MSBuildSettings = new DotNetMSBuildSettings()
                        .WithProperty("TypstVersion", version)
                        .WithProperty("RestoreAdditionalProjectSources", packages),
                });
            });

        // The targets tests with fixture packages (stub binaries); minutes, not seconds.
        Task("RuntimeTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Typstium.Runtime.Tests", new DotNetTestSettings { PathType = DotNetTestPathType.Project, Configuration = parameters.Configuration }));

        // Against the real packages in artifacts/packages (run PackTypstRuntimes first, or download CI's artifact there).
        Task("RuntimeEndToEndTests")
            .Does<BuildParameters>(parameters =>
                DotNetTest("tests/Typstium.Runtime.EndToEndTests", new DotNetTestSettings
                {
                    PathType = DotNetTestPathType.Project,
                    Configuration = parameters.Configuration,
                    EnvironmentVariables = { ["TYPSTIUM_RUNTIME_PACKAGES"] = MakeAbsolute(Directory("artifacts/packages")).FullPath },
                }));
    }

    static string RuntimeFolder(string version, string rid) => $"artifacts/runtimes/{version}/{rid}";

    // A lock file that names the wrong release would pack the wrong binary: run this machine's and compare versions.
    static void CheckHostBinary(string version)
    {
        var rid = TypstRuntimes.HostRuntimeIdentifier;
        if (!TypstRuntimes.RustTargets.ContainsKey(rid))
        {
            Warning("No Typst runtime for {0}; skipping the version check.", rid);
            return;
        }

        var executable = System.IO.Path.GetFullPath(System.IO.Path.Combine(RuntimeFolder(version, rid), TypstRuntimes.ExecutableName(rid)));
        if (!OperatingSystem.IsWindows())
        {
            System.IO.File.SetUnixFileMode(executable, System.IO.File.GetUnixFileMode(executable) | UnixFileMode.UserExecute);
        }

        StartProcess(executable, new ProcessSettings { Arguments = "--version", RedirectStandardOutput = true }, out var output);
        var line = output.FirstOrDefault() ?? "";
        if (!line.StartsWith($"typst {version}", StringComparison.Ordinal))
        {
            throw new CakeException($"{executable} --version printed '{line}', but {TypstRuntimes.LockFile} pins {version}.");
        }
    }
}
