using System.Collections.Immutable;

namespace Typstium;

/// <summary>What to compile: the immutable contract every backend receives.</summary>
public sealed partial record TypstCompileRequest
{
    private TypstSource _source;
    private ImmutableDictionary<string, string> _inputs = ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);
    private TypstOutput _output = TypstOutput.Pdf();
    private TypstCompileOptions _options = TypstCompileOptions.Default;

    /// <summary>A request for <paramref name="source"/>, compiled to PDF with default options.</summary>
    public TypstCompileRequest(TypstSource source) => _source = source ?? throw new ArgumentNullException(nameof(source));

    /// <summary>What to compile.</summary>
    public TypstSource Source
    {
        get => _source;
        init => _source = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>Key/value strings visible to the template as <c>sys.inputs</c>.</summary>
    public ImmutableDictionary<string, string> Inputs
    {
        get => _inputs;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            foreach (var pair in value)
            {
                InputKeys.Validate(pair.Key, nameof(value));
                if (pair.Value is null)
                {
                    throw new ArgumentException($"The input '{pair.Key}' has a null value.", nameof(value));
                }
            }

            _inputs = value.WithComparers(StringComparer.Ordinal);
        }
    }

    /// <summary>The output format and its options. PDF by default.</summary>
    public TypstOutput Output
    {
        get => _output;
        init => _output = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>How to compile and under which limits. <see cref="TypstCompileOptions.Default"/> by default.</summary>
    public TypstCompileOptions Options
    {
        get => _options;
        init => _options = value ?? throw new ArgumentNullException(nameof(value));
    }
}
