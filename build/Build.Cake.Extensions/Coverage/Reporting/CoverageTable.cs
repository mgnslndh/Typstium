namespace Build.Cake.Extensions.Coverage.Reporting;

public enum CoverageRowKind
{
    // A group within an assembly (a visibility, namespace or area).
    Group,

    // An assembly's subtotal, after its groups.
    Assembly,

    // The total of all assemblies, last.
    Total,

    // One class, in the top-classes view.
    Item,
}

public sealed record CoverageRow(CoverageRowKind Kind, string Name, IReadOnlyList<string> Cells);

// Headers[0] names the rows; Cells line up with Headers[1..]. The first two cell columns are right-aligned numbers.
public sealed record CoverageTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<CoverageRow> Rows);
