using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Shoko.Server.API.Swagger;

/// <summary>
/// Describes the mapped endpoints whose handler is a plain <see cref="RequestDelegate"/>,
/// which the minimal API explorer skips because it has no method to read them from.
/// </summary>
/// <remarks>
/// Only endpoints carrying a group name (<c>WithGroupName</c>) are described, since the group
/// name is what assigns them to a plugin's document. Everything else comes from the endpoint's
/// metadata: the route pattern gives the path and its parameters, <see cref="IAcceptsMetadata"/>
/// the request body, <see cref="IProducesResponseTypeMetadata"/> the responses, and tags,
/// summaries, descriptions and names are read from the endpoint by Swagger itself.
/// </remarks>
/// <param name="endpointDataSource">All the endpoints of the application.</param>
/// <param name="modelMetadataProvider">The metadata provider for the described types.</param>
public sealed class RequestDelegateApiDescriptionProvider(EndpointDataSource endpointDataSource, IModelMetadataProvider modelMetadataProvider)
    : IApiDescriptionProvider
{
    /// <summary>
    /// Runs alongside the minimal API explorer, before the versioned explorer sorts the results.
    /// </summary>
    public int Order => -1100;

    /// <inheritdoc/>
    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {
        foreach (var endpoint in endpointDataSource.Endpoints)
        {
            if (endpoint is not RouteEndpoint routeEndpoint)
                continue;

            var metadata = routeEndpoint.Metadata;
            if (metadata.GetMetadata<MethodInfo>() is not null ||
                metadata.GetMetadata<IEndpointGroupNameMetadata>() is not { EndpointGroupName.Length: > 0 } groupName ||
                metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is { ExcludeFromDescription: true } ||
                metadata.GetMetadata<IHttpMethodMetadata>() is not { HttpMethods.Count: > 0 } httpMethods)
                continue;

            foreach (var httpMethod in httpMethods.HttpMethods)
                context.Results.Add(CreateApiDescription(routeEndpoint, httpMethod, groupName.EndpointGroupName));
        }
    }

    /// <inheritdoc/>
    public void OnProvidersExecuted(ApiDescriptionProviderContext context) { }

    private ApiDescription CreateApiDescription(RouteEndpoint endpoint, string httpMethod, string groupName)
    {
        var apiDescription = new ApiDescription
        {
            HttpMethod = httpMethod,
            GroupName = groupName,
            RelativePath = endpoint.RoutePattern.RawText?.TrimStart('/'),
            ActionDescriptor = new ActionDescriptor
            {
                DisplayName = endpoint.DisplayName,
                // Swagger falls back to the controller name for the tag.
                RouteValues = { ["controller"] = groupName },
                EndpointMetadata = [.. endpoint.Metadata],
            },
        };

        foreach (var parameter in endpoint.RoutePattern.Parameters)
        {
            apiDescription.ParameterDescriptions.Add(new()
            {
                Name = parameter.Name,
                Source = BindingSource.Path,
                Type = typeof(string),
                ModelMetadata = modelMetadataProvider.GetMetadataForType(typeof(string)),
                IsRequired = !parameter.IsOptional,
                DefaultValue = parameter.Default,
            });
        }

        if (endpoint.Metadata.GetMetadata<IAcceptsMetadata>() is { ContentTypes.Count: > 0 } accepts)
        {
            var requestType = accepts.RequestType ?? typeof(object);
            apiDescription.ParameterDescriptions.Add(new()
            {
                Name = "body",
                Source = BindingSource.Body,
                Type = requestType,
                ModelMetadata = modelMetadataProvider.GetMetadataForType(requestType),
                IsRequired = !accepts.IsOptional,
            });
            foreach (var contentType in accepts.ContentTypes)
                apiDescription.SupportedRequestFormats.Add(new() { MediaType = contentType });
        }

        // The last response declared for a status code wins, as with the minimal API explorer.
        var responses = new Dictionary<int, IProducesResponseTypeMetadata>();
        foreach (var response in endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>())
            responses[response.StatusCode] = response;
        foreach (var response in responses.Values.OrderBy(response => response.StatusCode))
        {
            var responseType = new ApiResponseType
            {
                StatusCode = response.StatusCode,
                Type = response.Type ?? typeof(void),
                Description = response.Description,
            };
            if (response.Type is { } type && type != typeof(void))
                responseType.ModelMetadata = modelMetadataProvider.GetMetadataForType(type);
            foreach (var contentType in response.ContentTypes)
                responseType.ApiResponseFormats.Add(new() { MediaType = contentType });
            apiDescription.SupportedResponseTypes.Add(responseType);
        }

        return apiDescription;
    }
}
