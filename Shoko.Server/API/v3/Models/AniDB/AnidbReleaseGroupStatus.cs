using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
///   Represents the release status of a single AniDB release
///   group for an anime.
/// </summary>
public class AnidbReleaseGroupStatus
{
    /// <summary>
    ///   AniDB group ID.
    /// </summary>
    public int GroupID { get; set; }

    /// <summary>
    ///   AniDB group name.
    /// </summary>
    public string GroupName { get; set; }

    /// <summary>
    ///   The group's completion status for this anime.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public GroupCompletionStatus CompletionState { get; set; }

    /// <summary>
    ///   The last episode number released by the group, or 0 if
    ///   unknown.
    /// </summary>
    public int LastEpisodeNumber { get; set; }

    /// <summary>
    ///   The raw, comma-separated AniDB episode codes released by
    ///   the group (e.g. "1,2,3,S1"), as reported by AniDB.
    /// </summary>
    public string EpisodeRange { get; set; }

    /// <summary>
    ///   The group's rating for their release, 0-10, or 0 if
    ///   unrated.
    /// </summary>
    public double Rating { get; set; }

    /// <summary>
    ///   Number of votes behind <see cref="Rating"/>.
    /// </summary>
    public int RatingVotes { get; set; }

    public AnidbReleaseGroupStatus(AniDB_GroupStatus status)
    {
        GroupID = status.GroupID;
        GroupName = status.GroupName;
        CompletionState = (GroupCompletionStatus)status.CompletionState;
        LastEpisodeNumber = status.LastEpisodeNumber;
        EpisodeRange = status.EpisodeRange;
        Rating = (double)status.Rating;
        RatingVotes = status.Votes;
    }
}
