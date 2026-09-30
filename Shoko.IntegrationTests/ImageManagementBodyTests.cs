using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shoko.Server.API.v3.Models.ImageManagement.Input;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Reads the image management request bodies through the started server's
/// own JSON input formatter and model validation, as a request would: an
/// explicit null is refused where a value is required, and a partial update
/// marks only the members it sends.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ImageManagementBodyTests(DatabaseMigrationFixture fixture)
{
    #region Harness

    /// <summary>
    /// Reads a body as MVC does for <c>[FromBody]</c>, then validates it.
    /// </summary>
    /// <typeparam name="T">The body type.</typeparam>
    /// <param name="json">The request body.</param>
    /// <returns>The body read, or <c>null</c>, and the model state.</returns>
    private async Task<(T? Body, ModelStateDictionary State)> Read<T>(string json) where T : class
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var services = fixture.Services;
        var formatter = services.GetRequiredService<IOptions<MvcOptions>>().Value.InputFormatters.OfType<NewtonsoftJsonInputFormatter>().First();
        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var state = new ModelStateDictionary();
        var context = new InputFormatterContext(httpContext, string.Empty, state, metadataProvider.GetMetadataForType(typeof(T)), (stream, encoding) => new StreamReader(stream, encoding));

        var result = await formatter.ReadAsync(context);
        var body = result.Model as T;
        if (!result.HasError && body is not null)
        {
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(), state);
            services.GetRequiredService<IObjectModelValidator>().Validate(actionContext, validationState: null, prefix: string.Empty, body);
        }

        return (body, state);
    }

    private static readonly string _imageID = Guid.NewGuid().ToString();

    #endregion

    #region Add Cross-Reference

    [Fact]
    public async Task AddCrossReference_ExplicitNull_IsRefused()
    {
        var (_, sourceState) = await Read<AddImageCrossReferenceBody>($$"""{"ImageID": "{{_imageID}}", "ImageType": "Primary", "Source": null}""");
        var (_, enabledState) = await Read<AddImageCrossReferenceBody>($$"""{"ImageID": "{{_imageID}}", "ImageType": "Primary", "IsEnabled": null}""");

        Assert.False(sourceState.IsValid);
        Assert.Contains(sourceState, entry => entry.Key.EndsWith("Source") && entry.Value!.Errors.Count > 0);
        Assert.False(enabledState.IsValid);
        Assert.Contains(enabledState, entry => entry.Key.EndsWith("IsEnabled") && entry.Value!.Errors.Count > 0);
    }

    #endregion

    #region Partial Updates

    [Fact]
    public async Task UpdateImage_MarksOnlyTheMembersSent()
    {
        var (omitted, omittedState) = await Read<UpdateImageBody>("""{"IsEnabled": true}""");
        var (cleared, clearedState) = await Read<UpdateImageBody>("""{"LanguageCode": null, "Width": 10, "Height": 20}""");

        Assert.True(omittedState.IsValid);
        Assert.Equal((false, false, false, false), (omitted!.HasWidthSet, omitted.HasHeightSet, omitted.HasLanguageCodeSet, omitted.HasCountryCodeSet));
        var omittedData = omitted.ToImageUpdateData();
        Assert.Equal((true, false, false, false), (omittedData.IsEnabled, omittedData.HasSizeSet, omittedData.HasLanguageCodeSet, omittedData.HasCountryCodeSet));

        Assert.True(clearedState.IsValid);
        Assert.Equal((true, true, true, false), (cleared!.HasWidthSet, cleared.HasHeightSet, cleared.HasLanguageCodeSet, cleared.HasCountryCodeSet));
        var clearedData = cleared.ToImageUpdateData();
        Assert.Equal((true, true, false), (clearedData.HasSizeSet, clearedData.HasLanguageCodeSet, clearedData.HasCountryCodeSet));
        Assert.Equal((10, 20, null), (clearedData.Width, clearedData.Height, clearedData.LanguageCode));
    }

    [Fact]
    public async Task UpdateCrossReference_MarksOnlyTheMembersSent()
    {
        var (omitted, omittedState) = await Read<UpdateImageCrossReferenceBody>("""{"IsPreferred": true}""");
        var (cleared, clearedState) = await Read<UpdateImageCrossReferenceBody>("""{"Ordering": null, "Rating": 7.5}""");

        Assert.True(omittedState.IsValid);
        Assert.Equal((false, false), (omitted!.HasOrderingSet, omitted.HasRatingSet));
        Assert.Equal(true, omitted.ToImageCrossReferenceUpdateData().IsPreferred);

        Assert.True(clearedState.IsValid);
        Assert.Equal((true, true), (cleared!.HasOrderingSet, cleared.HasRatingSet));
        Assert.Null(cleared.Ordering);
        Assert.Equal(7.5, cleared.Rating);
    }

    [Fact]
    public async Task BatchUpdate_MarksOnlyTheMembersSentInTheNestedUpdate()
    {
        var (body, state) = await Read<BatchUpdateImageBody>($$$"""{"ImageIDs": ["{{{_imageID}}}"], "Update": {"CountryCode": "jp"}}""");

        Assert.True(state.IsValid);
        Assert.Equal((false, false, false, true), (body!.Update.HasWidthSet, body.Update.HasHeightSet, body.Update.HasLanguageCodeSet, body.Update.HasCountryCodeSet));
    }

    #endregion
}
