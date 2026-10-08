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
