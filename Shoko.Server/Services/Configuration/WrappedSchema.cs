using System.Collections.Generic;
using NJsonSchema;

namespace Shoko.Server.Services.Configuration;

public record class WrappedJsonSchema
{
    /// <summary>
    /// The JSON schema for the configuration.
    /// </summary>
    public required JsonSchema Schema { get; init; }

    /// <summary>
    /// The typed builders the generator filled in while walking the
    /// configuration type, keyed by the schema node they describe. The UI
    /// definition is joined from these and <see cref="Schema"/>, rather than
    /// from the <c>x-uiDefinition</c> bag the generator emits for the
    /// validator.
    /// </summary>
    internal IReadOnlyDictionary<JsonSchema, UiClassBuilder> UiBuilders { get; init; } = new Dictionary<JsonSchema, UiClassBuilder>();

    /// <summary>
    /// The value converters the configuration's own serializer would use.
    /// </summary>
    internal UiEmitContext? EmitContext { get; init; }

    /// <summary>
    /// Whether or not the configuration has custom actions.
    /// </summary>
    public bool HasCustomActions { get; set; }

    /// <summary>
    /// Whether or not the configuration has a custom new factory.
    /// </summary>
    public bool HasCustomNewFactory { get; set; }

    /// <summary>
    /// Whether or not the configuration has custom validation.
    /// </summary>
    public bool HasCustomValidation { get; set; }

    /// <summary>
    /// Whether or not the configuration has a custom save action.
    /// </summary>
    public bool HasCustomSave { get; set; }

    /// <summary>
    /// Whether or not the configuration has a custom load action.
    /// </summary>
    public bool HasCustomLoad { get; set; }

    /// <summary>
    /// Whether or not the configuration support live editing the in-memory
    /// configuration.
    /// </summary>
    public bool HasLiveEdit { get; set; }
}
