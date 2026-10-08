using System.Diagnostics;

namespace Typstium.Compilation;

/// <summary>OpenTelemetry-friendly tracing: one "typst.compile" activity per compile.</summary>
internal static class TypstTelemetry
{
    internal const string SourceName = "Typstium";

    internal static readonly ActivitySource Source = new(SourceName, typeof(TypstTelemetry).Assembly.GetName().Version?.ToString());

    public static Activity? StartCompile(TypstCompileRequest request)
    {
        var activity = Source.StartActivity("typst.compile");
        if (activity is null)
        {
            return null;
        }

        activity.SetTag("typst.source.kind", request.Source switch
        {
            TypstSource.FileSource => "file",
            TypstSource.TemplateSource => "template",
            TypstSource.MarkupSource => "markup",
            _ => "unknown",
        });
        if (request.Source is TypstSource.TemplateSource template)
        {
            activity.SetTag("typst.template.name", template.Name.Value);
        }

        activity.SetTag("typst.output.format", request.Output.FileExtension);
        return activity;
    }

    public static void Complete(Activity? activity, TypstCompileResult result)
    {
        if (activity is null)
        {
            return;
        }

        activity.SetTag("typst.version", result.Invocation.TypstVersion.ToString());
        activity.SetTag("typst.status", result.Status.ToString());
        activity.SetTag("typst.process.duration_ms", result.Invocation.ProcessDuration.TotalMilliseconds);
        activity.SetTag("typst.diagnostics.errors", result.Errors.Count());
        activity.SetTag("typst.diagnostics.warnings", result.Warnings.Count());
        if (!result.IsSuccess)
        {
            activity.SetStatus(ActivityStatusCode.Error, result.Status.ToString());
        }
    }

    public static void Fail(Activity? activity, Exception exception) =>
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name + ": " + exception.Message);
}
