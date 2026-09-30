using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server;
using Shoko.Server.API.Resolvers;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Unit tests for <see cref="ApiContractResolver"/>, which makes the API send a
/// <see cref="MetadataSource"/> as the old <c>DataSource</c> enum spelled it,
/// or as its value when the enum had no such source, and read registered
/// sources only.
/// </summary>
public class ApiContractResolverTests
{
    private static readonly JsonSerializerSettings _apiSettings = new() { ContractResolver = new ApiContractResolver() };

    private sealed class Dto
    {
        public MetadataSource? Source { get; set; }

        public List<MetadataSource> Order { get; set; } = [];

        public Dictionary<MetadataSource, List<string>> Linked { get; set; } = [];

        public IReadOnlyDictionary<MetadataSource, int> Counts { get; set; } = new Dictionary<MetadataSource, int>();
    }

    private static JObject SerializeForApi(object value)
        => JObject.Parse(JsonConvert.SerializeObject(value, _apiSettings));

    // A text handed out as a source of its own could no longer become an alias, so a free one proves nothing was handed out.
    private static void AssertNotHandedOut(string text)
        => MetadataSource.Register(text + "-owner", text + "-owner", [text]);

    #region Writing

    [Fact]
    public void Serialize_WritesTheOldSpellingOfAValue()
    {
        var json = SerializeForApi(new Dto { Source = MetadataSource.Generated, Order = [MetadataSource.AniDB, MetadataSource.User, MetadataSource.TMDB, MetadataSource.Shoko] });

        Assert.Equal("LocallyGenerated", json["Source"]!.Value<string>());
        Assert.Equal(new[] { "AniDB", "User", "TMDB", "Shoko" }, json["Order"]!.Values<string>());
    }

    [Fact]
    public void Serialize_WritesTheOldSpellingOfASourceHeldAsAnObject()
    {
        // Queue job details are object-typed, so the runtime type picks the converter.
        var json = SerializeForApi(new Dictionary<string, object> { ["Source"] = MetadataSource.TMDB, ["Resource ID"] = "/a.jpg" });

        Assert.Equal("TMDB", json["Source"]!.Value<string>());
    }

    [Fact]
    public void Serialize_WritesTheOldSpellingOfADictionaryKey()
    {
        var json = SerializeForApi(new Dto
        {
            Linked = new() { [MetadataSource.AniDB] = ["1"], [MetadataSource.Generated] = ["2"] },
            Counts = new Dictionary<MetadataSource, int> { [MetadataSource.Shoko] = 3 },
        });

        Assert.Equal(new[] { "AniDB", "LocallyGenerated" }, ((JObject)json["Linked"]!).Properties().Select(property => property.Name));
        Assert.Equal(new[] { "Shoko" }, ((JObject)json["Counts"]!).Properties().Select(property => property.Name));
    }

    [Fact]
    public void Serialize_WritesTheOldSpellingOfAPluginSourceTheOldEnumHad()
    {
        var anilist = TestSources.AniList;
        var json = SerializeForApi(new Dto { Source = anilist, Linked = new() { [anilist] = ["1"] } });

        Assert.Equal("AniList", json["Source"]!.Value<string>());
        Assert.Equal("AniList", ((JObject)json["Linked"]!).Properties().Single().Name);
    }

    [Fact]
    public void Serialize_WritesTheValueOfANewSource_NeverItsName()
    {
        var registered = MetadataSource.Register("Api Resolver Named", "api-resolver-named");
        var unregistered = MetadataSource.Parse("api-resolver-test-source");
        // The old enum had FanartTV, but nothing registers it, so its old spelling would not read back.
        var fanart = MetadataSource.Parse("fanart-tv");
        var json = SerializeForApi(new Dto { Source = registered, Order = [unregistered, fanart], Linked = new() { [registered] = ["x"], [fanart] = ["y"] } });

        Assert.Equal("api-resolver-named", json["Source"]!.Value<string>());
        Assert.Equal(new[] { "api-resolver-test-source", "fanart-tv" }, json["Order"]!.Values<string>());
        Assert.Equal(new[] { "api-resolver-named", "fanart-tv" }, ((JObject)json["Linked"]!).Properties().Select(property => property.Name));
    }

    [Fact]
    public void Serialize_WithoutTheResolver_StillWritesTheValue()
    {
        var dto = new Dto { Source = MetadataSource.TMDB, Linked = new() { [MetadataSource.Generated] = ["1"] } };

        var json = JObject.Parse(JsonConvert.SerializeObject(dto));

        Assert.Equal("tmdb", json["Source"]!.Value<string>());
        Assert.Equal("generated", ((JObject)json["Linked"]!).Properties().Single().Name);
    }

    [Fact]
    public void Serialize_TheEmptyEnumerableResolver_AlsoWritesTheOldSpelling()
    {
        var settings = new JsonSerializerSettings { ContractResolver = new EmitEmptyEnumerableInsteadOfNullResolver() };

        var json = JObject.Parse(JsonConvert.SerializeObject(new Dto { Source = MetadataSource.AniDB, Linked = new() { [MetadataSource.Generated] = [] } }, settings));

        Assert.Equal("AniDB", json["Source"]!.Value<string>());
        Assert.Equal("LocallyGenerated", ((JObject)json["Linked"]!).Properties().Single().Name);
    }

    #endregion

    #region Reading

    [Theory]
    [InlineData("tmdb", "tmdb")]
    [InlineData("TMDB", "tmdb")]
    [InlineData("themoviedb", "tmdb")]
    [InlineData("TheMovieDB", "tmdb")]
    [InlineData("LocallyGenerated", "generated")]
    public void Deserialize_AcceptsTheValueAliasOrOldSpellingIgnoringCase(string text, string value)
    {
        var json = $$$"""{"Source": "{{{text}}}", "Order": ["{{{text}}}"], "Linked": {"{{{text}}}": ["1"]}, "Counts": {"{{{text}}}": 2}}""";
        var expected = MetadataSource.Parse(value);

        var dto = JsonConvert.DeserializeObject<Dto>(json, _apiSettings)!;

        Assert.Equal(expected, dto.Source);
        Assert.Equal(new[] { expected }, dto.Order);
        Assert.Equal(new[] { expected }, dto.Linked.Keys);
        Assert.Equal(2, dto.Counts[expected]);
    }

    [Theory]
    [InlineData("TEST_PLUGIN")]
    [InlineData("test_plugin")]
    public void Deserialize_ReadsAnUnderscoreAsAHyphen(string text)
    {
        var json = $$$"""{"Source": "{{{text}}}", "Order": ["{{{text}}}"], "Linked": {"{{{text}}}": ["1"]}}""";

        var dto = JsonConvert.DeserializeObject<Dto>(json, _apiSettings)!;

        Assert.Same(TestSources.Plugin, dto.Source);
        Assert.Equal(new[] { TestSources.Plugin }, dto.Order);
        Assert.Equal(new[] { TestSources.Plugin }, dto.Linked.Keys);
    }

    [Fact]
    public void Deserialize_ReadsNull()
    {
        var dto = JsonConvert.DeserializeObject<Dto>("""{"Source": null, "Linked": null}""", _apiSettings)!;

        Assert.Null(dto.Source);
        Assert.Null(dto.Linked);
    }

    [Theory]
    [InlineData("api-resolver-unregistered-value")]
    [InlineData("Locally Generated")]
    [InlineData("FanartTV")]
    public void Deserialize_RefusesAValueThatNamesNoRegisteredSource(string text)
    {
        var json = $$$"""{"Source": "{{{text}}}"}""";

        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dto>(json, _apiSettings));
    }

    [Theory]
    [InlineData("api-resolver-unregistered-key")]
    [InlineData("Locally Generated")]
    public void Deserialize_RefusesADictionaryKeyThatNamesNoRegisteredSource(string text)
    {
        var json = $$$"""{"Linked": {"{{{text}}}": ["1"]}}""";

        Assert.Throws<JsonSerializationException>(() => JsonConvert.DeserializeObject<Dto>(json, _apiSettings));
    }

    [Fact]
    public void Deserialize_NeverHandsOutAnUnregisteredSource()
    {
        Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<Dto>("""{"Source": "api-resolver-never-value"}""", _apiSettings));
        Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<Dto>("""{"Order": ["api-resolver-never-item"]}""", _apiSettings));
        Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<Dto>("""{"Counts": {"api-resolver-never-key": 1}}""", _apiSettings));

        AssertNotHandedOut("api-resolver-never-value");
        AssertNotHandedOut("api-resolver-never-item");
        AssertNotHandedOut("api-resolver-never-key");
    }

    [Fact]
    public void Deserialize_WithoutTheResolver_StillReadsAnyValidValue()
    {
        var dto = JsonConvert.DeserializeObject<Dto>("""{"Source": "api-resolver-stored", "Linked": {"api-resolver-stored-key": []}}""")!;

        Assert.Equal("api-resolver-stored", dto.Source!.Value);
        Assert.Equal("api-resolver-stored-key", dto.Linked.Keys.Single().Value);
    }

    #endregion

    #region Settings

    [Fact]
    public void ForSettings_ReadsASourceWhosePluginIsNotLoaded()
    {
        var settings = new JsonSerializerSettings { ContractResolver = ApiContractResolver.ForSettings };
        var json = """{"Source": "fanart-tv", "Order": ["api-resolver-settings-item"], "Linked": {"api-resolver-settings-key": []}}""";

        var dto = JsonConvert.DeserializeObject<Dto>(json, settings)!;

        Assert.Equal("fanart-tv", dto.Source!.Value);
        Assert.Equal("api-resolver-settings-item", dto.Order.Single().Value);
        Assert.Equal("api-resolver-settings-key", dto.Linked.Keys.Single().Value);
    }

    [Fact]
    public void ForSettings_WritesAsTheApiDoes()
    {
        var settings = new JsonSerializerSettings { ContractResolver = ApiContractResolver.ForSettings };

        var json = JObject.Parse(JsonConvert.SerializeObject(new Dto { Source = MetadataSource.Generated, Linked = new() { [MetadataSource.AniDB] = [] } }, settings));

        Assert.Equal("LocallyGenerated", json["Source"]!.Value<string>());
        Assert.Equal("AniDB", ((JObject)json["Linked"]!).Properties().Single().Name);
    }

    #endregion
}
