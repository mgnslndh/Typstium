using System.Text.Encodings.Web;
using System.Text.Json;

namespace Build.Cake.Extensions.GitHub;

// Request bodies and response parsing for `gh api`. Bodies go through files (--input), so Markdown never has to
// survive command-line quoting.
public static class GitHubJson
{
    private static readonly JsonSerializerOptions Options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // The comments of `gh api --paginate --slurp`: an array of pages, each an array of comments.
    public static long? FindCommentStartingWith(string slurpedComments, string marker)
    {
        using var document = JsonDocument.Parse(slurpedComments);
        foreach (var page in document.RootElement.EnumerateArray())
        {
            foreach (var comment in page.EnumerateArray())
            {
                if (comment.GetProperty("body").GetString()?.StartsWith(marker, StringComparison.Ordinal) == true)
                {
                    return comment.GetProperty("id").GetInt64();
                }
            }
        }

        return null;
    }

    public static string CommentPayload(string body) => JsonSerializer.Serialize(new { body }, Options);

    public static string CheckRunPayload(string name, string headSha, string conclusion, string title, string summary) =>
        JsonSerializer.Serialize(new { name, head_sha = headSha, status = "completed", conclusion, output = new { title, summary } }, Options);
}
