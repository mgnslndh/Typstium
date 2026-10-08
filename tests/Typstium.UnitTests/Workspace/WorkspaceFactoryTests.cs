using Typstium.Testing.Fakes;
using Typstium.Testing.Fixtures;
using Typstium.Workspace;

namespace Typstium.UnitTests.Workspace;

public class WorkspaceFactoryTests
{
    [Test]
    public async Task Unknown_templates_are_a_caller_error()
    {
        using var temp = new TemporaryDirectory("factory");
        var factory = new WorkspaceFactory(temp.Path, TimeSpan.FromHours(1));
        var request = new TypstCompileRequest(TypstSource.Template(new InMemoryTemplateProvider(), TypstTemplateName.Parse("missing")));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => factory.CreateAsync(request, CancellationToken.None));
        await Assert.That(exception!.Message).Contains("missing");
    }

    [Test]
    public async Task A_failing_orphan_cleanup_does_not_fail_the_compile()
    {
        using var temp = new TemporaryDirectory("factory");
        var factory = new WorkspaceFactory(temp.Path, TimeSpan.FromHours(1), (_, _, _) => throw new InvalidOperationException("boom"));

        var workspace = await factory.CreateAsync(new TypstCompileRequest(TypstSource.Markup("= Hi")), CancellationToken.None);
        await workspace.DisposeAsync();

        await Assert.That(factory.CleanupRuns).IsEqualTo(1);
    }

    [Test]
    public async Task Cleaner_deletes_only_old_workspace_folders()
    {
        using var temp = new TemporaryDirectory("cleaner");
        var old = temp.CreateDirectory("ws-old");
        var fresh = temp.CreateDirectory("ws-fresh");
        var unrelated = temp.CreateDirectory("keep-me");
        Directory.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-2));
        Directory.SetLastWriteTimeUtc(unrelated, DateTime.UtcNow.AddHours(-2));

        var deleted = OrphanedWorkspaceCleaner.Clean(temp.Path, TimeSpan.FromHours(1), DateTime.UtcNow);

        await Assert.That(deleted).IsEqualTo(1);
        await Assert.That(Directory.Exists(old)).IsFalse();
        await Assert.That(Directory.Exists(fresh)).IsTrue();
        await Assert.That(Directory.Exists(unrelated)).IsTrue();
    }

    [Test]
    public async Task Parallel_cold_start_cleans_up_once_and_keeps_every_fresh_sandbox()
    {
        using var temp = new TemporaryDirectory("factory");
        var stale = temp.CreateDirectory("ws-stale");
        Directory.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-2));
        var factory = new WorkspaceFactory(temp.Path, TimeSpan.FromHours(1));
        var request = new TypstCompileRequest(TypstSource.Markup("= Hi"));

        var workspaces = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => factory.CreateAsync(request, CancellationToken.None))));
        try
        {
            await Assert.That(factory.CleanupRuns).IsEqualTo(1);
            await Assert.That(Directory.Exists(stale)).IsFalse();
            await Assert.That(workspaces.Select(w => w.Layout.RootDirectory).Distinct().Count()).IsEqualTo(20);
            await Assert.That(workspaces.All(w => Directory.Exists(w.Layout.RootDirectory))).IsTrue();
        }
        finally
        {
            foreach (var workspace in workspaces)
            {
                await workspace.DisposeAsync();
            }
        }
    }
}
