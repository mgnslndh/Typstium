namespace Build.Cake.Extensions.Tests;

public class ScaffoldTests
{
    [Test]
    public async Task Test_project_runs()
    {
        await Assert.That(AppContext.BaseDirectory).IsNotEmpty();
    }
}
