using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Shoko.Server.API.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.API.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var requiresAuthorization = !HasAttribute<AllowAnonymousAttribute>(context) && HasAttribute<AuthorizeAttribute>(context);
        if (!requiresAuthorization && !HasAttribute<OptionalAuthenticationAttribute>(context))
            return;

        operation.Security ??= [];

        // An empty requirement means no authentication is also accepted.
        if (!requiresAuthorization)
            operation.Security.Add([]);

        operation.Security.Add(new() { { new("ApiKey", context.Document), [] } });
    }

    private static bool HasAttribute<TAttribute>(OperationFilterContext context)
        => context.MethodInfo.GetCustomAttributes(true).OfType<TAttribute>().Any() ||
            (context.MethodInfo.DeclaringType?.GetCustomAttributes(true).OfType<TAttribute>().Any() ?? false);
}
