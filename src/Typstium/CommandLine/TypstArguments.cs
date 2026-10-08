using System.Collections;
using System.Collections.Immutable;
using Typstium.CommandLine;

namespace Typstium;

/// <summary>
/// An immutable list of command-line arguments. A list, not a string: each item reaches Typst
/// unchanged, so no quoting variants are needed.
/// </summary>
public sealed class TypstArguments : IReadOnlyList<TypstArgument>, IEquatable<TypstArguments>
{
    internal const string Redacted = "[REDACTED]";

    private readonly ImmutableArray<TypstArgument> _items;

    private TypstArguments(ImmutableArray<TypstArgument> items) => _items = items;

    public static TypstArguments Empty { get; } = new(ImmutableArray<TypstArgument>.Empty);

    public static TypstArguments Of(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new TypstArguments(values.Select(TypstArgument.Plain).ToImmutableArray());
    }

    public int Count => _items.Length;

    public TypstArgument this[int index] => _items[index];

    public TypstArguments Add(string value) => new(_items.Add(TypstArgument.Plain(value)));

    public TypstArguments AddSecret(string value) => new(_items.Add(TypstArgument.Secret(value)));

    public TypstArguments AddSwitch(string name, string value) => AddSwitchCore(name, TypstArgument.Plain(value));

    public TypstArguments AddSwitchSecret(string name, string value) => AddSwitchCore(name, TypstArgument.Secret(value));

    public TypstArguments AddRange(TypstArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return new TypstArguments(_items.AddRange(arguments._items));
    }

    public TypstArguments Insert(int index, TypstArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return new TypstArguments(_items.Insert(index, argument));
    }

    public TypstArguments RemoveAt(int index) => new(_items.RemoveAt(index));

    public TypstArguments SetItem(int index, TypstArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return new TypstArguments(_items.SetItem(index, argument));
    }

    /// <summary>The index of the first argument with this exact value, or -1.</summary>
    public int IndexOf(string value)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (string.Equals(_items[i].Value, value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Renders the arguments for display, including secrets. Not used to start processes.</summary>
    public string Render() => WindowsArgumentEscaper.Join(_items.Select(item => item.Value));

    /// <summary>Renders the arguments for logs, with secrets replaced by [REDACTED].</summary>
    public string RenderSafe() =>
        string.Join(" ", _items.Select(item => item.IsSecret ? Redacted : WindowsArgumentEscaper.Escape(item.Value)));

    internal IReadOnlyList<string> ToValues() => _items.Select(item => item.Value).ToArray();

    public IEnumerator<TypstArgument> GetEnumerator() => ((IEnumerable<TypstArgument>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(TypstArguments? other) => other is not null && _items.SequenceEqual(other._items);

    public override bool Equals(object? obj) => Equals(obj as TypstArguments);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in _items)
        {
            hash = unchecked((hash * 31) + item.GetHashCode());
        }

        return hash;
    }

    public override string ToString() => RenderSafe();

    private TypstArguments AddSwitchCore(string name, TypstArgument value)
    {
        if (string.IsNullOrEmpty(name) || !name.StartsWith("-", StringComparison.Ordinal))
        {
            throw new ArgumentException("A switch name must start with '-', for example '--root'.", nameof(name));
        }

        return new TypstArguments(_items.Add(TypstArgument.Plain(name)).Add(value));
    }
}
