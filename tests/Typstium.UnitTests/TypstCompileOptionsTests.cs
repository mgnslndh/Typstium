namespace Typstium.UnitTests;

public class TypstCompileOptionsTests
{
    [Test]
    public async Task Default_is_safe_and_reproducible()
    {
        var options = TypstCompileOptions.Default;
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(60));
        await Assert.That(options.MaxOutputBytes).IsEqualTo(100L * 1024 * 1024);
        await Assert.That(options.MemoryLimitBytes).IsNull();
        await Assert.That(options.Fonts.IncludeSystemFonts).IsFalse();
        await Assert.That(options.Fonts.IncludeEmbeddedFonts).IsTrue();
        await Assert.That(options.Packages.NetworkAccess).IsEqualTo(TypstNetworkAccess.Blocked);
        await Assert.That(options.CreationTimestamp).IsEqualTo(TypstCreationTimestamp.Inherit);
        await Assert.That(options.ArgumentCustomization).IsNull();
    }

    [Test]
    public async Task Untrusted_tightens_limits()
    {
        var options = TypstCompileOptions.Untrusted;
        await Assert.That(options.Timeout).IsEqualTo(TimeSpan.FromSeconds(10));
        await Assert.That(options.MaxOutputBytes).IsEqualTo(20L * 1024 * 1024);
        await Assert.That(options.MemoryLimitBytes).IsEqualTo(1024L * 1024 * 1024);
    }

    [Test]
    public async Task Invalid_values_cannot_be_set()
    {
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = TypstCompileOptions.Default with { Timeout = TimeSpan.Zero })).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = TypstCompileOptions.Default with { MaxOutputBytes = 0 })).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = TypstCompileOptions.Default with { Jobs = 0 })).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentNullException>(() => _ = TypstCompileOptions.Default with { Fonts = null! })).IsNotNull();
    }

    [Test]
    public async Task Infinite_timeout_is_allowed_explicitly()
    {
        var options = TypstCompileOptions.Default with { Timeout = Timeout.InfiniteTimeSpan };
        await Assert.That(options.Timeout).IsEqualTo(Timeout.InfiniteTimeSpan);
    }

    [Test]
    public async Task Font_paths_default_to_empty_not_default()
    {
        var fonts = TypstFontOptions.Default with { FontPaths = default };
        await Assert.That(fonts.FontPaths.IsDefault).IsFalse();
    }

    [Test]
    public async Task Process_priority_defaults_to_the_compiler_setting_and_rejects_unknown_values()
    {
        await Assert.That(TypstCompileOptions.Default.ProcessPriority).IsNull();
        await Assert.That(TypstCompileOptions.Untrusted.ProcessPriority).IsNull();
        await Assert.That(CliTypstCompilerSettings.Default.ProcessPriority).IsEqualTo(TypstProcessPriority.Normal);
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = TypstCompileOptions.Default with { ProcessPriority = (TypstProcessPriority)42 })).IsNotNull();
        await Assert.That(Assert.Throws<ArgumentOutOfRangeException>(() => _ = CliTypstCompilerSettings.Default with { ProcessPriority = (TypstProcessPriority)42 })).IsNotNull();
    }
}
