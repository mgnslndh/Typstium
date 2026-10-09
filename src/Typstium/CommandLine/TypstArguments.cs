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

    /// <summary>No arguments.</summary>
    public static TypstArguments Empty { get; } = new(ImmutableArray<TypstArgument>.Empty);

    /// <summary>Plain arguments with the given values, in order.</summary>
    public static TypstArguments Of(params string[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new TypstArguments(values.Select(TypstArgument.Plain).ToImmutableArray());
    }

    /// <summary>The number of arguments.</summary>
    public int Count => _items.Length;

    /// <summary>The argument at <paramref name="index"/>.</summary>
    public TypstArgument this[int index] => _items[index];

    /// <summary>Returns a copy with a plain argument appended.</summary>
    public TypstArguments Add(string value) => new(_items.Add(TypstArgument.Plain(value)));

    /// <summary>Returns a copy with a secret argument appended.</summary>
    public TypstArguments AddSecret(string value) => new(_items.Add(TypstArgument.Secret(value)));

    /// <summary>Returns a copy with a switch and its value appended as two arguments, e.g. <c>--input</c> <c>key=value</c>.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> doesn't start with <c>-</c>.</exception>
    public TypstArguments AddSwitch(string name, string value) => AddSwitchCore(name, TypstArgument.Plain(value));

    /// <summary>Like <see cref="AddSwitch"/>, but the value is secret. The switch name stays visible in logs.</summary>
    /// <exception cref="ArgumentException"><paramref name="name"/> doesn't start with <c>-</c>.</exception>
    public TypstArguments AddSwitchSecret(string name, string value) => AddSwitchCore(name, TypstArgument.Secret(value));

    /// <summary>Returns a copy with <paramref name="arguments"/> appended.</summary>
    public TypstArguments AddRange(TypstArguments arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return new TypstArguments(_items.AddRange(arguments._items));
    }

    /// <summary>Returns a copy with <paramref name="argument"/> inserted at <paramref name="index"/>.</summary>
    public TypstArguments Insert(int index, TypstArgument argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        return new TypstArguments(_items.Insert(index, argument));
    }

    /// <summary>Returns a copy without the argument at <paramref name="index"/>.</summary>
    public TypstArguments RemoveAt(int index) => new(_items.RemoveAt(index));

    /// <summary>Returns a copy with the argument at <paramref name="index"/> replaced.</summary>
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

    /// <inheritdoc/>
    public IEnumerator<TypstArgument> GetEnumerator() => ((IEnumerable<TypstArgument>)_items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Two lists are equal when they hold equal arguments in the same order.</summary>
    public bool Equals(TypstArguments? other) => other is not null && _items.SequenceEqual(other._items);

    /// <inheritdoc cref="Equals(TypstArguments?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstArguments);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in _items)
        {
            hash = unchecked((hash * 31) + item.GetHashCode());
        }

        return hash;
    }

    /// <summary>Returns <see cref="RenderSafe"/>, so secrets never reach a log by accident.</summary>
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
