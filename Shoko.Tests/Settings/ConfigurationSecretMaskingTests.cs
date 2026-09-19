using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Config;
using Xunit;

namespace Shoko.Tests.Settings;

/// <summary>
/// Covers <see cref="ConfigurationSecrets"/> on its own: what a secret looks
/// like on the way out, and which of the four incoming shapes means what on the
/// way back in.
/// </summary>
public class ConfigurationSecretMaskingTests
{
    #region Fixtures

    private sealed class Credentials : IConfiguration
    {
        public string? Username { get; set; }

        [PasswordPropertyText]
        public string? Password { get; set; }

        [DataType(DataType.Password)]
        public string? Token { get; set; }

        public Nested Nested { get; set; } = new();

        public List<Provider> Providers { get; set; } = [];

        public Dictionary<string, Provider> Keyed { get; set; } = [];
    }

    private sealed class Nested
    {
        [PasswordPropertyText]
        public string? ApiKey { get; set; }
    }

    private sealed class Provider
    {
        // A display label, like the WebUI's [Key]: not unique, and free to change.
        public string Label { get; set; } = string.Empty;

        [PasswordPropertyText]
        public string? ApiKey { get; set; }

        [PasswordPropertyText]
        public string? Token { get; set; }
    }

    private static readonly byte[] _key = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];

    private static readonly byte[] _otherKey = [.. Enumerable.Range(101, 32).Select(i => (byte)i)];

    private static JToken Parse(string json)
        => JToken.Parse(json);

    private static JToken Mask(string json, byte[]? key = null)
        => ConfigurationSecrets.Mask(Parse(json), typeof(Credentials), key ?? _key);

    private static (JToken Token, IReadOnlyDictionary<string, IReadOnlyList<string>> Errors) Restore(JToken incoming, string? current)
        => ConfigurationSecrets.Restore(incoming, current is null ? null : Parse(current), typeof(Credentials), _key);

    private static (JToken Token, IReadOnlyDictionary<string, IReadOnlyList<string>> Errors) Restore(string incoming, string? current)
        => Restore(Parse(incoming), current);

    #endregion

    #region Masking

    [Fact]
    public void Mask_ReplacesASecretMarkedWithADataType()
    {
        // The schema generator renders a password element for either marker, so
        // masking has to follow both; this one used to go out in plaintext.
        var masked = Mask("""{"Username":"me","Token":"t0ken"}""");

        Assert.True(ConfigurationSecrets.IsMasked(masked["Token"]!.Value<string>()));
        Assert.Equal("me", masked["Username"]!.Value<string>());
    }

    [Fact]
    public void Restore_KeepsAStoredSecretMarkedWithADataType()
    {
        var (restored, errors) = Restore(Mask("""{"Token":"t0ken"}"""), """{"Token":"t0ken"}""");

        Assert.Empty(errors);
        Assert.Equal("t0ken", restored["Token"]!.Value<string>());
    }

    [Fact]
    public void Mask_ReplacesASetSecret()
    {
        var masked = Mask("""{"Username":"some-user","Password":"hunter2"}""");

        Assert.Equal("some-user", masked["Username"]!.Value<string>());
        Assert.True(ConfigurationSecrets.IsMasked(masked["Password"]!.Value<string>()));
        Assert.DoesNotContain("hunter2", masked.ToString());
    }

    [Fact]
    public void Mask_GivesEqualSecretsEqualMasks_UnderOneKeyOnly()
    {
        var masked = Mask("""{"Password":"hunter2","Token":"hunter2"}""");
        var elsewhere = Mask("""{"Password":"hunter2"}""", _otherKey);

        Assert.Equal(masked["Password"]!.Value<string>(), masked["Token"]!.Value<string>());
        Assert.NotEqual(masked["Password"]!.Value<string>(), elsewhere["Password"]!.Value<string>());
    }

    [Theory]
    [InlineData("null", JTokenType.Null)]
    [InlineData("\"\"", JTokenType.String)]
    public void Mask_LeavesAnUnsetSecretAlone(string value, JTokenType expected)
    {
        // "No password configured" has to stay distinguishable from "password
        // withheld", or a client cannot tell whether there is anything to clear.
        var masked = Mask($$"""{"Password":{{value}}}""");

        Assert.Equal(expected, masked["Password"]!.Type);
        Assert.False(ConfigurationSecrets.IsMasked(masked["Password"]!.Value<string>()));
    }

    [Fact]
    public void Mask_DoesNotModifyTheDocumentItWasGiven()
    {
        var original = Parse("""{"Password":"hunter2"}""");

        ConfigurationSecrets.Mask(original, typeof(Credentials), _key);

        Assert.Equal("hunter2", original["Password"]!.Value<string>());
    }

    [Fact]
    public void Mask_ReachesNestedObjectsListsAndDictionaries()
    {
        var masked = Mask("""
            {
                "Nested": { "ApiKey": "nested-key" },
                "Providers": [ { "Label": "b", "ApiKey": "list-key" } ],
                "Keyed": { "c": { "Label": "c", "ApiKey": "keyed-key" } }
            }
            """);

        Assert.True(ConfigurationSecrets.IsMasked(masked["Nested"]!["ApiKey"]!.Value<string>()));
        Assert.True(ConfigurationSecrets.IsMasked(masked["Providers"]![0]!["ApiKey"]!.Value<string>()));
        Assert.True(ConfigurationSecrets.IsMasked(masked["Keyed"]!["c"]!["ApiKey"]!.Value<string>()));
    }

    #endregion

    #region The four incoming shapes

    [Fact]
    public void Restore_KeepsTheStoredValue_WhenThePropertyIsAbsent()
    {
        var (restored, errors) = Restore("""{"Username":"some-user"}""", """{"Username":"some-user","Password":"hunter2"}""");

        Assert.Empty(errors);
        Assert.Equal("hunter2", restored["Password"]!.Value<string>());
    }

    [Fact]
    public void Restore_KeepsTheStoredValue_WhenTheMaskedValueRoundTripsBack()
    {
        const string stored = """{"Username":"some-user","Password":"hunter2"}""";
        var (restored, errors) = Restore(Mask(stored), stored);

        Assert.Empty(errors);
        Assert.Equal("hunter2", restored["Password"]!.Value<string>());
    }

    [Fact]
    public void Restore_AcceptsTheBareSentinel_OutsideAList()
    {
        var (restored, errors) = Restore(
            $$"""{"Password":"{{ConfigurationSecrets.Sentinel}}"}""",
            """{"Password":"hunter2"}""");

        Assert.Empty(errors);
        Assert.Equal("hunter2", restored["Password"]!.Value<string>());
    }

    [Fact]
    public void Restore_TakesTheNewValue_WhenTheSecretChanged()
    {
        var (restored, errors) = Restore("""{"Password":"hunter3"}""", """{"Password":"hunter2"}""");

        Assert.Empty(errors);
        Assert.Equal("hunter3", restored["Password"]!.Value<string>());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void Restore_ClearsTheSecret_WhenItIsExplicitlyEmptied(string value)
    {
        // Clearing has to stay possible and has to stay distinguishable from not
        // touching the property, which is the whole reason the restore pass is a
        // merge and not a converter that skips the sentinel.
        var (restored, errors) = Restore($$"""{"Password":{{value}}}""", """{"Password":"hunter2"}""");

        Assert.Empty(errors);
        Assert.True(restored["Password"]!.Type is JTokenType.Null || restored["Password"]!.Value<string>() is "");
    }

    #endregion

    #region Rejecting the sentinel as a value

    [Theory]
    [InlineData(null)]
    [InlineData("""{"Password":null}""")]
    [InlineData("""{"Password":""}""")]
    public void Restore_RejectsTheSentinel_WhenThereIsNothingStoredBehindIt(string? current)
    {
        // Without this, a user whose real password happens to be the sentinel
        // would silently keep the old one and be locked out with no diagnostic.
        var (_, errors) = Restore($$"""{"Password":"{{ConfigurationSecrets.Sentinel}}"}""", current);

        var error = Assert.Single(errors);
        Assert.Equal("Password", error.Key);
        Assert.Contains(ConfigurationSecrets.Sentinel, Assert.Single(error.Value));
    }

    #endregion

    #region Nesting

    [Fact]
    public void Restore_ReachesIntoANestedObject()
    {
        const string stored = """{"Nested":{"ApiKey":"nested-key"}}""";
        var (restored, errors) = Restore(Mask(stored), stored);

        Assert.Empty(errors);
        Assert.Equal("nested-key", restored["Nested"]!["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_ReportsANestedFailureUnderItsFullPath()
    {
        var (_, errors) = Restore(Mask("""{"Nested":{"ApiKey":"nested-key"}}"""), null);

        Assert.Equal("Nested.ApiKey", Assert.Single(errors).Key);
    }

    #endregion

    #region Collections

    private const string TwoProviders = """
        {"Providers":[
            {"Label":"Main","ApiKey":"alpha-key"},
            {"Label":"Main","ApiKey":"beta-key"}
        ]}
        """;

    [Fact]
    public void Restore_FindsListSecretsByFingerprint_AcrossAReorder()
    {
        // Reordering the list must not hand one element's secret to another.
        var masked = Mask(TwoProviders);
        var providers = (JArray)masked["Providers"]!;
        var first = providers[0];
        first.Remove();
        providers.Add(first);

        var (restored, errors) = Restore(masked, TwoProviders);

        Assert.Empty(errors);
        Assert.Equal("beta-key", restored["Providers"]![0]!["ApiKey"]!.Value<string>());
        Assert.Equal("alpha-key", restored["Providers"]![1]!["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_KeepsSecretsApart_WhenTheLabelsCollide()
    {
        // Both elements share a label, so nothing but the fingerprint can tell
        // them apart; neither may end up with the other's secret.
        var (restored, errors) = Restore(Mask(TwoProviders), TwoProviders);

        Assert.Empty(errors);
        Assert.Equal("alpha-key", restored["Providers"]![0]!["ApiKey"]!.Value<string>());
        Assert.Equal("beta-key", restored["Providers"]![1]!["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_KeepsTheSecret_WhenAnElementIsRelabelledOrADeleteShiftsIt()
    {
        var masked = Mask(TwoProviders);
        var providers = (JArray)masked["Providers"]!;
        providers[0].Remove();
        providers[0]!["Label"] = "Renamed";

        var (restored, errors) = Restore(masked, TwoProviders);

        Assert.Empty(errors);
        Assert.Equal("beta-key", Assert.Single(restored["Providers"]!)["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_RejectsTheBareSentinel_InsideAList()
    {
        var (_, errors) = Restore(
            $$"""{"Providers":[{"Label":"b","ApiKey":"{{ConfigurationSecrets.Sentinel}}"}]}""",
            """{"Providers":[{"Label":"b","ApiKey":"list-key"}]}""");

        Assert.Equal("Providers[0].ApiKey", Assert.Single(errors).Key);
    }

    [Fact]
    public void Restore_RejectsAMaskedValue_ThatMatchesNothingStored()
    {
        // Masked under another install's key, so no stored secret has this
        // fingerprint.
        var (_, errors) = Restore(
            Mask("""{"Providers":[{"Label":"b","ApiKey":"list-key"}]}""", _otherKey),
            """{"Providers":[{"Label":"b","ApiKey":"list-key"}]}""");

        Assert.Equal("Providers[0].ApiKey", Assert.Single(errors).Key);
    }

    [Fact]
    public void Restore_DoesNotMoveASecretOntoAnotherProperty()
    {
        const string stored = """{"Providers":[{"Label":"b","ApiKey":"list-key"}]}""";
        var masked = Mask(stored);
        var element = masked["Providers"]![0]!;
        element["Token"] = element["ApiKey"]!.DeepClone();

        var (_, errors) = Restore(masked, stored);

        Assert.Equal("Providers[0].Token", Assert.Single(errors).Key);
    }

    [Fact]
    public void Restore_PairsDictionaryEntriesByTheirKey()
    {
        var (restored, errors) = Restore(
            $$"""{"Keyed": {"one": {"ApiKey": "{{ConfigurationSecrets.Sentinel}}"} } }""",
            """{"Keyed":{"two":{"ApiKey":"two-key"},"one":{"ApiKey":"one-key"}}}""");

        Assert.Empty(errors);
        Assert.Equal("one-key", restored["Keyed"]!["one"]!["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_LetsARealValueThroughAList()
    {
        var (restored, errors) = Restore("""{"Providers":[{"Label":"b","ApiKey":"list-key"}]}""", null);

        Assert.Empty(errors);
        Assert.Equal("list-key", restored["Providers"]![0]!["ApiKey"]!.Value<string>());
    }

    #endregion

    #region Types without secrets

    [Fact]
    public void ContainsSecrets_SeesThroughNestingAndCollections()
    {
        Assert.True(ConfigurationSecrets.ContainsSecrets(typeof(Credentials)));
        Assert.True(ConfigurationSecrets.ContainsSecrets(typeof(Nested)));
        Assert.False(ConfigurationSecrets.ContainsSecrets(typeof(NoSecrets)));
    }

    [Fact]
    public void Mask_LeavesATypeWithoutSecretsAlone()
    {
        var original = Parse("""{"Label":"nothing to hide"}""");

        Assert.True(JToken.DeepEquals(original, ConfigurationSecrets.Mask(original, typeof(NoSecrets), _key)));
    }

    private sealed class NoSecrets
    {
        public string Label { get; set; } = string.Empty;
    }

    #endregion
}
