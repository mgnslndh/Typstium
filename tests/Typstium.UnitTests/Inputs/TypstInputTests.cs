using System.Text.Json.Serialization;

namespace Typstium.UnitTests.Inputs;

public partial class TypstInputTests
{
    private sealed record Model(string Name);

    [JsonSerializable(typeof(Model))]
    private sealed partial class ModelContext : JsonSerializerContext;

    private static TypstInputKey Key(string value) => TypstInputKey.Parse(value);

    [Test]
    [Arguments("edition")]
    [Arguments("with space")]
    [Arguments("-dash")]
    [Arguments("ünïcode")]
    public async Task Keys_accept_any_non_blank_text_without_equals_signs(string value)
    {
        await Assert.That(TypstInputKey.Parse(value).Value).IsEqualTo(value);
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    [Arguments("a=b")]
    public async Task Keys_reject_blank_text_and_equals_signs(string? value)
    {
        await Assert.That(TypstInputKey.TryParse(value, out _)).IsFalse();
        var exception = Assert.Throws<ArgumentException>(() => TypstInputKey.Parse(value!));
        await Assert.That(exception!.ParamName).IsEqualTo("value");
    }

    [Test]
    public async Task Keys_compare_ordinally()
    {
        await Assert.That(Key("a") == Key("a")).IsTrue();
        await Assert.That(Key("a") == Key("A")).IsFalse();
        await Assert.That(Key("a").GetHashCode()).IsEqualTo(Key("a").GetHashCode());
    }

    [Test]
    public async Task Json_inputs_use_camel_case_by_default_and_honour_type_info()
    {
        var reflection = TypstInput.FromJson(Key("data"), new Model("x"));
        var generated = TypstInput.FromJson(Key("data"), new Model("x"), ModelContext.Default.Model);

        await Assert.That(reflection.Value).IsEqualTo("{\"name\":\"x\"}");
        await Assert.That(generated.Value).IsEqualTo("{\"Name\":\"x\"}");
    }

    [Test]
    public async Task ToString_redacts_the_value()
    {
        var input = TypstInput.FromText(Key("license"), "secret");
        await Assert.That(input.ToString()).IsEqualTo("license=[REDACTED]");
    }

    [Test]
    public async Task Sets_enumerate_in_ordinal_key_order()
    {
        var inputs = TypstInputs.Of(TypstInput.FromText(Key("b"), "2"), TypstInput.FromText(Key("B"), "1"), TypstInput.FromText(Key("a"), "3"));

        await Assert.That(inputs.Select(input => input.Key.Value)).IsEquivalentTo(new[] { "B", "a", "b" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(inputs.ToString()).IsEqualTo("B=[REDACTED], a=[REDACTED], b=[REDACTED]");
    }

    [Test]
    public async Task Of_rejects_duplicate_keys_and_set_replaces()
    {
        var first = TypstInput.FromText(Key("k"), "1");
        var second = TypstInput.FromText(Key("k"), "2");

        var exception = Assert.Throws<ArgumentException>(() => TypstInputs.Of(first, second));
        var replaced = TypstInputs.Of(first).Set(second);

        await Assert.That(exception!.Message).Contains("more than once");
        await Assert.That(replaced.Count).IsEqualTo(1);
        await Assert.That(replaced[Key("k")]).IsEqualTo("2");
    }

    [Test]
    public async Task Sets_are_immutable_and_compare_by_content()
    {
        var empty = TypstInputs.Empty;
        var one = empty.Set(TypstInput.FromText(Key("k"), "v"));

        await Assert.That(empty.Count).IsEqualTo(0);
        await Assert.That(one.ContainsKey(Key("k"))).IsTrue();
        await Assert.That(one.Remove(Key("k"))).IsEqualTo(empty);
        await Assert.That(one).IsEqualTo(TypstInputs.Of(TypstInput.FromText(Key("k"), "v")));
        await Assert.That(one).IsNotEqualTo(TypstInputs.Of(TypstInput.FromText(Key("k"), "other")));
        await Assert.That(one.TryGetValue(Key("missing"), out _)).IsFalse();
        await Assert.That(Assert.Throws<KeyNotFoundException>(() => _ = one[Key("missing")])).IsNotNull();
    }
}
