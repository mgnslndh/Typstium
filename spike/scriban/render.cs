// THROWAWAY SPIKE CODE. Not part of the library.
// Renders a Scriban template over a JSON model into Markdown and lays out a
// sandbox root (layout + assets + generated Markdown) ready for `typst compile`.
//
// Usage: dotnet run render.cs -- <model.json> <template.md.sbn> <layout-dir> <sandbox-dir>
#:package Scriban@7.5.0

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Scriban;
using Scriban.Runtime;

if (args.Length != 4)
{
    Console.Error.WriteLine("usage: render.cs <model.json> <template.md.sbn> <layout-dir> <sandbox-dir>");
    return 2;
}

var (modelPath, templatePath, layoutDir, sandboxDir) = (args[0], args[1], args[2], args[3]);

var sw = Stopwatch.StartNew();
var template = Template.Parse(File.ReadAllText(templatePath), templatePath);
if (template.HasErrors)
{
    foreach (var m in template.Messages) Console.Error.WriteLine(m);
    return 1;
}

var globals = (ScriptObject)ToScript(JsonDocument.Parse(File.ReadAllText(modelPath)).RootElement)!;
globals.Import("md", new Func<object?, string>(v => EscapeMarkdown(v?.ToString() ?? "")));

var context = new TemplateContext { StrictVariables = true };
context.PushGlobal(globals);
var markdown = template.Render(context);
var renderMs = sw.Elapsed.TotalMilliseconds;

// Sandbox root: only what the template needs. Never the app directory.
if (Directory.Exists(sandboxDir)) Directory.Delete(sandboxDir, recursive: true);
Directory.CreateDirectory(sandboxDir);
File.Copy(Path.Combine(layoutDir, "release-notes.typ"), Path.Combine(sandboxDir, "release-notes.typ"));
File.Copy(Path.Combine(layoutDir, "logo.png"), Path.Combine(sandboxDir, "logo.png"));
File.WriteAllText(Path.Combine(sandboxDir, "release-notes.md"), markdown, new UTF8Encoding(false));

Console.WriteLine($"Scriban render: {renderMs:F1} ms, {markdown.Length} chars -> {sandboxDir}");
return 0;

// CommonMark: any ASCII punctuation character may be backslash-escaped, so
// escaping all of them makes data render literally. Newlines would start new
// blocks, so they are folded into spaces.
static string EscapeMarkdown(string s)
{
    var sb = new StringBuilder(s.Length * 2);
    foreach (var ch in s)
    {
        if (ch is '\r' or '\n') { sb.Append(' '); continue; }
        if (ch < 128 && char.IsPunctuation(ch) || ch < 128 && char.IsSymbol(ch)) sb.Append('\\');
        sb.Append(ch);
    }
    return sb.ToString();
}

static object? ToScript(JsonElement e) => e.ValueKind switch
{
    JsonValueKind.Object => e.EnumerateObject().Aggregate(new ScriptObject(), (o, p) => { o[p.Name] = ToScript(p.Value); return o; }),
    JsonValueKind.Array => new ScriptArray(e.EnumerateArray().Select(ToScript)),
    JsonValueKind.String => e.GetString(),
    JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.GetDouble(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    _ => null,
};
