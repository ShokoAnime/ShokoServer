
namespace Shoko.Abstractions.Enums;

/// <summary>
/// Represents the types of AniDB ban that can occur.
/// </summary>
public enum AnidbBanType : byte
{
    /// <summary>
    /// An AniDB UDP ban.
    /// </summary>
    UDP = 1,

    /// <summary>
    /// An AniDB HTTP ban.
    /// </summary>
    HTTP = 2,
}
