
using System;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.ActionConstraints;

public class RedirectConstraint : IActionConstraint
{
    public int Order => 0;

    public bool Accept(ActionConstraintContext context)
    {
        var requestPath = context.RouteContext.HttpContext.Request.Path.Value;
        if (!string.Equals(requestPath, "/", StringComparison.OrdinalIgnoreCase))
            return false;

        var settings = Utils.SettingsProvider.GetSettings();
        return settings.Web.EnableWebUI && !string.Equals(settings.Web.WebUIPublicPath, "/", StringComparison.OrdinalIgnoreCase);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class RedirectConstraintAttribute : Attribute, IActionConstraintFactory
{
    public bool IsReusable => true;

    public IActionConstraint CreateInstance(IServiceProvider services)
        => new RedirectConstraint();
}
