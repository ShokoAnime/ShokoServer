#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters;

public sealed class FilterableSeriesUserInfo(AnimeSeries series, int userID, DateTime now) : IFilterableUserInfo
{
    private readonly AniDB_Anime? _anime = series.AniDB_Anime;
    private readonly AnimeSeries_User? _user = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);

    private List<DateTime>? _watchedDates;
    private List<DateTime> WatchedDates => _watchedDates ??= series.VideoLocals
        .Select(a => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate)
        .WhereNotNull()
        .Order()
        .ToList();

    private EpisodeCounts? _watchedEpisodeCounts;

    public bool IsFavorite => _user?.IsFavorite ?? false;

    public IReadOnlySet<string> UserTags => _user?.UserTags.ToHashSet() ?? [];

    public int WatchedEpisodes => _user?.WatchedEpisodeCount ?? 0;

    public EpisodeCounts WatchedEpisodeCounts
    {
        get
        {
            if (_watchedEpisodeCounts is { } cached) return cached;
            var counts = new EpisodeCounts();
            foreach (var ep in series.AnimeEpisodes)
            {
                if (!(ep.GetUserRecord(userID)?.IsWatched ?? false)) continue;
                switch (ep.AniDB_Episode?.EpisodeType)
                {
                    case EpisodeType.Episode: counts.Episodes++; break;
                    case EpisodeType.Special: counts.Specials++; break;
                    case EpisodeType.Credits: counts.Credits++; break;
                    case EpisodeType.Trailer: counts.Trailers++; break;
                    case EpisodeType.Parody: counts.Parodies++; break;
                    default: counts.Others++; break;
                }
            }
            return _watchedEpisodeCounts = counts;
        }
    }

    public int UnwatchedEpisodes => _user?.UnwatchedEpisodeCount ?? 0;

    public bool HasVotes => _user is { HasUserRating: true };

    public bool HasPermanentVotes => _user is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent };

    public bool MissingPermanentVotes =>
        _user is not { HasUserRating: true } && _anime?.EndDate is not null && _anime.EndDate > now.Date;

    public int SeriesVoteCount => _user is { HasUserRating: true } ? 1 : 0;

    public int SeriesTemporaryVoteCount =>
        _user is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Temporary } ? 1 : 0;

    public int SeriesPermanentVoteCount =>
        _user is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent } ? 1 : 0;

    public DateTime? WatchedDate => WatchedDates.FirstOrDefault();

    public DateTime? LastWatchedDate => WatchedDates.LastOrDefault();

    public double LowestUserRating => _user?.UserRating ?? 0;

    public double HighestUserRating => _user?.UserRating ?? 0;
}
