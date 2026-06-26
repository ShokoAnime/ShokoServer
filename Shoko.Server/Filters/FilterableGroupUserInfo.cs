#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.User.Enums;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Filters;

public sealed class FilterableGroupUserInfo(AnimeGroup group, int userID, DateTime now) : IFilterableUserInfo
{
    private List<AnimeSeries>? _allSeries;
    private List<AnimeSeries> AllSeries => _allSeries ??= group.AllSeries;

    private Dictionary<int, AnimeSeries_User>? _seriesUserDict;
    private Dictionary<int, AnimeSeries_User> SeriesUserDict => _seriesUserDict ??=
        AllSeries.Select(a => RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, a.AnimeSeriesID))
            .WhereNotNull()
            .ToDictionary(a => a.AnimeSeriesID);

    private List<double>? _ratings;
    private List<double> Ratings => _ratings ??=
        SeriesUserDict.Values
            .Where(u => u.HasUserRating)
            .Select(a => a.UserRating!.Value)
            .Order()
            .ToList();

    private List<DateTime>? _watchedDates;
    private List<DateTime> WatchedDates => _watchedDates ??=
        AllSeries.SelectMany(a => a.VideoLocals)
            .Select(a => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate)
            .WhereNotNull()
            .OrderBy(a => a)
            .ToList();

    private EpisodeCounts? _watchedEpisodeCounts;

    private int GetEpCount(bool getWatched)
    {
        var count = 0;
        foreach (var ep in AllSeries.SelectMany(s => s.AnimeEpisodes))
        {
            if (ep.EpisodeType is not (EpisodeType.Episode or EpisodeType.Special)) continue;
            var vls = ep.VideoLocals;
            if (vls.Count == 0 || vls.All(vl => vl.IsIgnored)) continue;

            var isWatched = ep.GetUserRecord(userID)?.IsWatched ?? false;
            if (isWatched == getWatched)
                count++;
        }
        return count;
    }

    public bool IsFavorite => SeriesUserDict.Values.Any(a => a.IsFavorite);

    public IReadOnlySet<string> UserTags => SeriesUserDict.Values.SelectMany(a => a.UserTags).ToHashSet();

    public int WatchedEpisodes => GetEpCount(true);

    public EpisodeCounts WatchedEpisodeCounts
    {
        get
        {
            if (_watchedEpisodeCounts is { } cached) return cached;
            var counts = new EpisodeCounts();
            foreach (var ep in AllSeries.SelectMany(ser => ser.AnimeEpisodes))
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

    public int UnwatchedEpisodes => GetEpCount(false);

    public bool HasVotes => Ratings.Count > 0;

    public bool HasPermanentVotes =>
        SeriesUserDict.Values.Any(a => a is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent });

    public bool MissingPermanentVotes =>
        AllSeries.Any(ser => !(SeriesUserDict.TryGetValue(ser.AnimeSeriesID, out var userData) && userData.HasUserRating) && ser.EndDate is not null && ser.EndDate > now.Date);

    public int SeriesVoteCount => SeriesUserDict.Count;

    public int SeriesTemporaryVoteCount =>
        SeriesUserDict.Values.Count(userData => userData is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Temporary });

    public int SeriesPermanentVoteCount =>
        SeriesUserDict.Values.Count(userData => userData is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent });

    public DateTime? WatchedDate => WatchedDates.FirstOrDefault();

    public DateTime? LastWatchedDate => WatchedDates.LastOrDefault();

    public double LowestUserRating => Ratings.FirstOrDefault();

    public double HighestUserRating => Ratings.LastOrDefault();
}
