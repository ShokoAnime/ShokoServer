using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.OpenApi;
using Moq;
using Shoko.Abstractions.Plugin;
using Shoko.Server.API.Swagger;
using Shoko.Tests.Infrastructure;
using Xunit;

namespace Shoko.Tests.API.Swagger;

/// <summary>
///   Endpoints a plugin maps itself, rather than through a controller, are
///   described and listed in that plugin's Swagger document only.
/// </summary>
public class PluginEndpointDocumentTests
{
    private const string PluginDocument = "SomePlugin-v1";

    private static readonly PluginDocumentInclusionPredicate _predicate = CreatePredicate();

    private static readonly MethodInfo _handler = typeof(PluginEndpointDocumentTests).GetMethod(nameof(Hello), BindingFlags.NonPublic | BindingFlags.Static)!;

    [Fact]
    public void RequestDelegateEndpoint_WithGroupName_IsDescribed()
    {
        var descriptions = Describe(CreateEndpoint(
            "/api/plugin/SomePlugin/items/{id}",
            new HttpMethodMetadata(["POST", "DELETE"]),
            new EndpointGroupNameAttribute("SomePlugin"),
            new AcceptsMetadata(["application/json"], typeof(Request)),
            new ProducesResponseTypeMetadata(StatusCodes.Status404NotFound),
            new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(string), ["text/plain"])
        ));

        Assert.Equal(["POST", "DELETE"], descriptions.Select(description => description.HttpMethod));
        var post = descriptions[0];
        Assert.Equal("SomePlugin", post.GroupName);
        Assert.Equal("api/plugin/SomePlugin/items/{id}", post.RelativePath);
        Assert.Equal("SomePlugin", post.ActionDescriptor.RouteValues["controller"]);

        var id = Assert.Single(post.ParameterDescriptions, parameter => parameter.Source == BindingSource.Path);
        Assert.Equal("id", id.Name);
        var body = Assert.Single(post.ParameterDescriptions, parameter => parameter.Source == BindingSource.Body);
        Assert.Equal(typeof(Request), body.ModelMetadata.ModelType);
        Assert.True(body.IsRequired);
        Assert.Equal("application/json", Assert.Single(post.SupportedRequestFormats).MediaType);

        Assert.Equal([200, 404], post.SupportedResponseTypes.Select(response => response.StatusCode));
        Assert.Equal(typeof(string), post.SupportedResponseTypes[0].Type);
        Assert.Equal("text/plain", Assert.Single(post.SupportedResponseTypes[0].ApiResponseFormats).MediaType);
    }

    [Fact]
    public void RequestDelegateEndpoint_WithoutGroupName_IsNotDescribed()
        => Assert.Empty(Describe(CreateEndpoint("/signalr/some/negotiate", new HttpMethodMetadata(["POST"]))));

    [Fact]
    public void RequestDelegateEndpoint_ExcludedFromDescription_IsNotDescribed()
        => Assert.Empty(Describe(CreateEndpoint(
            "/api/plugin/SomePlugin/hidden",
            new HttpMethodMetadata(["GET"]),
            new EndpointGroupNameAttribute("SomePlugin"),
            new ExcludeFromDescriptionAttribute()
        )));

    [Fact]
    public void RouteHandlerEndpoint_IsLeftToTheMinimalApiExplorer()
        => Assert.Empty(Describe(CreateEndpoint(
            "/api/plugin/SomePlugin/hello",
            new HttpMethodMetadata(["GET"]),
            new EndpointGroupNameAttribute("SomePlugin"),
            _handler
        )));

    [Fact]
    public void MappedEndpoint_NamingThePlugin_GoesInThePluginDocumentOnly()
    {
        var description = Describe(CreateEndpoint(
            "/api/plugin/SomePlugin/raw",
            new HttpMethodMetadata(["POST"]),
            new EndpointGroupNameAttribute("someplugin")
        )).Single();

        Assert.True(_predicate.Include(PluginDocument, description));
        Assert.False(_predicate.Include("v1", description));
        Assert.False(_predicate.Include("v3", description));
        Assert.False(_predicate.Include("OtherPlugin-v1", description));
        Assert.False(_predicate.Include("SomePlugin-v3", description));
    }

    [Fact]
    public void MappedEndpoint_NamingNoPlugin_IsNotListed()
    {
        var description = Describe(CreateEndpoint(
            "/api/plugin/Unknown/raw",
            new HttpMethodMetadata(["POST"]),
            new EndpointGroupNameAttribute("Unknown")
        )).Single();

        Assert.False(_predicate.Include(PluginDocument, description));
        Assert.False(_predicate.Include("v1", description));
    }

    [Fact]
    public void RouteHandlerEndpoint_IsOwnedByTheAssemblyOfItsHandler()
    {
        var description = CreateMinimalDescription(_handler);

        Assert.True(_predicate.Include(PluginDocument, description));
        Assert.False(_predicate.Include("v1", description));
    }

    [Fact]
    public void RouteHandlerEndpoint_DeclaredInTheServer_IsNotListed()
    {
        var description = CreateMinimalDescription(typeof(PluginDocumentInclusionPredicate).GetMethod(nameof(PluginDocumentInclusionPredicate.Include))!);

        Assert.False(_predicate.Include(PluginDocument, description));
        Assert.False(_predicate.Include("v1", description));
    }

    [Fact]
    public void Authorization_OnAMappedEndpointWithoutMethod_RequiresTheApiKey()
    {
        var description = Describe(CreateEndpoint(
            "/api/plugin/SomePlugin/raw",
            new HttpMethodMetadata(["POST"]),
            new EndpointGroupNameAttribute("SomePlugin"),
            new AuthorizeAttribute()
        )).Single();
        var operation = new OpenApiOperation();

        new AuthorizeOperationFilter().Apply(operation, new(description, null!, new(), new(), null!));

        Assert.Equal(2, operation.Security!.Count);
    }

    [Fact]
    public void MappedEndpoint_WithoutAuthorization_HasNoSecurity()
    {
        var description = Describe(CreateEndpoint("/api/plugin/SomePlugin/raw", new HttpMethodMetadata(["POST"]), new EndpointGroupNameAttribute("SomePlugin")))
            .Single();
        var operation = new OpenApiOperation();

        new AuthorizeOperationFilter().Apply(operation, new(description, null!, new(), new(), null!));

        Assert.Null(operation.Security);
    }

    private static string Hello() => "hello";

    private static PluginDocumentInclusionPredicate CreatePredicate()
    {
        var pluginManager = new Mock<IPluginManager>();
        pluginManager.Setup(manager => manager.GetPluginInfos()).Returns([
            PluginTestDoubles.CorePluginInfo(typeof(PluginDocumentInclusionPredicate), Guid.NewGuid()),
            PluginTestDoubles.InstalledPluginInfo(typeof(PluginTestDoubles.TestPlugin), Guid.NewGuid()),
        ]);
        return new(pluginManager.Object);
    }

    private static RouteEndpoint CreateEndpoint(string pattern, params object[] metadata)
        => new(_ => Task.CompletedTask, RoutePatternFactory.Parse(pattern), 0, new(metadata), pattern);

    private static ApiDescription[] Describe(RouteEndpoint endpoint)
    {
        var context = new ApiDescriptionProviderContext([]);
        new RequestDelegateApiDescriptionProvider(new DefaultEndpointDataSource(endpoint), new EmptyModelMetadataProvider()).OnProvidersExecuting(context);
        return [.. context.Results];
    }

    private static ApiDescription CreateMinimalDescription(MethodInfo handler)
        => new()
        {
            HttpMethod = "GET",
            RelativePath = "api/plugin/SomePlugin/hello",
            ActionDescriptor = new ActionDescriptor { EndpointMetadata = [handler, new HttpMethodMetadata(["GET"])] },
        };

    public sealed record Request(string Query);
}
