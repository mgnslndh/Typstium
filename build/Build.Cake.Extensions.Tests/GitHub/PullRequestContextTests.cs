using Build.Cake.Extensions.GitHub;

namespace Build.Cake.Extensions.Tests.GitHub;

public class PullRequestContextTests
{
    [Test]
    public async Task Reads_number_head_and_author_from_the_event_payload()
    {
        var pullRequest = PullRequestEvent.Parse(Fixture.Read("Events", "pull_request.json"));

        await Assert.That(pullRequest).IsEqualTo(new PullRequestEvent(42, "0123456789abcdef0123456789abcdef01234567", "mgnslndh/Typstium", "mgnslndh"));
    }

    [Test]
    public async Task A_pull_request_from_this_repository_can_write()
    {
        var context = new PullRequestContext("mgnslndh/Typstium", "main", PullRequestEvent.Parse(Fixture.Read("Events", "pull_request.json")));

        await Assert.That(context.ReadOnlyReason).IsNull();
    }

    [Test]
    public async Task A_fork_is_read_only_whatever_the_case_of_its_name()
    {
        var context = new PullRequestContext("mgnslndh/Typstium", "main", new PullRequestEvent(1, "abc", "someone/Typstium", "someone"));
        var sameRepository = context with { PullRequest = new PullRequestEvent(1, "abc", "MGNSLNDH/typstium", "mgnslndh") };

        await Assert.That(context.ReadOnlyReason).IsEqualTo("a pull request from a fork (someone/Typstium)");
        await Assert.That(sameRepository.ReadOnlyReason).IsNull();
    }

    [Test]
    public async Task A_pull_request_from_a_deleted_fork_is_read_only()
    {
        var pullRequest = PullRequestEvent.Parse(Fixture.Read("Events", "pull_request-deleted-fork.json"));
        var context = new PullRequestContext("mgnslndh/Typstium", "main", pullRequest);

        await Assert.That(pullRequest.HeadRepository).IsNull();
        await Assert.That(context.ReadOnlyReason).IsEqualTo("a pull request from a deleted fork");
    }

    [Test]
    public async Task Dependabot_is_read_only()
    {
        var context = new PullRequestContext("mgnslndh/Typstium", "main", new PullRequestEvent(3, "abc", "mgnslndh/Typstium", "dependabot[bot]"));

        await Assert.That(context.ReadOnlyReason).IsEqualTo("a Dependabot pull request");
    }
}
