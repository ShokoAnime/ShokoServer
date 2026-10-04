using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Shoko.Abstractions.Metadata;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Shoko.Server.API.Swagger;

/// <summary>
///   Publishes every route or query parameter taking one
///   <see cref="MetadataSource"/> or <see cref="MetadataEntityType"/> as a
///   string enum of the registered values, so clients see which sources and
///   kinds the server has.
/// </summary>
/// <remarks>
///   The values are taken once registration closes, after every plugin is set
///   up and before the server serves, and stay the same until it restarts.
///   Input still takes an alias or an old spelling, ignoring case, as the
///   binders do; the enum only lists the values.
/// </remarks>
public sealed class MetadataRouteParameterFilter : IOperationFilter
{
    #region Snapshot

    private static volatile RegisteredValues? _captured;

    /// <summary>
    ///   The registered values, as a closed registration left them.
    /// </summary>
    /// <param name="Sources">Every registered source's value.</param>
    /// <param name="EntityTypes">Every registered entity type's value.</param>
    internal sealed record RegisteredValues(IReadOnlyList<string> Sources, IReadOnlyList<string> EntityTypes);

    /// <summary>
    ///   Takes the registered sources and entity types, once registration is
    ///   closed.
    /// </summary>
    internal static void Capture()
        => _captured = Current();

    /// <summary>
    ///   The values the parameters are published with: those taken when
    ///   registration closed, or the ones registered so far before then.
    /// </summary>
    internal static RegisteredValues Values
        => _captured ?? Current();

    private static RegisteredValues Current()
        => new(
            [.. MetadataSource.All.Select(source => source.Value)],
            [.. MetadataEntityType.All.Select(entityType => entityType.Value)]
        );

    #endregion

    #region Filter

    /// <summary>
    ///   Replaces the schema of each source or entity type parameter with a
    ///   string enum of the registered values.
    /// </summary>
    /// <param name="operation">The operation being described.</param>
    /// <param name="context">What the operation was described from.</param>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        if (operation.Parameters is not { Count: > 0 } parameters)
            return;

        var values = Values;
        foreach (var parameter in parameters.OfType<OpenApiParameter>())
        {
            if (parameter.In is not (ParameterLocation.Path or ParameterLocation.Query) || TypeOf(parameter, context) is not { } type)
                continue;

            if (type == typeof(MetadataSource))
                parameter.Schema = Create("metadata source", values.Sources);
            else if (type == typeof(MetadataEntityType))
                parameter.Schema = Create("metadata entity type", values.EntityTypes);
        }
    }

    /// <summary>
    ///   The type a route or query parameter binds to, from the action's own
    ///   parameters or, failing that, from the API description.
    /// </summary>
    /// <remarks>
    ///   The action's parameter is asked first, since the API description may
    ///   describe a route value by its template rather than by the type it is
    ///   bound to.
    /// </remarks>
    /// <param name="parameter">The parameter.</param>
    /// <param name="context">What the operation was described from.</param>
    /// <returns>The type, or <c>null</c> when it cannot be told.</returns>
    private static Type? TypeOf(OpenApiParameter parameter, OperationFilterContext context)
    {
        if (context.MethodInfo?.GetParameters().FirstOrDefault(info => string.Equals(info.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)) is { } info)
            return info.ParameterType;

        var source = parameter.In is ParameterLocation.Path ? BindingSource.Path : BindingSource.Query;
        return context.ApiDescription.ParameterDescriptions
            .FirstOrDefault(description => description.Source == source && string.Equals(description.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
            ?.Type;
    }

    /// <summary>
    ///   The schema of a parameter taking one registered value.
    /// </summary>
    /// <param name="kind">What the value names, for the description.</param>
    /// <param name="allowed">The registered values.</param>
    /// <returns>The schema.</returns>
    internal static OpenApiSchema Create(string kind, IReadOnlyList<string> allowed)
        => new()
        {
            Type = JsonSchemaType.String,
            Description = $"A registered {kind}, by its value. An alias or an old spelling is also accepted, ignoring case and reading _ as -.",
            Enum = [.. allowed.Select(value => (JsonNode)JsonValue.Create(value))],
        };

    #endregion
}
