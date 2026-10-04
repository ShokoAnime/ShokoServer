using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// How an AniDB anime's regular episodes number on a schedule of a source
/// they are not all linked to yet, learned from the ones that are. It is what
/// places an episode the schedule's source does not list yet.
/// </summary>
/// <param name="Offset">What to add to an AniDB episode number to get the schedule's number.</param>
/// <param name="LastLinkedEpisodeNumber">The highest AniDB episode number linked into the schedule's series or season.</param>
/// <param name="LastLinkedScheduleNumber">The highest schedule episode number any of the anime's episodes is linked to.</param>
/// <param name="LinkedEpisodeIDs">The anime's AniDB episodes linked to anything on the schedule's source.</param>
internal sealed record AiringEpisodeOffset(
    int Offset,
    int LastLinkedEpisodeNumber,
    int LastLinkedScheduleNumber,
    IReadOnlySet<int> LinkedEpisodeIDs
)
{
    /// <summary>
    /// The schedule's number for an AniDB episode: only for a regular episode
    /// with no link to the schedule's source that continues past the last
    /// linked one.
    /// </summary>
    /// <param name="episode">The AniDB episode.</param>
    /// <returns>The schedule's episode number, or <c>null</c> when the offset does not place the episode.</returns>
    public int? GetScheduleEpisodeNumber(IAnidbEpisode episode)
    {
        if (((IEpisode)episode).Type is not EpisodeType.Episode || LinkedEpisodeIDs.Contains(episode.AnidbID))
            return null;
        if (episode.EpisodeNumber <= LastLinkedEpisodeNumber)
            return null;

        var number = episode.EpisodeNumber + Offset;
        return number > LastLinkedScheduleNumber && number > 0 ? number : null;
    }
}
