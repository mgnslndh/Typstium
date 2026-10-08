using System.Reflection;
using Typstium.Templates;

namespace Typstium;

/// <summary>
/// Templates embedded in an assembly. Embed them with a LogicalName that keeps folder separators:
/// <c>&lt;EmbeddedResource Include="Templates\**" LogicalName="Templates/%(RecursiveDir)%(Filename)%(Extension)" /&gt;</c>
/// and pass "Templates" as the prefix.
/// </summary>
public sealed class EmbeddedResourceTemplateProvider : ITemplateProvider
{
    private readonly Assembly _assembly;
    private readonly string _prefix;

    public EmbeddedResourceTemplateProvider(Assembly assembly, string resourcePrefix)
    {
        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourcePrefix);
        _prefix = Normalize(resourcePrefix).TrimEnd('/') + "/";
    }

    public async ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        var templatePrefix = _prefix + name.Value + "/";
        var files = _assembly.GetManifestResourceNames()
            .Select(resource => (Resource: resource, Normalized: Normalize(resource)))
            .Where(entry => entry.Normalized.StartsWith(templatePrefix, StringComparison.Ordinal))
            .Select(entry => (TypstTemplateFile)new EmbeddedResourceTemplateFile(
                TypstPath.Parse(entry.Normalized.Substring(templatePrefix.Length)), _assembly, entry.Resource))
            .ToList();

        if (files.Count == 0)
        {
            return null;
        }

        var mainFile = TemplateBundleRules.SelectMainFile(name, files.Select(file => file.Path));
        var version = await TemplateHasher.ComputeAsync(files, cancellationToken).ConfigureAwait(false);
        return new TypstTemplate(name, mainFile, version, files);
    }

    // RecursiveDir produces backslashes on Windows.
    private static string Normalize(string resourceName) => resourceName.Replace('\\', '/');

    private sealed class EmbeddedResourceTemplateFile(TypstPath path, Assembly assembly, string resourceName) : TypstTemplateFile(path)
    {
        public override ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken) =>
            new(assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"The embedded resource '{resourceName}' disappeared from {assembly.GetName().Name}."));
    }
}
