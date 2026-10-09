using System.Collections.Immutable;
using System.Text;
using Typstium.Testing.Fixtures;
using Typstium.Workspace;

namespace Typstium.UnitTests.Workspace;

public class SandboxWorkspaceTests
{
    private static TypstTemplate LicenseTemplate() => new(
        TypstTemplateName.Parse("license"),
        TypstPath.Parse("license.typ"),
        new TypstTemplateVersion("v1"),
        new[]
        {
            TypstTemplateFile.FromBytes(TypstPath.Parse("license.typ"), Encoding.UTF8.GetBytes("= License")),
            TypstTemplateFile.FromBytes(TypstPath.Parse("assets/logo.svg"), Encoding.UTF8.GetBytes("<svg/>")),
            TypstTemplateFile.FromBytes(TypstPath.Parse("data.json"), Encoding.UTF8.GetBytes("{\"sample\":true}")),
        });

    [Test]
    public async Task Template_sandbox_contains_bundle_and_virtual_files_with_virtual_files_winning()
    {
        using var temp = new TemporaryDirectory("sandbox");
        var data = TypstVirtualFile.FromText(TypstPath.Parse("data.json"), "{\"real\":true}");

        var workspace = await SandboxWorkspace.CreateForTemplateAsync(LicenseTemplate(), ImmutableArray.Create(data), needsPagedOutput: false, temp.Path, CancellationToken.None);
        try
        {
            var root = workspace.Layout.RootDirectory;
            await Assert.That(File.ReadAllText(Path.Combine(root, "assets", "logo.svg"))).IsEqualTo("<svg/>");
            await Assert.That(File.ReadAllText(Path.Combine(root, "data.json"))).IsEqualTo("{\"real\":true}");
            await Assert.That(workspace.Layout.MainInput).IsEqualTo(Path.Combine(root, "license.typ"));
            await Assert.That(workspace.Layout.PagedOutputDirectory).IsNull();
            await Assert.That(workspace.Layout.Mapping.IsSandbox).IsTrue();
            await Assert.That(workspace.Layout.Mapping.VirtualFiles.Contains(TypstPath.Parse("data.json"))).IsTrue();
        }
        finally
        {
            await workspace.DisposeAsync();
        }

        await Assert.That(Directory.GetDirectories(temp.Path).Length).IsEqualTo(0);
    }

    // An alias no name rule lists: NTFS 8.3 short names, generated on many system volumes (where %TEMP% lives).
    [Test]
    public async Task A_second_name_for_an_already_written_file_fails_instead_of_overwriting_it()
    {
        using var temp = new TemporaryDirectory("sandbox");
        File.WriteAllText(temp.Combine("datafile.json"), "probe");
        if (!File.Exists(temp.Combine("DATAFI~1.JSO")))
        {
            Skip.Test("This volume doesn't generate 8.3 short names.");
        }

        var template = new TypstTemplate(
            TypstTemplateName.Parse("t"),
            TypstPath.Parse("main.typ"),
            new TypstTemplateVersion("v1"),
            new[]
            {
                TypstTemplateFile.FromBytes(TypstPath.Parse("main.typ"), Encoding.UTF8.GetBytes("= Hi")),
                TypstTemplateFile.FromBytes(TypstPath.Parse("datafile.json"), Encoding.UTF8.GetBytes("{\"original\":true}")),
            });
        var alias = TypstVirtualFile.FromText(TypstPath.Parse("DATAFI~1.JSO"), "{\"replaced\":true}");

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            SandboxWorkspace.CreateForTemplateAsync(template, ImmutableArray.Create(alias), needsPagedOutput: false, temp.Path, CancellationToken.None));

        await Assert.That(exception!.Message).Contains("DATAFI~1.JSO");
        await Assert.That(Directory.GetDirectories(temp.Path).Length).IsEqualTo(0);
    }

    [Test]
    public async Task Markup_sandbox_sends_the_markup_over_stdin_and_creates_an_output_folder_for_paged_output()
    {
        using var temp = new TemporaryDirectory("sandbox");
        var workspace = await SandboxWorkspace.CreateForMarkupAsync("= Hi å", ImmutableArray<TypstVirtualFile>.Empty, needsPagedOutput: true, temp.Path, CancellationToken.None);
        try
        {
            await Assert.That(workspace.Layout.MainInput).IsEqualTo(WorkspaceLayout.StdinInput);
            await Assert.That(Encoding.UTF8.GetString(workspace.Layout.Stdin.ToArray())).IsEqualTo("= Hi å");
            await Assert.That(Directory.Exists(workspace.Layout.PagedOutputDirectory!)).IsTrue();
        }
        finally
        {
            await workspace.DisposeAsync();
        }
    }
}
