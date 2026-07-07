
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Video.Release;
using Shoko.Server.Providers.AniDB.Helpers;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.AniDB;

public class AniDB_GroupStatus : IAnidbReleaseGroupStatus
{
    public int AniDB_GroupStatusID { get; set; }

    public int AnimeID { get; set; }

    public int GroupID { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public int CompletionState { get; set; }

    public int LastEpisodeNumber { get; set; }

    public decimal Rating { get; set; }

    public int Votes { get; set; }

    public string EpisodeRange { get; set; } = string.Empty;

    #region IAnidbReleaseGroupStatus Implementation

    int IAnidbReleaseGroupStatus.AnidbAnimeID => AnimeID;

    IAnidbAnime? IAnidbReleaseGroupStatus.AnidbAnime => RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    IReleaseGroup? IAnidbReleaseGroupStatus.Group =>
        RepoFactory.StoredReleaseInfo.GetByGroupAndProviderIDs(GroupID.ToString(), "AniDB").FirstOrDefault() is IReleaseInfo { Group: { } group }
            ? group
            : new ReleaseGroup { ID = GroupID.ToString(), Name = GroupName, ShortName = GroupName, Source = "AniDB" };

    GroupCompletionStatus IAnidbReleaseGroupStatus.CompletionState => (GroupCompletionStatus)CompletionState;

    int IAnidbReleaseGroupStatus.LastEpisodeNumber => LastEpisodeNumber;

    IAnidbEpisode? IAnidbReleaseGroupStatus.LastEpisode =>
        RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeNumber(AnimeID, LastEpisodeNumber).FirstOrDefault();

    string IAnidbReleaseGroupStatus.EpisodeRange => EpisodeRange;

    IReadOnlyList<IAnidbEpisode> IAnidbReleaseGroupStatus.ReleasedEpisodes =>
        (string.IsNullOrWhiteSpace(EpisodeRange) ? [] : EpisodeRange.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Select(AniDBEpisodeNumber.Parse)
            .Select(epNum => RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(AnimeID, epNum.EpisodeType, epNum.EpisodeNumber).FirstOrDefault())
            .OfType<AniDB_Episode>()
            .ToList();

    double IAnidbReleaseGroupStatus.Rating => (double)Rating;

    int IAnidbReleaseGroupStatus.RatingVotes => Votes;

    #endregion
}
