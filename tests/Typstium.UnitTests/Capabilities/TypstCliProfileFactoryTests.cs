using System.Collections.Immutable;
using TUnit.Assertions.Enums;
using Typstium.Capabilities;
using Typstium.CommandLine;
using Typstium.Diagnostics;
using Typstium.Testing.Fixtures;
using Typstium.UnitTests.Testing;

namespace Typstium.UnitTests.Capabilities;

public class TypstCliProfileFactoryTests
{
    private static TypstExecutable Executable(TemporaryDirectory temp) => new(temp.CreateFile("typst.exe", "fake"), "Test");

    [Test]
    public async Task Probes_once_even_under_parallel_cold_start()
    {
        using var temp = new TemporaryDirectory("profile");
        var runner = new FakeProcessRunner(_ => FakeProcessRunner.Exited(0, FakeProcessRunner.InfoJson("0.15.1"))) { Delay = TimeSpan.FromMilliseconds(50) };
        var factory = new TypstCliProfileFactory(runner);
        var executable = Executable(temp);

        var profiles = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => factory.GetProfileAsync(executable, CancellationToken.None))));

        await Assert.That(factory.ProbeCount).IsEqualTo(1);
        await Assert.That(profiles.Distinct().Count()).IsEqualTo(1);
        await Assert.That(profiles[0].Capabilities.Version).IsEqualTo(new TypstVersion(0, 15, 1));
        await Assert.That(runner.Specs[0].Arguments.ToValues()).IsEquivalentTo(new[] { "--color", "never", "info", "--format", "json" }, CollectionOrdering.Matching);
    }

    [Test]
    public async Task Typst_without_info_command_is_reported_as_too_old()
    {
        using var temp = new TemporaryDirectory("profile");
        var runner = new FakeProcessRunner(spec => FakeProcessRunner.IsInfo(spec)
            ? FakeProcessRunner.Exited(2, stderr: "error: unrecognized subcommand 'info'")
            : FakeProcessRunner.Exited(0, "typst 0.14.2 (abcdef12)"));
        var factory = new TypstCliProfileFactory(runner);

        var exception = await Assert.ThrowsAsync<TypstFeatureNotSupportedException>(() => factory.GetProfileAsync(Executable(temp), CancellationToken.None));
        await Assert.That(exception!.InstalledVersion).IsEqualTo(new TypstVersion(0, 14, 2));
        await Assert.That(exception.RequiredVersion).IsEqualTo(TypstCliProfileFactory.MinimumVersion);
    }

    [Test]
    public async Task Failures_are_not_cached()
    {
        using var temp = new TemporaryDirectory("profile");
        var calls = 0;
        var runner = new FakeProcessRunner(_ => Interlocked.Increment(ref calls) == 1
            ? FakeProcessRunner.Exited(101, stderr: "panicked")
            : FakeProcessRunner.Exited(0, FakeProcessRunner.InfoJson("0.15.1")));
        var factory = new TypstCliProfileFactory(runner);
        var executable = Executable(temp);

        await Assert.ThrowsAsync<TypstProcessCrashedException>(() => factory.GetProfileAsync(executable, CancellationToken.None));
        var profile = await factory.GetProfileAsync(executable, CancellationToken.None);
        await Assert.That(profile.Capabilities.Version).IsEqualTo(new TypstVersion(0, 15, 1));
    }

    [Test]
    public async Task Version_table_uses_the_newest_profile_for_unknown_newer_versions()
    {
        var info = new TypstInfo(new TypstVersion(0, 17, 3), ImmutableHashSet.Create("html"));
        var profile = TypstCliProfileFactory.CreateProfile(info);

        await Assert.That(profile.ArgumentsBuilder is CompileArgumentsBuilderV015).IsTrue();
        await Assert.That(profile.DiagnosticParser is HumanDiagnosticParserV015).IsTrue();
        await Assert.That(profile.Capabilities.SupportsHtml).IsTrue();
    }

    [Test]
    public async Task Versions_below_the_minimum_are_rejected()
    {
        var info = new TypstInfo(new TypstVersion(0, 14, 9), ImmutableHashSet<string>.Empty);
        var exception = Assert.Throws<TypstFeatureNotSupportedException>(() => TypstCliProfileFactory.CreateProfile(info));
        await Assert.That(exception).IsNotNull();
    }

    [Test]
    public async Task Html_support_follows_the_feature_list()
    {
        var withoutHtml = TypstCliProfileFactory.CreateProfile(new TypstInfo(new TypstVersion(0, 15, 1), ImmutableHashSet<string>.Empty));
        await Assert.That(withoutHtml.Capabilities.SupportsHtml).IsFalse();
        await Assert.That(withoutHtml.Capabilities.PdfStandards.Contains(PdfStandard.A2b)).IsTrue();
    }
}
