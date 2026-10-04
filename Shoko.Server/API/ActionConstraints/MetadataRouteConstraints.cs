using System;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.API.ActionConstraints;

/// <summary>
///   Matches a route segment naming a registered <see cref="MetadataSource"/>,
///   by its value or an alias, ignoring case and reading <c>_</c> as
///   <c>-</c>, so a segment naming no source falls through to a
///   <c>404 Not Found</c> rather than reaching an action.
/// </summary>
public sealed class MetadataSourceRouteConstraint : IRouteConstraint
{
    /// <summary>
    ///   The name the constraint is registered under in route templates.
    /// </summary>
    public const string Name = "metadata-source";

    /// <summary>
    ///   Tells whether the route value names a registered source.
    /// </summary>
    /// <param name="httpContext">The request, if any.</param>
    /// <param name="route">The route being matched.</param>
    /// <param name="routeKey">The name of the route value.</param>
    /// <param name="values">The route values.</param>
    /// <param name="routeDirection">Whether the route is matched or generated.</param>
    /// <returns><c>true</c> when the value names a registered source.</returns>
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(routeKey);
        ArgumentNullException.ThrowIfNull(values);

        return values.TryGetValue(routeKey, out var value) &&
            Convert.ToString(value, CultureInfo.InvariantCulture) is { } text &&
            MetadataSource.TryGet(text, out _);
    }
}

/// <summary>
///   Matches a route segment naming a registered
///   <see cref="MetadataEntityType"/>, by its value or an alias, ignoring case
///   and reading <c>_</c> as <c>-</c>.
/// </summary>
public sealed class MetadataEntityTypeRouteConstraint : IRouteConstraint
{
    /// <summary>
    ///   The name the constraint is registered under in route templates.
    /// </summary>
    public const string Name = "metadata-entity-type";

    /// <summary>
    ///   Tells whether the route value names a registered entity type.
    /// </summary>
    /// <param name="httpContext">The request, if any.</param>
    /// <param name="route">The route being matched.</param>
    /// <param name="routeKey">The name of the route value.</param>
    /// <param name="values">The route values.</param>
    /// <param name="routeDirection">Whether the route is matched or generated.</param>
    /// <returns><c>true</c> when the value names a registered entity type.</returns>
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        ArgumentNullException.ThrowIfNull(routeKey);
        ArgumentNullException.ThrowIfNull(values);

        return values.TryGetValue(routeKey, out var value) &&
            Convert.ToString(value, CultureInfo.InvariantCulture) is { } text &&
            MetadataEntityType.TryGet(text, out _);
    }
}
