namespace Typstium;

/// <summary>A 1-based line and column, as used by MSBuild and editors.</summary>
public readonly record struct TypstPosition(int Line, int Column)
{
    /// <summary>Returns <c>line,column</c>, the form MSBuild uses.</summary>
    public override string ToString() => $"{Line},{Column}";
}
