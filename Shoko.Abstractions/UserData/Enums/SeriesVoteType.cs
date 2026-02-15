
namespace Shoko.Abstractions.UserData.Enums;

/// <summary>
/// Type of vote submitted for content.
/// </summary>
public enum SeriesVoteType
{
    /// <summary>
    /// Permanent vote for completed content.
    /// </summary>
    Permanent = 1,

    /// <summary>
    /// Temporary vote for ongoing/airing content.
    /// </summary>
    Temporary = 2,
}
