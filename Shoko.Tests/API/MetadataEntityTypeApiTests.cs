using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Operations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.Resolvers;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Unit tests for how the API reads and writes a <see cref="MetadataEntityType"/>:
/// the model binder, <see cref="ApiContractResolver"/> and the JSON Patch key
/// check. Output is the old <c>DataEntityType</c> spelling, or the value for a
/// kind the old enum lacked; input takes a registered entity type only, by its
/// value or an alias, ignoring case.
/// </summary>
public class MetadataEntityTypeApiTests
{
    private static readonly JsonSerializerSettings _apiSettings = new() { ContractResolver = new ApiContractResolver() };

    private sealed class Dto
    {
        public MetadataEntityType? EntityType { get; set; }

        public List<MetadataEntityType> Order { get; set; } = [];

        public Dictionary<MetadataEntityType, int> Counts { get; set; } = [];
    }

    // A text handed out as a kind of its own could no longer become an alias, so a free one proves nothing was handed out.
    private static void AssertNotHandedOut(string text)
        => MetadataEntityType.Register(text + "-owner", text + "-owner", [text]);

    #region Model Binder

    private static async Task<DefaultModelBindingContext> Bind(string? text)
    {
        var query = text is null ? new Dictionary<string, StringValues>() : new Dictionary<string, StringValues> { ["entityType"] = text };
        var context = new DefaultModelBindingContext
        {
            ModelName = "entityType",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, new QueryCollection(query), CultureInfo.InvariantCulture),
        };

        await new MetadataEntityTypeModelBinder().BindModelAsync(context);
        return context;
    }

    [Theory]
    [InlineData("series", "series")]
    [InlineData(" Series ", "series")]
    [InlineData("Show", "series")]
    [InlineData("anime", "series")]
    [InlineData("Company", "studio")]
    [InlineData("channel", "channel")]
    [InlineData("STAFFMEMBER", "creator")]
    public async Task Binder_BindsARegisteredKindByValueOrAliasIgnoringCase(string text, string value)
    {
        var context = await Bind(text);

        Assert.True(context.Result.IsModelSet);
        Assert.Same(MetadataEntityType.Get(value), context.Result.Model);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Fact]
    public async Task Binder_ReadsAnUnderscoreAsAHyphen()
    {
        var entityType = MetadataEntityType.Register("MetapiUnderscore", "metapi-under-score");

        Assert.Same(entityType, (await Bind("METAPI_UNDER_SCORE")).Result.Model);
    }

    [Theory]
    [InlineData("metapi-unregistered")]
    [InlineData("Unknown")]
    [InlineData("not valid")]
    public async Task Binder_RefusesTextThatNamesNoRegisteredKind(string text)
    {
        var context = await Bind(text);

        Assert.False(context.Result.IsModelSet);
        Assert.Single(context.ModelState["entityType"]!.Errors);
    }

    [Fact]
    public async Task Binder_NeverHandsOutAnUnregisteredKind()
    {
        await Bind("metapi-binder-never");

        AssertNotHandedOut("metapi-binder-never");
    }

    [Fact]
    public async Task Binder_LeavesAMissingValueUnboundAndBindsAnEmptyOneAsNull()
    {
        var missing = await Bind(null);
        var empty = await Bind("");

        Assert.False(missing.Result.IsModelSet);
        Assert.Equal(0, missing.ModelState.ErrorCount);
        Assert.True(empty.Result.IsModelSet);
        Assert.Null(empty.Result.Model);
    }

    private sealed class ProviderContext(BindingInfo bindingInfo, Type modelType) : ModelBinderProviderContext
    {
        private static readonly EmptyModelMetadataProvider _metadataProvider = new();

        public override BindingInfo BindingInfo => bindingInfo;

        public override ModelMetadata Metadata => _metadataProvider.GetMetadataForType(modelType);

        public override IModelMetadataProvider MetadataProvider => _metadataProvider;

        public override IModelBinder CreateBinder(ModelMetadata metadata)
            => throw new NotSupportedException();
    }

    [Fact]
    public void BinderProvider_BindsAKindFromAValueOnly()
    {
        var provider = new MetadataEntityTypeModelBinderProvider();

        Assert.IsType<MetadataEntityTypeModelBinder>(provider.GetBinder(new ProviderContext(new BindingInfo(), typeof(MetadataEntityType))));
        Assert.IsType<MetadataEntityTypeModelBinder>(provider.GetBinder(new ProviderContext(new BindingInfo { BindingSource = BindingSource.Path }, typeof(MetadataEntityType))));
        Assert.IsType<MetadataEntityTypeModelBinder>(provider.GetBinder(new ProviderContext(new BindingInfo { BindingSource = BindingSource.Query }, typeof(MetadataEntityType))));
        Assert.Null(provider.GetBinder(new ProviderContext(new BindingInfo { BindingSource = BindingSource.Body }, typeof(MetadataEntityType))));
        Assert.Null(provider.GetBinder(new ProviderContext(new BindingInfo { BinderType = typeof(MetadataEntityTypeModelBinder) }, typeof(MetadataEntityType))));
        Assert.Null(provider.GetBinder(new ProviderContext(new BindingInfo(), typeof(MetadataSource))));
    }

    #endregion

    #region Contract Resolver

    [Fact]
    public void Serialize_WritesTheOldSpellingOfAValueAndADictionaryKey()
    {
        var json = JObject.Parse(JsonConvert.SerializeObject(new Dto
        {
            EntityType = MetadataEntityType.Collection,
            Order = [MetadataEntityType.Series, MetadataEntityType.Parse("metapi-dormant"), MetadataEntityType.Ordering, MetadataEntityType.Channel],
            Counts = new() { [MetadataEntityType.Studio] = 1, [MetadataEntityType.Creator] = 2 },
        }, _apiSettings));

        Assert.Equal("Franchise", json["EntityType"]!.Value<string>());
        Assert.Equal(new[] { "Show", "metapi-dormant", "ordering", "channel" }, json["Order"]!.Values<string>());
        Assert.Equal(new[] { "Studio", "Creator" }, ((JObject)json["Counts"]!).Properties().Select(property => property.Name));
    }

    [Theory]
    [InlineData("series")]
    [InlineData("SERIES")]
    [InlineData("Show")]
    [InlineData("anime")]
    public void Deserialize_AcceptsTheValueOrAliasIgnoringCase(string text)
    {
        var json = $$$"""{"EntityType": "{{{text}}}", "Order": ["{{{text}}}"], "Counts": {"{{{text}}}": 2}}""";

        var dto = JsonConvert.DeserializeObject<Dto>(json, _apiSettings)!;

        Assert.Same(MetadataEntityType.Series, dto.EntityType);
        Assert.Equal(new[] { MetadataEntityType.Series }, dto.Order);
        Assert.Equal(2, dto.Counts[MetadataEntityType.Series]);
    }

    [Fact]
    public void Deserialize_NeverHandsOutAnUnregisteredKind()
    {
        Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<Dto>("""{"EntityType": "metapi-never-value"}""", _apiSettings));
        Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<Dto>("""{"Order": ["metapi-never-item"]}""", _apiSettings));
        Assert.ThrowsAny<JsonException>(() => JsonConvert.DeserializeObject<Dto>("""{"Counts": {"metapi-never-key": 1}}""", _apiSettings));

        AssertNotHandedOut("metapi-never-value");
        AssertNotHandedOut("metapi-never-item");
        AssertNotHandedOut("metapi-never-key");
    }

    [Fact]
    public void ForSettings_ReadsAKindWhosePluginIsNotLoaded()
    {
        var settings = new JsonSerializerSettings { ContractResolver = ApiContractResolver.ForSettings };

        var dto = JsonConvert.DeserializeObject<Dto>("""{"EntityType": "metapi-settings-value", "Order": ["Library"]}""", settings)!;

        Assert.Equal("metapi-settings-value", dto.EntityType!.Value);
        Assert.Equal("library", dto.Order.Single().Value);
    }

    #endregion

    #region JSON Patch

    public sealed class PatchModel
    {
        public Dictionary<MetadataEntityType, int> Counts { get; set; } = [];
    }

    private static JsonPatchDocument<PatchModel> Patch(string path)
        => new([new Operation<PatchModel>("add", path, null, 1)], new ApiContractResolver());

    [Theory]
    [InlineData("/Counts/series")]
    [InlineData("/Counts/Show")]
    public void PatchKeys_AcceptARegisteredKind(string path)
    {
        var modelState = new ModelStateDictionary();

        Assert.True(MetadataSourcePatchKeys.Validate(Patch(path), modelState));
        Assert.True(modelState.IsValid);
    }

    [Fact]
    public void PatchKeys_RefuseAKeyThatNamesNoRegisteredKind()
    {
        var modelState = new ModelStateDictionary();

        Assert.False(MetadataSourcePatchKeys.Validate(Patch("/Counts/metapi-patch-unregistered"), modelState));
        Assert.Single(modelState[nameof(PatchModel)]!.Errors);
        AssertNotHandedOut("metapi-patch-unregistered");
    }

    #endregion
}
