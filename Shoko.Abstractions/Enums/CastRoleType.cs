namespace Shoko.Abstractions.Enums;

/// <summary>
/// Types of roles an actor can have in a cast.
/// </summary>
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
