using Typstium.Runtime.Testing;

namespace Typstium.Runtime.Tests;

public class PlatformTests
{
    [Test]
    [Property(Scenario.Key, "RS35")]
    public async Task Copied_binaries_are_executable_on_unix()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip.Test("Unix file modes don't exist on Windows.");
            return;
        }

        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        project.Write();
        var build = await project.RunAsync("build", "-c", "Release", "-o", "out");
        var publish = await project.RunAsync("publish", "-c", "Release", "-o", "pub");

        await Assert.That(build.ExitCode).IsEqualTo(0).Because(build.Output);
        await Assert.That(publish.ExitCode).IsEqualTo(0).Because(publish.Output);
        foreach (var folder in new[] { "out", "pub" })
        {
            var mode = File.GetUnixFileMode(Path.Combine(project.Directory, folder, "typst", "linux-x64", "typst"));
            await Assert.That(mode.HasFlag(UnixFileMode.UserExecute)).IsTrue();
        }
    }

    [Test]
    [Property(Scenario.Key, "RS24")]
    public async Task Platforms_that_cant_start_processes_copy_nothing_and_say_so()
    {
        using var project = await SampleProject.CreateAsync(runtimes: ["linux-x64"]);
        // A plain console project with browser-wasm needs the wasm-tools workload (NETSDK1147), so this is a minimal Blazor WebAssembly app.
        project.Sdk = "Microsoft.NET.Sdk.BlazorWebAssembly";
        project.ExtraItems.Add("""<PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.*" />""");
        project.Write();
        File.WriteAllText(Path.Combine(project.Directory, "Program.cs"), "await Microsoft.AspNetCore.Components.WebAssembly.Hosting.WebAssemblyHostBuilder.CreateDefault(args).Build().RunAsync();\n");
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out", "-v:n");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(run.DiagnosticCodes).Contains("TYPSTIUM0006");
        await Assert.That(project.TypstFiles("out")).IsEmpty();
    }

    [Test]
    public async Task Projects_in_folders_with_spaces_and_non_ascii_characters_copy_and_describe_their_runtimes()
    {
        using var project = await SampleProject.CreateAsync(folder: "My App ü", runtimes: ["linux-x64"]);
        project.Properties["TypstiumRuntimeDirectory"] = "Typst Runtime/{rid}";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out dir");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("out dir")).IsEquivalentTo(new[] { "Typst Runtime/linux-x64/typst", "typstium.runtime.json" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        using var manifest = System.Text.Json.JsonDocument.Parse(project.ReadText("out dir/typstium.runtime.json"));
        await Assert.That(manifest.RootElement.GetProperty("runtimes").GetProperty("linux-x64").GetString()).IsEqualTo("Typst Runtime/linux-x64/typst");
    }

    [Test]
    [Property(Scenario.Key, "RS20")]
    public async Task A_windows_forms_app_gets_its_runtime()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip.Test("Windows Forms builds only on Windows.");
            return;
        }

        using var project = await SampleProject.CreateAsync(runtimes: ["win-x64"]);
        project.TargetFrameworks = "net10.0-windows";
        project.OutputType = "WinExe";
        project.Properties["UseWindowsForms"] = "true";
        project.Write();
        var run = await project.RunAsync("build", "-c", "Release", "-o", "out");

        await Assert.That(run.ExitCode).IsEqualTo(0).Because(run.Output);
        await Assert.That(project.TypstFiles("out")).Contains("typst/win-x64/typst.exe");
    }
}
