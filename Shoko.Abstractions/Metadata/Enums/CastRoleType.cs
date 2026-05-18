namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// Types of roles an actor can have in a cast.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum CastRoleType : byte
{
    /// <summary>
    /// No specified role type.
    /// </summary>
    None = 0,

    /// <summary>
    /// Main character.
    /// </summary>
    MainCharacter,

    /// <summary>
    /// Minor character.
    /// </summary>
    MinorCharacter,

    /// <summary>
    /// Background character.
    /// </summary>
    BackgroundCharacter,

    /// <summary>
    /// Cameo appearance.
    /// </summary>
    Cameo,
}
