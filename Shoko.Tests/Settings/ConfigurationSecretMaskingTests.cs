using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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

        public Nested Nested { get; set; } = new();

        public List<IdentifiedProvider> Identified { get; set; } = [];

        public List<AnonymousProvider> Anonymous { get; set; } = [];

        public Dictionary<string, AnonymousProvider> Keyed { get; set; } = [];
    }

    private sealed class Nested
    {
        [PasswordPropertyText]
        public string? ApiKey { get; set; }
    }

    private sealed class IdentifiedProvider
    {
        [Key]
        public string Name { get; set; } = string.Empty;

        [PasswordPropertyText]
        public string? ApiKey { get; set; }
    }

    private sealed class AnonymousProvider
    {
        public string Label { get; set; } = string.Empty;

        [PasswordPropertyText]
        public string? ApiKey { get; set; }
    }

    private static JToken Parse(string json)
        => JToken.Parse(json);

    #endregion

    #region Masking

    [Fact]
    public void Mask_ReplacesASetSecret()
    {
        var masked = ConfigurationSecrets.Mask(Parse("""{"Username":"some-user","Password":"hunter2"}"""), typeof(Credentials));

        Assert.Equal("some-user", masked["Username"]!.Value<string>());
        Assert.Equal(ConfigurationSecrets.Sentinel, masked["Password"]!.Value<string>());
    }

    [Theory]
    [InlineData("null", JTokenType.Null)]
    [InlineData("\"\"", JTokenType.String)]
    public void Mask_LeavesAnUnsetSecretAlone(string value, JTokenType expected)
    {
        // "No password configured" has to stay distinguishable from "password
        // withheld", or a client cannot tell whether there is anything to clear.
        var masked = ConfigurationSecrets.Mask(Parse($$"""{"Password":{{value}}}"""), typeof(Credentials));

        Assert.Equal(expected, masked["Password"]!.Type);
        Assert.NotEqual(ConfigurationSecrets.Sentinel, masked["Password"]!.Value<string>());
    }

    [Fact]
    public void Mask_DoesNotModifyTheDocumentItWasGiven()
    {
        var original = Parse("""{"Password":"hunter2"}""");

        ConfigurationSecrets.Mask(original, typeof(Credentials));

        Assert.Equal("hunter2", original["Password"]!.Value<string>());
    }

    [Fact]
    public void Mask_ReachesNestedObjectsListsAndDictionaries()
    {
        var masked = ConfigurationSecrets.Mask(Parse("""
            {
                "Nested": { "ApiKey": "nested-key" },
                "Identified": [ { "Name": "a", "ApiKey": "identified-key" } ],
                "Anonymous": [ { "Label": "b", "ApiKey": "anonymous-key" } ],
                "Keyed": { "c": { "Label": "c", "ApiKey": "keyed-key" } }
            }
            """), typeof(Credentials));

        Assert.Equal(ConfigurationSecrets.Sentinel, masked["Nested"]!["ApiKey"]!.Value<string>());
        Assert.Equal(ConfigurationSecrets.Sentinel, masked["Identified"]![0]!["ApiKey"]!.Value<string>());
        Assert.Equal(ConfigurationSecrets.Sentinel, masked["Anonymous"]![0]!["ApiKey"]!.Value<string>());
        Assert.Equal(ConfigurationSecrets.Sentinel, masked["Keyed"]!["c"]!["ApiKey"]!.Value<string>());
    }

    #endregion

    #region The four incoming shapes

    [Fact]
    public void Restore_KeepsTheStoredValue_WhenThePropertyIsAbsent()
    {
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse("""{"Username":"some-user"}"""),
            Parse("""{"Username":"some-user","Password":"hunter2"}"""),
            typeof(Credentials));

        Assert.Empty(errors);
        Assert.Equal("hunter2", restored["Password"]!.Value<string>());
    }

    [Fact]
    public void Restore_KeepsTheStoredValue_WhenTheSentinelRoundTripsBack()
    {
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Username":"some-user","Password":"{{ConfigurationSecrets.Sentinel}}"}"""),
            Parse("""{"Username":"some-user","Password":"hunter2"}"""),
            typeof(Credentials));

        Assert.Empty(errors);
        Assert.Equal("hunter2", restored["Password"]!.Value<string>());
    }

    [Fact]
    public void Restore_TakesTheNewValue_WhenTheSecretChanged()
    {
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse("""{"Password":"hunter3"}"""),
            Parse("""{"Password":"hunter2"}"""),
            typeof(Credentials));

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
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Password":{{value}}}"""),
            Parse("""{"Password":"hunter2"}"""),
            typeof(Credentials));

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
        var (_, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Password":"{{ConfigurationSecrets.Sentinel}}"}"""),
            current is null ? null : Parse(current),
            typeof(Credentials));

        var error = Assert.Single(errors);
        Assert.Equal("Password", error.Key);
        Assert.Contains(ConfigurationSecrets.Sentinel, Assert.Single(error.Value));
    }

    #endregion

    #region Nesting

    [Fact]
    public void Restore_ReachesIntoANestedObject()
    {
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Nested": {"ApiKey": "{{ConfigurationSecrets.Sentinel}}"} }"""),
            Parse("""{"Nested":{"ApiKey":"nested-key"}}"""),
            typeof(Credentials));

        Assert.Empty(errors);
        Assert.Equal("nested-key", restored["Nested"]!["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_ReportsANestedFailureUnderItsFullPath()
    {
        var (_, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Nested": {"ApiKey": "{{ConfigurationSecrets.Sentinel}}"} }"""),
            null,
            typeof(Credentials));

        Assert.Equal("Nested.ApiKey", Assert.Single(errors).Key);
    }

    #endregion

    #region Collections

    [Fact]
    public void Restore_PairsListElementsByTheirKey_NotTheirPosition()
    {
        // Reordering the list must not hand one element's secret to another.
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse($$"""
                {"Identified":[
                    {"Name":"beta","ApiKey":"{{ConfigurationSecrets.Sentinel}}"},
                    {"Name":"alpha","ApiKey":"{{ConfigurationSecrets.Sentinel}}"}
                ]}
                """),
            Parse("""
                {"Identified":[
                    {"Name":"alpha","ApiKey":"alpha-key"},
                    {"Name":"beta","ApiKey":"beta-key"}
                ]}
                """),
            typeof(Credentials));

        Assert.Empty(errors);
        Assert.Equal("beta-key", restored["Identified"]![0]!["ApiKey"]!.Value<string>());
        Assert.Equal("alpha-key", restored["Identified"]![1]!["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_RejectsTheSentinelOnANewListElement()
    {
        var (_, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Identified":[{"Name":"gamma","ApiKey":"{{ConfigurationSecrets.Sentinel}}"}]}"""),
            Parse("""{"Identified":[{"Name":"alpha","ApiKey":"alpha-key"}]}"""),
            typeof(Credentials));

        Assert.Equal("Identified[0].ApiKey", Assert.Single(errors).Key);
    }

    [Fact]
    public void Restore_PairsDictionaryEntriesByTheirKey()
    {
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Keyed": {"one": {"ApiKey": "{{ConfigurationSecrets.Sentinel}}"} } }"""),
            Parse("""{"Keyed":{"two":{"ApiKey":"two-key"},"one":{"ApiKey":"one-key"}}}"""),
            typeof(Credentials));

        Assert.Empty(errors);
        Assert.Equal("one-key", restored["Keyed"]!["one"]!["ApiKey"]!.Value<string>());
    }

    [Fact]
    public void Restore_RefusesAMaskedSecretInAListWithoutAKey()
    {
        // There is no way to tell which stored element this one used to be, and
        // guessing by position would reassign secrets on a reorder. Refuse and
        // say so instead.
        var (_, errors) = ConfigurationSecrets.Restore(
            Parse($$"""{"Anonymous":[{"Label":"b","ApiKey":"{{ConfigurationSecrets.Sentinel}}"}]}"""),
            Parse("""{"Anonymous":[{"Label":"b","ApiKey":"anonymous-key"}]}"""),
            typeof(Credentials));

        var error = Assert.Single(errors);
        Assert.Equal("Anonymous[0].ApiKey", error.Key);
        Assert.Contains("[Key]", Assert.Single(error.Value));
    }

    [Fact]
    public void Restore_LetsARealValueThroughAListWithoutAKey()
    {
        var (restored, errors) = ConfigurationSecrets.Restore(
            Parse("""{"Anonymous":[{"Label":"b","ApiKey":"anonymous-key"}]}"""),
            null,
            typeof(Credentials));

        Assert.Empty(errors);
        Assert.Equal("anonymous-key", restored["Anonymous"]![0]!["ApiKey"]!.Value<string>());
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

        Assert.True(JToken.DeepEquals(original, ConfigurationSecrets.Mask(original, typeof(NoSecrets))));
    }

    private sealed class NoSecrets
    {
        public string Label { get; set; } = string.Empty;
    }

    #endregion
}
