using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB.Helpers;

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
    ///   The episodes released by the group, compressed into
    ///   ranges (e.g. "1-5, 7, S1-S3"), or empty if unknown.
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
        EpisodeRange = CompressEpisodeRange(status.EpisodeRange);
        Rating = (double)status.Rating;
        RatingVotes = status.Votes;
    }

    private static string CompressEpisodeRange(string rawRange)
    {
        if (string.IsNullOrWhiteSpace(rawRange))
            return string.Empty;

        var parsed = rawRange.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(AniDBEpisodeNumber.Parse)
            .ToList();

        return parsed
            .GroupBy(x => x.EpisodeType)
            .OrderBy(g => g.Key)
            .Select(g => g.Select(x => x.EpisodeNumber).ToCompressedRange(prefix: g.Key.Prefix))
            .Join(", ");
    }
}
