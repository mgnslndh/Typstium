using System.Collections;
using System.Collections.Immutable;

namespace Typstium;

/// <summary>
/// An immutable set of inputs with unique keys, visible to the document as <c>sys.inputs</c>.
/// Enumerates in ordinal key order, so the command line doesn't depend on the order inputs were added.
/// </summary>
public sealed class TypstInputs : IReadOnlyCollection<TypstInput>, IEquatable<TypstInputs>
{
    private readonly ImmutableSortedDictionary<string, TypstInput> _items;

    private TypstInputs(ImmutableSortedDictionary<string, TypstInput> items) => _items = items;

    /// <summary>No inputs.</summary>
    public static TypstInputs Empty { get; } = new(ImmutableSortedDictionary.Create<string, TypstInput>(StringComparer.Ordinal));

    /// <summary>The given inputs.</summary>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> contains null or two inputs with the same key.</exception>
    public static TypstInputs Of(params TypstInput[] inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var builder = ImmutableSortedDictionary.CreateBuilder<string, TypstInput>(StringComparer.Ordinal);
        foreach (var input in inputs)
        {
            if (input is null)
            {
                throw new ArgumentException("Inputs must not be null.", nameof(inputs));
            }

            if (builder.ContainsKey(input.Key.Value))
            {
                throw new ArgumentException($"The input '{input.Key}' is given more than once.", nameof(inputs));
            }

            builder.Add(input.Key.Value, input);
        }

        return new TypstInputs(builder.ToImmutable());
    }

    /// <summary>The number of inputs.</summary>
    public int Count => _items.Count;

    /// <summary>The value of the input with <paramref name="key"/>.</summary>
    /// <exception cref="KeyNotFoundException">No input has this key.</exception>
    public string this[TypstInputKey key] =>
        TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"There is no input '{key}'.");

    /// <summary>The value of the input with <paramref name="key"/>; a shorthand for the <see cref="TypstInputKey"/> indexer.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> isn't a valid input key.</exception>
    /// <exception cref="KeyNotFoundException">No input has this key.</exception>
    public string this[string key] => this[TypstInputKey.Parse(key, nameof(key))];

    /// <summary>Whether an input has <paramref name="key"/>.</summary>
    public bool ContainsKey(TypstInputKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _items.ContainsKey(key.Value);
    }

    /// <summary>Gets the value of the input with <paramref name="key"/>, if there is one.</summary>
    public bool TryGetValue(TypstInputKey key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return TryGetValueCore(key.Value, out value);
    }

    /// <summary>
    /// Gets the value of the input with <paramref name="key"/>, if there is one; a shorthand for the
    /// <see cref="TypstInputKey"/> overload. Returns <c>false</c> for a key that isn't valid, since no input can have it.
    /// </summary>
    public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        ArgumentNullException.ThrowIfNull(key);
        return TryGetValueCore(key, out value);
    }

    private bool TryGetValueCore(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        if (_items.TryGetValue(key, out var input))
        {
            value = input.Value;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>Returns a copy with <paramref name="input"/> added, replacing any input with the same key.</summary>
    public TypstInputs Set(TypstInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new TypstInputs(_items.SetItem(input.Key.Value, input));
    }

    /// <summary>Returns a copy without the input with <paramref name="key"/>; unchanged if there is none.</summary>
    public TypstInputs Remove(TypstInputKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new TypstInputs(_items.Remove(key.Value));
    }

    /// <summary>Enumerates the inputs in ordinal key order.</summary>
    public IEnumerator<TypstInput> GetEnumerator() => _items.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Two sets are equal when they hold equal inputs.</summary>
    public bool Equals(TypstInputs? other) => other is not null && _items.Values.SequenceEqual(other._items.Values);

    /// <inheritdoc cref="Equals(TypstInputs?)"/>
    public override bool Equals(object? obj) => Equals(obj as TypstInputs);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var input in _items.Values)
        {
            hash = unchecked((hash * 31) + input.GetHashCode());
        }

        return hash;
    }

    /// <summary>Lists the keys with values redacted, e.g. <c>data=[REDACTED], edition=[REDACTED]</c>.</summary>
    public override string ToString() => string.Join(", ", _items.Values);
}
