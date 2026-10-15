using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.OpenApi;
using Newtonsoft.Json.Linq;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using Xunit;

namespace Shoko.Tests.API.Swagger;

public class AuthorizeOperationFilterTests
{
    [Theory]
    [InlineData(typeof(Endpoints), nameof(Endpoints.Optional))]
    [InlineData(typeof(UnattributedEndpoints), nameof(UnattributedEndpoints.Optional))] // without authorization attributes
    public async Task OptionalAuthentication_AdvertisesAnonymousAndApiKeyAccess(Type endpointsType, string methodName)
    {
        var security = await GetSecurity(endpointsType, methodName);

        Assert.NotNull(security);
        Assert.Equal(3, security.Count);
        Assert.Empty((JObject)security[0]);
        Assert.NotNull(security[1][AuthorizeOperationFilter.ApiKeyHeaderScheme]);
        Assert.NotNull(security[2][AuthorizeOperationFilter.ApiKeyQueryScheme]);
    }

    [Theory]
    [InlineData(nameof(Endpoints.Authorized))]
    [InlineData(nameof(Endpoints.AuthorizedWithOptionalAuthentication))] // optional authentication on an endpoint requiring authorization
    public async Task Authorized_RequiresTheApiKey(string methodName)
    {
        var security = await GetSecurity(methodName);

        Assert.NotNull(security);
        Assert.Equal(2, security.Count);
        Assert.NotNull(security[0][AuthorizeOperationFilter.ApiKeyHeaderScheme]);
        Assert.NotNull(security[1][AuthorizeOperationFilter.ApiKeyQueryScheme]);
    }

    [Theory]
    [InlineData(typeof(Endpoints), nameof(Endpoints.Anonymous))]
    [InlineData(typeof(UnattributedEndpoints), nameof(UnattributedEndpoints.Plain))] // unattributed
    public async Task Anonymous_HasNoSecurity(Type endpointsType, string methodName)
        => Assert.Null(await GetSecurity(endpointsType, methodName));

    private static Task<JArray?> GetSecurity(string methodName)
        => GetSecurity(typeof(Endpoints), methodName);

    private static async Task<JArray?> GetSecurity(Type endpointsType, string methodName)
    {
        var document = new OpenApiDocument
        {
            Components = new()
            {
                SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    [AuthorizeOperationFilter.ApiKeyHeaderScheme] = new OpenApiSecurityScheme
                    {
                        Name = "apikey",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                    },
                    [AuthorizeOperationFilter.ApiKeyQueryScheme] = new OpenApiSecurityScheme
                    {
                        Name = "apikey",
                        In = ParameterLocation.Query,
                        Type = SecuritySchemeType.ApiKey,
                    },
                },
            },
            Paths = [],
        };
        var method = endpointsType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;
        var operation = new OpenApiOperation();
        var context = new OperationFilterContext(new ApiDescription(), null!, new SchemaRepository(), document, method);

        new AuthorizeOperationFilter().Apply(operation, context);

        document.Paths["/test"] = new OpenApiPathItem { Operations = new() { [HttpMethod.Get] = operation } };
        var json = JObject.Parse(await document.SerializeAsJsonAsync(OpenApiSpecVersion.OpenApi3_0));
        return json["paths"]!["/test"]!["get"]!["security"] as JArray;
    }

    [Authorize]
    private sealed class Endpoints
    {
        [AllowAnonymous]
        [OptionalAuthentication]
        public void Optional() { }

        public void Authorized() { }

        [OptionalAuthentication]
        public void AuthorizedWithOptionalAuthentication() { }

        [AllowAnonymous]
        public void Anonymous() { }
    }

    private sealed class UnattributedEndpoints
    {
        [OptionalAuthentication]
        public void Optional() { }

        public void Plain() { }
    }
}
