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
    [Fact]
    public async Task OptionalAuthentication_AdvertisesAnonymousAndApiKeyAccess()
    {
        var security = await GetSecurity(nameof(Endpoints.Optional));

        Assert.NotNull(security);
        Assert.Equal(3, security.Count);
        Assert.Empty((JObject)security[0]);
        Assert.NotNull(security[1][AuthorizeOperationFilter.ApiKeyHeaderScheme]);
        Assert.NotNull(security[2][AuthorizeOperationFilter.ApiKeyQueryScheme]);
    }

    [Fact]
    public async Task Authorized_RequiresTheApiKey()
    {
        var security = await GetSecurity(nameof(Endpoints.Authorized));

        Assert.NotNull(security);
        Assert.Equal(2, security.Count);
        Assert.NotNull(security[0][AuthorizeOperationFilter.ApiKeyHeaderScheme]);
        Assert.NotNull(security[1][AuthorizeOperationFilter.ApiKeyQueryScheme]);
    }

    [Fact]
    public async Task OptionalAuthentication_WithoutAuthorizationAttributes_AdvertisesAnonymousAndApiKeyAccess()
    {
        var security = await GetSecurity(typeof(UnattributedEndpoints), nameof(UnattributedEndpoints.Optional));

        Assert.NotNull(security);
        Assert.Equal(3, security.Count);
        Assert.Empty((JObject)security[0]);
        Assert.NotNull(security[1][AuthorizeOperationFilter.ApiKeyHeaderScheme]);
        Assert.NotNull(security[2][AuthorizeOperationFilter.ApiKeyQueryScheme]);
    }

    [Fact]
    public async Task OptionalAuthentication_OnAnEndpointRequiringAuthorization_StillRequiresTheApiKey()
    {
        var security = await GetSecurity(nameof(Endpoints.AuthorizedWithOptionalAuthentication));

        Assert.NotNull(security);
        Assert.Equal(2, security.Count);
        Assert.NotNull(security[0][AuthorizeOperationFilter.ApiKeyHeaderScheme]);
        Assert.NotNull(security[1][AuthorizeOperationFilter.ApiKeyQueryScheme]);
    }

    [Fact]
    public async Task Anonymous_HasNoSecurity()
        => Assert.Null(await GetSecurity(nameof(Endpoints.Anonymous)));

    [Fact]
    public async Task Unattributed_HasNoSecurity()
        => Assert.Null(await GetSecurity(typeof(UnattributedEndpoints), nameof(UnattributedEndpoints.Plain)));

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
