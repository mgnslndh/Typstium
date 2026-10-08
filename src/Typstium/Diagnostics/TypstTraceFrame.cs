namespace Typstium;

/// <summary>One "while calling …" frame, innermost first.</summary>
public sealed record TypstTraceFrame(string Message, TypstLocation? Location);
