using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Common;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   Comma-separated query values: known ones bind, an unknown one is the
///   caller's mistake.
/// </summary>
public class CommaDelimitedModelBinderTests
{
    private static async Task<ModelBindingContext> Bind(Type type, string value)
    {
        var context = new DefaultModelBindingContext
        {
            ModelName = "include",
            ModelState = new ModelStateDictionary(),
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(type),
            ValueProvider = new QueryStringValueProvider(
                BindingSource.Query,
                new QueryCollection(new Dictionary<string, StringValues> { ["include"] = value }),
                CultureInfo.InvariantCulture
            ),
        };
        await new CommaDelimitedModelBinder(NullLogger<CommaDelimitedModelBinder>.Instance).BindModelAsync(context);
        return context;
    }

    [Fact]
    public async Task KnownValues_Bind()
    {
        var context = await Bind(typeof(FileNonDefaultIncludeType[]), "MediaInfo, XRefs");

        Assert.True(context.Result.IsModelSet);
        Assert.Equal([FileNonDefaultIncludeType.MediaInfo, FileNonDefaultIncludeType.XRefs], (FileNonDefaultIncludeType[])context.Result.Model!);
    }

    [Theory]
    [InlineData(typeof(FileNonDefaultIncludeType[]))]
    [InlineData(typeof(HashSet<FileNonDefaultIncludeType>))]
    public async Task UnknownValue_FailsTheBinding(Type type)
    {
        var context = await Bind(type, "MediaInfo,Locations");

        Assert.False(context.Result.IsModelSet);
        Assert.False(context.ModelState.IsValid);
        Assert.Contains("Locations", context.ModelState["include"]!.Errors[0].ErrorMessage);
    }
}
