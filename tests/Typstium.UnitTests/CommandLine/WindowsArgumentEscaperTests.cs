using System.Runtime.InteropServices;
using TUnit.Assertions.Enums;
using Typstium.CommandLine;

namespace Typstium.UnitTests.CommandLine;

public class WindowsArgumentEscaperTests
{
    [Test]
    [Arguments("simple", "simple")]
    [Arguments("", "\"\"")]
    [Arguments("with space", "\"with space\"")]
    [Arguments("quote\"inside", "\"quote\\\"inside\"")]
    [Arguments("C:\\Program Files\\", "\"C:\\Program Files\\\\\"")]
    [Arguments("a\\b", "a\\b")]
    [Arguments("tab\tinside", "\"tab\tinside\"")]
    public async Task Escape_follows_windows_rules(string input, string expected)
    {
        await Assert.That(WindowsArgumentEscaper.Escape(input)).IsEqualTo(expected);
    }

    public static IEnumerable<Func<string[]>> RoundTripCases()
    {
        yield return () => ["compile", "--root", @"C:\Users\Åsa Ström\AppData\Local\Temp\typstium\ws-1\root"];
        yield return () => ["--input", "data={\"name\":\"a \\\"quoted\\\" value\"}"];
        yield return () => [@"C:\trailing\", @"two\\", "", " ", "\"", "ends with space "];
    }

    [Test]
    [MethodDataSource(nameof(RoundTripCases))]
    public async Task Join_round_trips_through_the_windows_parser(string[] arguments)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip.Test("CommandLineToArgvW is Windows-only.");
        }

        var commandLine = "typst.exe " + WindowsArgumentEscaper.Join(arguments);
        var parsed = ParseWithWindows(commandLine).Skip(1).ToArray();
        await Assert.That(parsed).IsEquivalentTo(arguments, CollectionOrdering.Matching);
    }

    private static string[] ParseWithWindows(string commandLine)
    {
        var pointer = CommandLineToArgvW(commandLine, out var count);
        try
        {
            var result = new string[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(pointer, i * IntPtr.Size))!;
            }

            return result;
        }
        finally
        {
            LocalFree(pointer);
        }
    }

    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int count);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
