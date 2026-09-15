using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Shoko.Server.API.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.API.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    /// <summary>
    ///   The name of the security scheme for the API key in the <c>apikey</c> header.
    /// </summary>
    public const string ApiKeyHeaderScheme = "ApiKey";

    /// <summary>
    ///   The name of the security scheme for the API key in the <c>apikey</c> query parameter.
    /// </summary>
    public const string ApiKeyQueryScheme = "ApiKeyQuery";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresAuthorization = !HasAttribute<AllowAnonymousAttribute>(context) && HasAttribute<AuthorizeAttribute>(context);
        if (!requiresAuthorization && !HasAttribute<OptionalAuthenticationAttribute>(context))
            return;

        operation.Security ??= [];

        // An empty requirement means no authentication is also accepted.
        if (!requiresAuthorization)
            operation.Security.Add([]);

        // Separate requirements are alternatives, so either location for the key is accepted.
        operation.Security.Add(new() { { new(ApiKeyHeaderScheme, context.Document), [] } });
        operation.Security.Add(new() { { new(ApiKeyQueryScheme, context.Document), [] } });
    }

    private static bool HasAttribute<TAttribute>(OperationFilterContext context)
        => context.MethodInfo.GetCustomAttributes(true).OfType<TAttribute>().Any() ||
            (context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<TAttribute>().Any() ?? false);
}
