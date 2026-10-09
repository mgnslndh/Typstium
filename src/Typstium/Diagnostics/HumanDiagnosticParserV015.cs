using System.Collections.Immutable;
using System.Globalization;

namespace Typstium.Diagnostics;

/// <summary>
/// Parses Typst 0.15's <c>--diagnostic-format human</c> output (with <c>--color never</c>).
/// Typst 0.15 prints 1-based lines and 0-based columns; columns are converted to 1-based here.
/// </summary>
/// <remarks>
/// Stderr is attacker-influenced (an <c>assert</c> message can print anything), so every line is matched by hand in a
/// single pass, without regular expressions: the whole parse is linear in the size of stderr, which the runner caps.
/// A backtracking pattern here once took seconds for one long line and would have taken hours at the cap.
/// </remarks>
internal sealed class HumanDiagnosticParserV015 : IDiagnosticParser
{
    private const string LocationMarker = "┌─ ";
    private const string TraceSeparator = " at ";

    public ImmutableArray<RawDiagnostic> Parse(string standardError)
    {
        var result = ImmutableArray.CreateBuilder<RawDiagnostic>();
        if (string.IsNullOrEmpty(standardError))
        {
            return result.ToImmutable();
        }

        var block = new List<string>();
        var orphans = new List<string>();
        foreach (var line in standardError.Replace("\r\n", "\n").Split('\n'))
        {
            if (TryParseHeader(line, out _, out _))
            {
                FlushBlock(block, result);
                FlushUnrecognized(orphans, result);
                block.Add(line);
            }
            else if (block.Count > 0)
            {
                block.Add(line);
            }
            else if (!IsNoise(line))
            {
                orphans.Add(line);
            }
        }

        FlushBlock(block, result);
        FlushUnrecognized(orphans, result);
        return result.ToImmutable();
    }

    /// <summary><c>error: message</c> or <c>warning: message</c>.</summary>
    internal static bool TryParseHeader(string line, out TypstDiagnosticSeverity severity, out string message)
    {
        const string Error = "error: ";
        const string Warning = "warning: ";
        if (line.StartsWith(Error, StringComparison.Ordinal))
        {
            severity = TypstDiagnosticSeverity.Error;
            message = line.Substring(Error.Length);
            return true;
        }

        if (line.StartsWith(Warning, StringComparison.Ordinal))
        {
            severity = TypstDiagnosticSeverity.Warning;
            message = line.Substring(Warning.Length);
            return true;
        }

        severity = default;
        message = string.Empty;
        return false;
    }

    /// <summary>
    /// <c>= hint: text</c>, <c>= note: text</c> or <c>= help: text</c> after optional whitespace. Notes and help keep
    /// their kind as a prefix; hints don't.
    /// </summary>
    internal static bool TryParseHint(string line, out string hint)
    {
        var start = SkipWhitespace(line, 0);
        foreach (var kind in new[] { "hint", "note", "help" })
        {
            var prefix = "= " + kind + ": ";
            if (string.CompareOrdinal(line, start, prefix, 0, prefix.Length) == 0)
            {
                var text = line.Substring(start + prefix.Length);
                hint = kind == "hint" ? text : kind + ": " + text;
                return true;
            }
        }

        hint = string.Empty;
        return false;
    }

    /// <summary>
    /// An indented <c>message at path:line:column</c> line. The message ends at the first <c> at </c> that leaves a
    /// valid location after it, so a message may itself contain <c> at </c>.
    /// </summary>
    internal static bool TryParseTraceFrame(string line, out string message, out RawLocation? location)
    {
        message = string.Empty;
        location = null;
        var start = SkipWhitespace(line, 0);
        if (start == 0 || start == line.Length || !TryFindLocationSuffix(line, start, out var suffixStart))
        {
            return false;
        }

        // The location needs a non-empty path before its ":line:column" suffix.
        var separator = line.IndexOf(TraceSeparator, start, StringComparison.Ordinal);
        if (separator <= start || separator + TraceSeparator.Length >= suffixStart)
        {
            return false;
        }

        message = line.Substring(start, separator - start);
        location = ParseLocation(line.Substring(separator + TraceSeparator.Length));
        return true;
    }

    /// <summary><c>path:line:column</c> with a non-empty path, which may itself contain colons (<c>C:\x.typ:2:1</c>).</summary>
    internal static RawLocation? ParseLocation(string text)
    {
        text = text.Trim();
        if (!TryFindLocationSuffix(text, 0, out var suffixStart))
        {
            return null;
        }

        var lineStart = suffixStart + 1;
        var columnSeparator = text.IndexOf(':', lineStart);
        if (!int.TryParse(text.Substring(lineStart, columnSeparator - lineStart), NumberStyles.None, CultureInfo.InvariantCulture, out var line)
            || !int.TryParse(text.Substring(columnSeparator + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var zeroBasedColumn))
        {
            return null;
        }

        return new RawLocation(text.Substring(0, suffixStart), line, zeroBasedColumn + 1, null);
    }

    /// <summary>
    /// Finds a trailing <c>:digits:digits</c> with at least one character before it (from <paramref name="start"/>),
    /// scanning back from the end once. <paramref name="suffixStart"/> is the index of its first colon.
    /// </summary>
    private static bool TryFindLocationSuffix(string text, int start, out int suffixStart)
    {
        suffixStart = -1;
        var i = text.Length - 1;
        for (var group = 0; group < 2; group++)
        {
            var digitsEnd = i;
            while (i >= start && text[i] is >= '0' and <= '9')
            {
                i--;
            }

            if (i == digitsEnd || i < start || text[i] != ':')
            {
                return false;
            }

            i--;
        }

        suffixStart = i + 1;
        return suffixStart > start;
    }

    private static int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    private static void FlushBlock(List<string> block, ImmutableArray<RawDiagnostic>.Builder result)
    {
        if (block.Count == 0)
        {
            return;
        }

        TryParseHeader(block[0], out var severity, out var headerMessage);
        RawLocation? location = null;
        int? caretCount = null;
        var hints = ImmutableArray.CreateBuilder<string>();
        var trace = ImmutableArray.CreateBuilder<RawTraceFrame>();
        var unrecognized = new List<string>();
        var previousWasTrace = false;

        foreach (var line in block.Skip(1))
        {
            var trimmed = line.Trim();
            var isTrace = false;
            if (trimmed.Length == 0 || IsNoise(line))
            {
                // Blank lines separate sections.
            }
            else if (trimmed.StartsWith(LocationMarker, StringComparison.Ordinal))
            {
                location = ParseLocation(trimmed.Substring(LocationMarker.Length));
            }
            else if (IsGutterLine(line))
            {
                caretCount ??= CountCarets(line);
            }
            else if (TryParseHint(line, out var hint))
            {
                hints.Add(hint);
            }
            else if (TryParseTraceFrame(line, out var frameMessage, out var frameLocation))
            {
                trace.Add(new RawTraceFrame(frameMessage, frameLocation));
                isTrace = true;
            }
            else if (previousWasTrace && line.StartsWith("    ", StringComparison.Ordinal))
            {
                // The source snippet printed under a trace frame.
            }
            else
            {
                unrecognized.Add(line);
            }

            previousWasTrace = isTrace;
        }

        if (location is not null && caretCount is { } carets)
        {
            location = location with { EndColumn = location.Column + carets - 1 };
        }

        var rawText = string.Join("\n", block).TrimEnd();
        result.Add(new RawDiagnostic(severity, headerMessage, location, hints.ToImmutable(), trace.ToImmutable(), rawText, IsRecognized: true));
        FlushUnrecognized(unrecognized, result);
        block.Clear();
    }

    private static void FlushUnrecognized(List<string> lines, ImmutableArray<RawDiagnostic>.Builder result)
    {
        var text = string.Join("\n", lines).Trim();
        lines.Clear();
        if (text.Length == 0)
        {
            return;
        }

        result.Add(new RawDiagnostic(
            TypstDiagnosticSeverity.Error, text, null, ImmutableArray<string>.Empty, ImmutableArray<RawTraceFrame>.Empty, text, IsRecognized: false));
    }

    /// <summary>Source and underline lines of the snippet: they contain the box-drawing gutter.</summary>
    private static bool IsGutterLine(string line) =>
        line.IndexOf('│') >= 0 || line.IndexOf('╭') >= 0 || line.IndexOf('╰') >= 0;

    /// <summary>The number of carets on a pure underline line ("  │   ^^^"), or null for anything else.</summary>
    private static int? CountCarets(string line)
    {
        var gutter = line.IndexOf('│');
        if (gutter < 0)
        {
            return null;
        }

        var underline = line.Substring(gutter + 1).Trim();
        if (underline.Length == 0 || underline.Any(c => c != '^'))
        {
            return null;
        }

        return underline.Length;
    }

    private static bool IsNoise(string line) =>
        line.Trim().Length == 0
        || line.StartsWith("downloading ", StringComparison.Ordinal)
        || line.IndexOf(" ETA: ", StringComparison.Ordinal) >= 0;
}
