namespace Typstium.UnitTests.CommandLine;

public class TypstArgumentsTests
{
    [Test]
    public async Task Builder_methods_return_new_instances()
    {
        var empty = TypstArguments.Empty;
        var one = empty.Add("compile");
        await Assert.That(empty.Count).IsEqualTo(0);
        await Assert.That(one.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Switches_become_two_items_and_secrets_are_redacted()
    {
        var args = TypstArguments.Empty
            .AddSwitch("--root", "C:\\work dir")
            .AddSwitchSecret("--input", "license={\"id\":1}")
            .AddSecret("token");

        await Assert.That(args.ToValues()).IsEquivalentTo(new[] { "--root", "C:\\work dir", "--input", "license={\"id\":1}", "token" });
        await Assert.That(args.RenderSafe()).IsEqualTo("--root \"C:\\work dir\" --input [REDACTED] [REDACTED]");
        await Assert.That(args.Render()).Contains("license=");
    }

    [Test]
    public async Task Insert_remove_set_and_index_of()
    {
        var args = TypstArguments.Of("compile", "--root", "a", "main.typ");
        var index = args.IndexOf("--root");
        var changed = args.SetItem(index + 1, TypstArgument.Plain("b")).RemoveAt(0).Insert(0, TypstArgument.Plain("c"));
        await Assert.That(index).IsEqualTo(1);
        await Assert.That(changed.ToValues()).IsEquivalentTo(new[] { "c", "--root", "b", "main.typ" });
    }

    [Test]
    public async Task AddSwitch_requires_a_dash_prefixed_name()
    {
        var exception = Assert.Throws<ArgumentException>(() => TypstArguments.Empty.AddSwitch("root", "x"));
        await Assert.That(exception!.ParamName).IsEqualTo("name");
    }

    [Test]
    public async Task Equality_is_by_sequence()
    {
        var left = TypstArguments.Of("a", "b");
        var right = TypstArguments.Empty.Add("a").Add("b");
        await Assert.That(left).IsEqualTo(right);
        await Assert.That(left.Equals(TypstArguments.Empty.Add("a").AddSecret("b"))).IsFalse();
    }
}
