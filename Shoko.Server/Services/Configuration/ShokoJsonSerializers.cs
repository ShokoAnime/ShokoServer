using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.Services.Configuration;

/// <summary>
///   The serializer settings <see cref="ShokoJsonSchemaGenerator"/> generates
///   against.
/// </summary>
/// <remarks>
///   Both configurations and executable actions are described by the same
///   generator, so both have to hand it the same settings — the settings decide
///   how enum members, defaults and denied values are rendered, and two callers
///   drifting apart would show up as two subtly different
///   <see cref="Abstractions.UI.UiDefinition"/> shapes for the same authored
///   type.
/// </remarks>
internal static class ShokoJsonSerializers
{
    /// <summary>
    ///   Creates the Newtonsoft settings. A fresh instance per caller, because
    ///   <see cref="JsonSerializerSettings"/> is mutable and the schema
    ///   generator clones it to swap the contract resolver.
    /// </summary>
    public static JsonSerializerSettings CreateNewtonsoftSettings()
        => new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Include,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = [new StringEnumConverter()],
        };

    /// <summary>
    ///   Creates the System.Text.Json options.
    /// </summary>
    public static JsonSerializerOptions CreateSystemTextJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            WriteIndented = true,
            PreferredObjectCreationHandling = JsonObjectCreationHandling.Replace,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
