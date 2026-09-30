using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using Shoko.Abstractions.Metadata;
using Shoko.Server.API.ModelBinders;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
/// Unit tests for <see cref="MetadataSourceModelBinder"/>, which binds route
/// and query input to registered sources only.
/// </summary>
public class MetadataSourceModelBinderTests
{
    private static async Task<DefaultModelBindingContext> Bind(string? text)
    {
        var query = text is null ? new Dictionary<string, StringValues>() : new Dictionary<string, StringValues> { ["source"] = text };
        var context = new DefaultModelBindingContext
        {
            ModelName = "source",
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(BindingSource.Query, new QueryCollection(query), CultureInfo.InvariantCulture),
        };

        await new MetadataSourceModelBinder().BindModelAsync(context);
        return context;
    }

    [Theory]
    [InlineData("anidb")]
    [InlineData("AniDB")]
    [InlineData(" ANIDB ")]
    public async Task BindsARegisteredSourceByValueIgnoringCase(string text)
    {
        var context = await Bind(text);

        Assert.True(context.Result.IsModelSet);
        Assert.Same(MetadataSource.AniDB, context.Result.Model);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Theory]
    [InlineData("themoviedb", "tmdb")]
    [InlineData("LocallyGenerated", "generated")]
    [InlineData("locally-generated", "generated")]
    [InlineData("LOCALLY_GENERATED", "generated")]
    public async Task BindsARegisteredSourceByAliasIgnoringCase(string text, string value)
        => Assert.Same(MetadataSource.Get(value), (await Bind(text)).Result.Model);

    [Theory]
    [InlineData("TEST_PLUGIN")]
    [InlineData("test_plugin")]
    public async Task BindsAPluginSourceReadingUnderscoresAsHyphens(string text)
        => Assert.Same(TestSources.Plugin, (await Bind(text)).Result.Model);

    [Theory]
    [InlineData("msmb-unregistered")]
    [InlineData("Locally Generated")]
    [InlineData("not valid")]
    public async Task RefusesTextThatNamesNoRegisteredSource(string text)
    {
        var context = await Bind(text);

        Assert.False(context.Result.IsModelSet);
        Assert.Single(context.ModelState["source"]!.Errors);
    }

    [Fact]
    public async Task NeverHandsOutAnUnregisteredSource()
    {
        await Bind("msmb-never");

        // A text handed out as a source of its own could no longer become an alias.
        MetadataSource.Register("MsmbOwner", "msmb-owner", ["msmb-never"]);
    }

    [Fact]
    public async Task LeavesAMissingValueUnbound()
    {
        var context = await Bind(null);

        Assert.False(context.Result.IsModelSet);
        Assert.Equal(0, context.ModelState.ErrorCount);
    }

    [Fact]
    public async Task BindsAnEmptyValueAsNull()
    {
        var context = await Bind("");

        Assert.True(context.Result.IsModelSet);
        Assert.Null(context.Result.Model);
    }

    #region Provider

    private sealed class ProviderContext(BindingInfo bindingInfo, Type modelType) : ModelBinderProviderContext
    {
        private static readonly EmptyModelMetadataProvider _metadataProvider = new();

        public override BindingInfo BindingInfo => bindingInfo;

        public override ModelMetadata Metadata => _metadataProvider.GetMetadataForType(modelType);

        public override IModelMetadataProvider MetadataProvider => _metadataProvider;

        public override IModelBinder CreateBinder(ModelMetadata metadata)
            => throw new NotSupportedException();
    }

    private static BindingSource? GetBindingSource(string? name)
        => name is null ? null : (BindingSource)typeof(BindingSource).GetField(name)!.GetValue(null)!;

    [Theory]
    [InlineData(null)]
    [InlineData("ModelBinding")]
    [InlineData("Path")]
    [InlineData("Query")]
    [InlineData("Form")]
    public void ProviderBindsASourceFromAValue(string? bindingSource)
    {
        var context = new ProviderContext(new BindingInfo { BindingSource = GetBindingSource(bindingSource) }, typeof(MetadataSource));

        Assert.IsType<MetadataSourceModelBinder>(new MetadataSourceModelBinderProvider().GetBinder(context));
    }

    [Theory]
    [InlineData("Body")]
    [InlineData("Header")]
    [InlineData("Services")]
    [InlineData("Special")]
    public void ProviderLeavesOtherBindingSourcesToTheirOwnProviders(string bindingSource)
    {
        // A header comes back through this provider, as the header binder's inner binder, with ModelBinding as its source.
        var context = new ProviderContext(new BindingInfo { BindingSource = GetBindingSource(bindingSource) }, typeof(MetadataSource));

        Assert.Null(new MetadataSourceModelBinderProvider().GetBinder(context));
    }

    [Fact]
    public void ProviderLeavesAnExplicitBinderAlone()
    {
        var context = new ProviderContext(new BindingInfo { BinderType = typeof(MetadataSourceModelBinder) }, typeof(MetadataSource));

        Assert.Null(new MetadataSourceModelBinderProvider().GetBinder(context));
    }

    [Fact]
    public void ProviderLeavesOtherTypesAlone()
        => Assert.Null(new MetadataSourceModelBinderProvider().GetBinder(new ProviderContext(new BindingInfo(), typeof(string))));

    #endregion
}
