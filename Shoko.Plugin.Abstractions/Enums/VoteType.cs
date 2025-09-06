namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// Type of vote submitted for content.
/// </summary>
public enum VoteType
{
    /// <summary>
    /// Permanent vote for completed content.
    /// </summary>
    Permanent = 1,

    /// <summary>
    /// Temporary vote for ongoing/airing content.
    /// </summary>
    Temporary = 2
}