namespace Typstium;

public enum TypstNetworkAccess
{
    /// <summary>Packages come only from the package path or cache; downloads fail fast.</summary>
    Blocked,

    /// <summary>Typst may download missing packages from the internet.</summary>
    Allowed,
}
