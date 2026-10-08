namespace Typstium;

/// <summary>Supplies named template bundles: a main file plus every file it may reference.</summary>
public interface ITemplateProvider
{
    /// <returns>The template, or <c>null</c> when no template has this name.</returns>
    ValueTask<TypstTemplate?> GetTemplateAsync(TypstTemplateName name, CancellationToken cancellationToken);
}
