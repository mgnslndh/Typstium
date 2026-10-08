namespace Typstium;

internal static class InputKeys
{
    public static void Validate(string key, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Input keys must not be empty.", parameterName);
        }

        if (key.IndexOf('=') >= 0)
        {
            throw new ArgumentException($"The input key '{key}' contains '=', which Typst uses to separate key and value.", parameterName);
        }
    }
}
