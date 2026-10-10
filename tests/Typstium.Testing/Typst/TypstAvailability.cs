namespace Typstium.Testing.Typst;

/// <summary>Finds Typst for integration tests: skip when it's missing, unless TYPSTIUM_REQUIRE_TYPST=1.</summary>
public static class TypstAvailability
{
    private static readonly Lazy<Task<string?>> Located = new(LocateAsync);

    public static bool Required => Environment.GetEnvironmentVariable("TYPSTIUM_REQUIRE_TYPST") == "1";

    public static async Task<CliTypstCompiler> CreateCompilerAsync(CliTypstCompilerSettings settings)
    {
        var path = await Located.Value;
        if (path is null)
        {
            if (Required)
            {
                throw new InvalidOperationException("TYPSTIUM_REQUIRE_TYPST=1 but Typst was not found.");
            }

            Skip.Test("Typst is not installed. Install it (winget install Typst.Typst) or set TYPST_PATH to run integration tests.");
        }

        return new CliTypstCompiler(TypstExecutableResolver.CreateDefault(path!), settings);
    }

    /// <summary>The full path of the Typst found for integration tests; skips (or fails when required) without one.</summary>
    public static async Task<string> RequireExecutablePathAsync()
    {
        var path = await Located.Value;
        if (path is null)
        {
            if (Required)
            {
                throw new InvalidOperationException("TYPSTIUM_REQUIRE_TYPST=1 but Typst was not found.");
            }

            Skip.Test("Typst is not installed. Install it (winget install Typst.Typst) or set TYPST_PATH to run integration tests.");
        }

        return path!;
    }

    private static async Task<string?> LocateAsync()
    {
        try
        {
            return (await TypstExecutableResolver.CreateDefault().ResolveAsync(CancellationToken.None)).Path;
        }
        catch (TypstExecutableNotFoundException)
        {
            return null;
        }
    }
}
