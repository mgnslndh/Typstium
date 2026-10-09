using System.Text.Json;
using Build.Cake.Extensions.GitHub;

namespace Build.Cake.Extensions.Tests.GitHub;

public class GitHubJsonTests
{
    [Test]
    public async Task Finds_the_first_comment_starting_with_the_marker_across_pages()
    {
        const string slurped = """
            [
              [ { "id": 1, "body": "Looks good" }, { "id": 2, "body": "see <!-- typstium-coverage --> later" } ],
              [ { "id": 3, "body": "<!-- typstium-coverage -->\n### Coverage" }, { "id": 4, "body": "<!-- typstium-coverage -->" } ]
            ]
            """;

        await Assert.That(GitHubJson.FindCommentStartingWith(slurped, "<!-- typstium-coverage -->")).IsEqualTo(3L);
        await Assert.That(GitHubJson.FindCommentStartingWith("[[]]", "<!-- typstium-coverage -->")).IsNull();
    }

    [Test]
    public async Task Parses_workflow_runs_in_the_order_listed()
    {
        var runs = WorkflowRun.ParseList("""[ { "databaseId": 11, "headSha": "aaa" }, { "databaseId": 10, "headSha": "bbb" } ]""");

        await Assert.That(runs).IsEquivalentTo([new WorkflowRun(11, "aaa"), new WorkflowRun(10, "bbb")], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task Payloads_carry_Markdown_unescaped_and_intact()
    {
        const string markdown = "<!-- m -->\n### Coverage: 83.0% lines 🟢 +1.0 pp\n| a | b |\n";

        using var comment = JsonDocument.Parse(GitHubJson.CommentPayload(markdown));
        using var check = JsonDocument.Parse(GitHubJson.CheckRunPayload("Coverage", "abc", "success", "83.0% lines (+1.0 pp)", markdown));

        await Assert.That(comment.RootElement.GetProperty("body").GetString()).IsEqualTo(markdown);
        await Assert.That(check.RootElement.GetProperty("name").GetString()).IsEqualTo("Coverage");
        await Assert.That(check.RootElement.GetProperty("head_sha").GetString()).IsEqualTo("abc");
        await Assert.That(check.RootElement.GetProperty("status").GetString()).IsEqualTo("completed");
        await Assert.That(check.RootElement.GetProperty("conclusion").GetString()).IsEqualTo("success");
        await Assert.That(check.RootElement.GetProperty("output").GetProperty("title").GetString()).IsEqualTo("83.0% lines (+1.0 pp)");
        await Assert.That(check.RootElement.GetProperty("output").GetProperty("summary").GetString()).IsEqualTo(markdown);
    }
}
