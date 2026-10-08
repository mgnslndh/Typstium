using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Typstium.Diagnostics;

/// <summary>
/// Parses Typst 0.15's <c>--diagnostic-format human</c> output (with <c>--color never</c>).
/// Typst 0.15 prints 1-based lines and 0-based columns; columns are converted to 1-based here.
/// </summary>
internal sealed class HumanDiagnosticParserV015 : IDiagnosticParser
{
    private const string LocationMarker = "┌─ ";

    private static readonly Regex Header = new("^(?<severity>error|warning): (?<message>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex Location = new("^(?<path>.+):(?<line>[0-9]+):(?<column>[0-9]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex Hint = new("^\\s*= (?<kind>hint|note|help): (?<text>.*)$", RegexOptions.CultureInvariant);
    private static readonly Regex TraceFrame = new("^\\s+(?<message>.+?) at (?<location>.+:[0-9]+:[0-9]+)$", RegexOptions.CultureInvariant);

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
            if (Header.IsMatch(line))
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

    private static void FlushBlock(List<string> block, ImmutableArray<RawDiagnostic>.Builder result)
    {
        if (block.Count == 0)
        {
            return;
        }

        var header = Header.Match(block[0]);
        var severity = header.Groups["severity"].Value == "warning" ? TypstDiagnosticSeverity.Warning : TypstDiagnosticSeverity.Error;
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
                location = ParseLocation(trimmed.Substring(LocationMarker.Length), endColumnCarets: null);
            }
            else if (IsGutterLine(line))
            {
                caretCount ??= CountCarets(line);
            }
            else if (Hint.Match(line) is { Success: true } hint)
            {
                var kind = hint.Groups["kind"].Value;
                var text = hint.Groups["text"].Value;
                hints.Add(kind == "hint" ? text : kind + ": " + text);
            }
            else if (TraceFrame.Match(line) is { Success: true } frame)
            {
                trace.Add(new RawTraceFrame(frame.Groups["message"].Value, ParseLocation(frame.Groups["location"].Value, endColumnCarets: null)));
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
        result.Add(new RawDiagnostic(severity, header.Groups["message"].Value, location, hints.ToImmutable(), trace.ToImmutable(), rawText, IsRecognized: true));
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

    private static RawLocation? ParseLocation(string text, int? endColumnCarets)
    {
        var match = Location.Match(text.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups["line"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var line)
            || !int.TryParse(match.Groups["column"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var zeroBasedColumn))
        {
            return null;
        }

        var column = zeroBasedColumn + 1;
        return new RawLocation(match.Groups["path"].Value, line, column, endColumnCarets is { } carets ? column + carets - 1 : null);
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
