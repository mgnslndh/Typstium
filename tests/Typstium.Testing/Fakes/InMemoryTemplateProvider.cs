using System.Text;

namespace Typstium.Testing.Fakes;

/// <summary>A template provider backed by in-memory strings, for tests.</summary>
public sealed class InMemoryTemplateProvider : ITemplateProvider
{
    private readonly Dictionary<string, TypstTemplate> _templates = new(StringComparer.Ordinal);
    private int _requestCount;

    public int RequestCount => _requestCount;

    public InMemoryTemplateProvider Add(string name, string mainFile, params (string Path, string Content)[] files)
    {
        var templateName = TypstTemplateName.Parse(name);
        var templateFiles = files
            .Select(file => TypstTemplateFile.FromBytes(TypstPath.Parse(file.Path), Encoding.UTF8.GetBytes(file.Content)))
            .ToList();
        _templates[name] = new TypstTemplate(templateName, TypstPath.Parse(mainFile), new TypstTemplateVersion("test-" + name), templateFiles);
        return this;
    }

    public ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        return new ValueTask<TypstTemplate?>(_templates.TryGetValue(name.Value, out var template) ? template : null);
    }
}
