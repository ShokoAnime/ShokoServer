using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Enums;
using Shoko.Server.API.ModelBinders;
using Xunit;

namespace Shoko.Tests.API;

/// <summary>
///   Unit tests for <see cref="EnumMemberModelBinder"/>, which binds an enum by
///   the name it is written as in a body as well as by its C# name.
/// </summary>
public class EnumMemberModelBinderTests
{
    private static async Task<DefaultModelBindingContext> Bind(string text)
    {
        var context = new DefaultModelBindingContext
        {
            ModelName = "reactiveEventType",
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(ReactiveEventType)),
            ModelState = new ModelStateDictionary(),
            ValueProvider = new QueryStringValueProvider(
                BindingSource.Query,
                new QueryCollection(new Dictionary<string, StringValues> { ["reactiveEventType"] = text }),
                CultureInfo.InvariantCulture
            ),
        };

        await new EnumMemberModelBinder().BindModelAsync(context);
        return context;
    }

    [Theory]
    [InlineData("view-changed", ReactiveEventType.ViewChanged)]
    [InlineData("new-value", ReactiveEventType.NewValue)]
    [InlineData("NewValue", ReactiveEventType.NewValue)]
    [InlineData("viewchanged", ReactiveEventType.ViewChanged)]
    public async Task BindsTheWireNameAndTheCSharpName(string text, ReactiveEventType expected)
        => Assert.Equal(expected, (await Bind(text)).Result.Model);

    [Fact]
    public async Task RefusesTextThatNamesNoMember()
    {
        var context = await Bind("sideways");

        Assert.False(context.Result.IsModelSet);
        Assert.Single(context.ModelState["reactiveEventType"]!.Errors);
    }

    [Fact]
    public void ReactiveEventType_IsWrittenByItsWireName()
    {
        ReactiveEventType[] events = [ReactiveEventType.Edited, ReactiveEventType.ViewChanged];

        Assert.Equal("""["edited","view-changed"]""", JsonConvert.SerializeObject(events));
        Assert.Equal("""["edited","view-changed"]""", System.Text.Json.JsonSerializer.Serialize(events));
    }
}
