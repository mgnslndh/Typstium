using System.Text.Json;

namespace Typstium.Utilities;

internal static class JsonDefaults
{
    internal const string UnreferencedCodeMessage =
        "JSON serialization may need types that trimming removes. Use the overload that takes a JsonTypeInfo<T>.";

    internal const string DynamicCodeMessage =
        "JSON serialization may need runtime code generation. Use the overload that takes a JsonTypeInfo<T>.";

    /// <summary>Web defaults: camelCase property names, as Typst templates conventionally read them.</summary>
    internal static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
