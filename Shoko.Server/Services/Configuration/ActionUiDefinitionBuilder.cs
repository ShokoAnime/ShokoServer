using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using NJsonSchema;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.UI;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   How an executable action's invocation parameters are described: a
///   render-ready definition for a client to lay a form out from, and the JSON
///   schema an incoming payload is checked against.
/// </summary>
/// <param name="Definition">The render-ready definition.</param>
/// <param name="Schema">The schema an invocation payload must satisfy.</param>
public sealed record ActionParameterDescription(UiDefinition Definition, JsonSchema Schema);

/// <summary>
///   Produces the render-ready <see cref="UiDefinition"/> for an executable
///   action's invocation parameters.
/// </summary>
/// <remarks>
///   <para>
///     Nothing here re-implements the configuration machinery: the schema comes
///     out of <see cref="ShokoJsonSchemaGenerator.GetSchemaForActionParameters"/>
///     and the definition out of the same <see cref="UiDefinitionBuilder"/> a
///     configuration goes through, so the two produce the same DTO and a client
///     cannot tell them apart.
///   </para>
///   <para>
///     It owns its own generator instance rather than sharing
///     <c>ConfigurationService</c>'s, because the generator serialises every
///     walk behind a single lock and action registration happens while
///     configurations are still being described.
///   </para>
/// </remarks>
/// <param name="loggerFactory">Logger factory.</param>
public class ActionUiDefinitionBuilder(ILoggerFactory loggerFactory)
{
    private readonly ShokoJsonSchemaGenerator _generator =
        new(ShokoJsonSerializers.CreateNewtonsoftSettings(), ShokoJsonSerializers.CreateSystemTextJsonOptions());

    private readonly UiDefinitionBuilder _uiDefinitionBuilder = new(loggerFactory.CreateLogger<UiDefinitionBuilder>());


    /// <summary>
    ///   Describes an action's invocation parameters.
    /// </summary>
    /// <remarks>
    ///   An action that declares parameters has to be describable. A shape the
    ///   generator cannot render is a defect in the action, not a condition to
    ///   recover from, so it fails startup exactly as the equivalent
    ///   configuration would rather than leaving a half-usable action behind
    ///   with no way to invoke it from a UI. The SHOKO0001-0005 analyzer rules
    ///   catch these shapes at compile time for anyone referencing the package.
    /// </remarks>
    /// <param name="id">The action's id.</param>
    /// <param name="name">The action's display name.</param>
    /// <param name="description">The action's description.</param>
    /// <param name="actionType">The concrete action type.</param>
    /// <returns>
    ///   The description, or <see langword="null"/> when the action declares no
    ///   parameters.
    /// </returns>
    /// <exception cref="Exception">
    ///   Thrown when the action declares parameters that cannot be described.
    /// </exception>
    public ActionParameterDescription? Build(Guid id, string name, string? description, Type actionType)
    {
        ArgumentNullException.ThrowIfNull(actionType);

        // Generating a schema is not free and the common case is an action with
        // no parameters at all, so skip the walk when reflection can already
        // tell there is nothing but metadata on the type.
        if (!MayHaveParameters(actionType))
            return null;

        var wrapped = _generator.GetSchemaForActionParameters(actionType);
        if (wrapped.Schema.ActualProperties.Count is 0)
            return null;

        return new(_uiDefinitionBuilder.Build(id, name, description, wrapped), wrapped.Schema);
    }

    /// <summary>
    ///   Whether the action carries any public property beyond its metadata
    ///   surface.
    /// </summary>
    /// <remarks>
    ///   Deliberately permissive — it only has to be free of false negatives,
    ///   since the generated schema is the thing that actually decides. A
    ///   get-only collection counts, because Newtonsoft populates one.
    /// </remarks>
    private static bool MayHaveParameters(Type actionType)
        => actionType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Any(x => !ActionMetadataContractResolver.MetadataMembers.TryGetValue(x.Name, out var declaredType) || x.PropertyType != declaredType);
}
