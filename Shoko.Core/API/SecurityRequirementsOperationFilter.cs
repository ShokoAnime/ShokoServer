using Microsoft.AspNetCore.Authorization;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shoko.Core.API
{
    public class SecurityRequirementsOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            // Policy names map to scopes
            var attributes = context.MethodInfo
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>();

            if (attributes.Any())
            {
                var scopes = attributes.Select(s => s.Policy).Distinct();
                if (!scopes.Any()) scopes = new[] { "user" };

                operation.Responses.Add("401", new Response { Description = "Unauthorized" });
                operation.Responses.Add("403", new Response { Description = "Forbidden" });

                operation.Security = new List<IDictionary<string, IEnumerable<string>>>
                {
                    new Dictionary<string, IEnumerable<string>>
                    {
                        { "oauth2", scopes }
                    }
                };

                return;
            }
            if (context.MethodInfo.GetCustomAttributes(true).OfType<IAllowAnonymous>().Any()) return;

            attributes = context.MethodInfo
                .DeclaringType
                .GetCustomAttributes(true)
                .OfType<AuthorizeAttribute>();

            if (attributes.Any())
            {
                var scopes = attributes.Select(s => s.Policy).Distinct();
                if (!scopes.Any()) scopes = new[] { "user" };

                operation.Responses.Add("401", new Response { Description = "Unauthorized" });
                operation.Responses.Add("403", new Response { Description = "Forbidden" });

                operation.Security = new List<IDictionary<string, IEnumerable<string>>>
                {
                    new Dictionary<string, IEnumerable<string>>
                    {
                        { "oauth2", scopes }
                    }
                };

                return;
            }
        }
    }
}
