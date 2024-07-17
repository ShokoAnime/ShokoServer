using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using AnimeType = Shoko.Models.Enums.AnimeType;

namespace Shoko.Server.Filters;

public static class FilterExtensions
{
    public static bool IsDirectory(this FilterPreset filter) => (filter.FilterType & GroupFilterType.Directory) != 0;
    
    public static Filterable ToFilterable(this SVR_AnimeSeries series, ILookup<int, CrossRef_AniDB_Other> movieDBLookup = null)
    {
        var filterable = new Filterable
        {
            NameDelegate = () => series.SeriesName,
            NamesDelegate = () =>
            {
                var titles = new HashSet<string>();
                if (!string.IsNullOrEmpty(series.SeriesNameOverride)) titles.Add(series.SeriesNameOverride);
                var ani = series.AniDB_Anime;
                if (ani != null) titles.UnionWith(ani.GetAllTitles());
                var tvdb = series.TvDBSeries?.Select(t => t.SeriesName).WhereNotNull();
                if (tvdb != null) titles.UnionWith(tvdb);
                var group = series.AnimeGroup;
                if (group != null) titles.Add(group.GroupName);
                return titles;
            },
            AniDBIDsDelegate = () => new HashSet<string>(){series.AniDB_ID.ToString()},
            SortingNameDelegate = () => series.SeriesName.ToSortName(),
            SeriesCountDelegate = () => 1,
            AirDateDelegate = () => series.AniDB_Anime?.AirDate,
            MissingEpisodesDelegate = () => series.MissingEpisodeCount,
            MissingEpisodesCollectingDelegate = () => series.MissingEpisodeCountGroups,
            TagsDelegate = () => series.AniDB_Anime?.Tags.Select(a => a.TagName).ToHashSet() ?? [],
            CustomTagsDelegate =
                () => series.AniDB_Anime?.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? [],
            YearsDelegate = () => series.Years,
            SeasonsDelegate = () => series.AniDB_Anime?.Seasons.ToHashSet() ?? [],
            HasTvDBLinkDelegate = () => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTvDbLinkDelegate = () => HasMissingTvDBLink(series),
            // expensive, as these are direct
            HasTMDbLinkDelegate = () => movieDBLookup?.Contains(series.AniDB_ID) ?? series.MovieDB_Movie != null,
            HasMissingTMDbLinkDelegate = () => HasMissingTMDbLink(series, movieDBLookup),
            HasTraktLinkDelegate = () => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTraktLinkDelegate = () => !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            IsFinishedDelegate =
                () =>
                {
                    var anime = series.AniDB_Anime;
                    return anime?.EndDate != null && anime.EndDate.Value < DateTime.Now;
                },
            LastAirDateDelegate = () =>
                series.EndDate ?? series.AllAnimeEpisodes.Select(a => a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            AddedDateDelegate = () => series.DateTimeCreated,
            LastAddedDateDelegate = () => series.VideoLocals.Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCountDelegate = () => series.AniDB_Anime?.EpisodeCountNormal ?? 0,
            TotalEpisodeCountDelegate = () => series.AniDB_Anime?.EpisodeCount ?? 0,
            LowestAniDBRatingDelegate = () => decimal.Round(Convert.ToDecimal(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRatingDelegate = () => decimal.Round(Convert.ToDecimal(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AverageAniDBRatingDelegate = () => decimal.Round(Convert.ToDecimal(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AnimeTypesDelegate = () =>
            {
                var anime = series.AniDB_Anime;
                return anime == null
                    ? []
                    : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                    {
                        ((AnimeType)anime.AnimeType).ToString()
                    };
            },
            VideoSourcesDelegate = () => series.VideoLocals.Select(a => a.AniDBFile).Where(a => a != null).Select(a => a.File_Source).ToHashSet(),
            SharedVideoSourcesDelegate = () =>
            {
                var sources = series.VideoLocals.Select(b => b.AniDBFile).Where(a => a != null).Select(a => a.File_Source).ToHashSet();
                return sources.Count > 0 ? sources : [];
            },
            AudioLanguagesDelegate =
                () => series.VideoLocals.Select(a => a.AniDBFile).Where(a => a != null).SelectMany(a => a.Languages.Select(b => b.LanguageName))
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedAudioLanguagesDelegate = () =>
            {
                var audio = new HashSet<string>();
                var audioNames = series.VideoLocals.Select(b => b.AniDBFile).Where(a => a != null)
                    .Select(a => a.Languages.Select(b => b.LanguageName));
                if (audioNames.Any()) audio = audioNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet();
                return audio;
            },
            SubtitleLanguagesDelegate =
                () => series.VideoLocals.Select(a => a.AniDBFile).Where(a => a != null).SelectMany(a => a.Subtitles.Select(b => b.LanguageName))
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedSubtitleLanguagesDelegate = () =>
            {
                var subtitles = new HashSet<string>();
                var subtitleNames = series.VideoLocals.Select(b => b.AniDBFile).Where(a => a != null)
                    .Select(a => a.Subtitles.Select(b => b.LanguageName));
                if (subtitleNames.Any()) subtitles = subtitleNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet();
                return subtitles;
            },
            ResolutionsDelegate = () =>
                series.VideoLocals.Where(a => a.MediaInfo?.VideoStream != null).Select(a =>
                    MediaInfoUtils.GetStandardResolution(Tuple.Create(a.MediaInfo.VideoStream.Width, a.MediaInfo.VideoStream.Height))).ToHashSet(),
            FilePathsDelegate = () => series.VideoLocals.Select(a => a.FirstValidPlace.FilePath).ToHashSet()
        };

        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this SVR_AnimeSeries series, int userID)
    {
        var anime = series.AniDB_Anime;
        var user = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);
        var vote = anime?.UserVote;
        var watchedDates = series.VideoLocals.Select(a => RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate)
            .Where(a => a != null).OrderBy(a => a).ToList();

        var filterable = new FilterableUserInfo
        {
            IsFavoriteDelegate = () => false,
            WatchedEpisodesDelegate = () => user?.WatchedEpisodeCount ?? 0,
            UnwatchedEpisodesDelegate = () => user?.UnwatchedEpisodeCount ?? 0,
            LowestUserRatingDelegate = () => vote?.VoteValue ?? 0,
            HighestUserRatingDelegate = () => vote?.VoteValue ?? 0,
            HasVotesDelegate = () => vote != null,
            HasPermanentVotesDelegate = () => vote is { VoteType: (int)AniDBVoteType.Anime },
            MissingPermanentVotesDelegate = () => vote is not { VoteType: (int)AniDBVoteType.Anime } && anime?.EndDate != null && anime.EndDate > DateTime.Now,
            WatchedDateDelegate = () => watchedDates.FirstOrDefault(),
            LastWatchedDateDelegate = () => watchedDates.LastOrDefault()
        };

        return filterable;
    }

    private static bool HasMissingTMDbLink(SVR_AnimeSeries series, ILookup<int, CrossRef_AniDB_Other> movieDBLookup)
    {
        var anime = series.AniDB_Anime;
        if (anime == null)
        {
            return false;
        }

        // TODO update this with the TMDB refactor
        if (anime.AnimeType != (int)AnimeType.Movie)
        {
            return false;
        }

        if (anime.Restricted > 0)
        {
            return false;
        }

        return !movieDBLookup?.Contains(series.AniDB_ID) ?? series.MovieDB_Movie == null;
    }

    private static bool HasMissingTvDBLink(SVR_AnimeSeries series)
    {
        var anime = series.AniDB_Anime;
        if (anime == null)
        {
            return false;
        }

        if (anime.AnimeType == (int)AnimeType.Movie)
        {
            return false;
        }

        if (anime.Restricted > 0)
        {
            return false;
        }

        return !RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any();
    }

    public static Filterable ToFilterable(this SVR_AnimeGroup group, ILookup<int, CrossRef_AniDB_Other> movieDBLookup = null)
    {
        var series = group.AllSeries;
        var anime = group.Anime;

        var filterable = new Filterable
        {
            NameDelegate = () => group.GroupName,
            NamesDelegate = () =>
            {
                var result = new HashSet<string>()
                {
                    group.GroupName
                };
                result.UnionWith(group.AllSeries.SelectMany(a =>
                {
                    var titles = new HashSet<string>();
                    if (!string.IsNullOrEmpty(a.SeriesNameOverride)) titles.Add(a.SeriesNameOverride);
                    var ani = a.AniDB_Anime;
                    if (ani != null) titles.UnionWith(ani.GetAllTitles());
                    var tvdb = a.TvDBSeries?.Select(t => t.SeriesName).WhereNotNull();
                    if (tvdb != null) titles.UnionWith(tvdb);
                    return titles;
                }));
                return result;
            },
            AniDBIDsDelegate = () => group.AllSeries.Select(a => a.AniDB_ID.ToString()).ToHashSet(),
            SortingNameDelegate = () => group.GroupName.ToSortName(),
            SeriesCountDelegate = () => series.Count,
            AirDateDelegate = () => group.AllSeries.Select(a => a.AirDate).DefaultIfEmpty(DateTime.MaxValue).Min(),
            LastAirDateDelegate = () => group.AllSeries.SelectMany(a => a.AllAnimeEpisodes).Select(a =>
                a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            MissingEpisodesDelegate = () => group.MissingEpisodeCount,
            MissingEpisodesCollectingDelegate = () => group.MissingEpisodeCount,
            TagsDelegate = () => group.Tags.Select(a => a.TagName).ToHashSet(),
            CustomTagsDelegate = () => group.CustomTags.Select(a => a.TagName).ToHashSet(),
            YearsDelegate = () => group.Years.ToHashSet(),
            SeasonsDelegate = () => group.Seasons.ToHashSet(),
            HasTvDBLinkDelegate = () => series.Any(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a.AniDB_ID).Any()),
            HasMissingTvDbLinkDelegate = () => HasMissingTvDBLink(group),
            HasTMDbLinkDelegate = () => series.Any(a => a.CrossRefMovieDB != null),
            HasMissingTMDbLinkDelegate = () => HasMissingTMDbLink(series),
            HasTraktLinkDelegate = () => series.Any(a => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(a.AniDB_ID).Any()),
            HasMissingTraktLinkDelegate = () => series.Any(a => !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(a.AniDB_ID).Any()),
            IsFinishedDelegate = () => group.AllSeries.All(a => a.EndDate != null && a.EndDate <= DateTime.Today),
            AddedDateDelegate = () => group.DateTimeCreated,
            LastAddedDateDelegate = () => series.SelectMany(a => a.VideoLocals).Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCountDelegate = () => series.Sum(a => a.AniDB_Anime?.EpisodeCountNormal ?? 0),
            TotalEpisodeCountDelegate = () => series.Sum(a => a.AniDB_Anime?.EpisodeCount ?? 0),
            LowestAniDBRatingDelegate =
                () => anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Min(),
            HighestAniDBRatingDelegate =
                () => anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Max(),
            AverageAniDBRatingDelegate =
                () => anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Average(),
            AnimeTypesDelegate = () => new HashSet<string>(anime.Select(a => ((AnimeType)a.AnimeType).ToString()), StringComparer.InvariantCultureIgnoreCase),
            VideoSourcesDelegate = () => series.SelectMany(a => a.VideoLocals).Select(a => a.AniDBFile).Where(a => a != null).Select(a => a.File_Source).ToHashSet(),
            SharedVideoSourcesDelegate = () =>
            {
                var sources = series.SelectMany(a => a.VideoLocals).Select(b => b.AniDBFile).Where(a => a != null).Select(a => a.File_Source).ToHashSet();
                return sources.Count > 0 ? sources : [];
            },
            AudioLanguagesDelegate = () => series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).Where(a => a != null)
                .SelectMany(a => a.Languages.Select(b => b.LanguageName)).ToHashSet(),
            SharedAudioLanguagesDelegate = () =>
            {
                var audio = new HashSet<string>();
                var audioLanguageNames = series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).Where(a => a != null)
                    .Select(a => a.Languages.Select(b => b.LanguageName));
                if (audioLanguageNames.Any())
                    audio = audioLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase))
                        .ToHashSet();
                return audio;
            },
            SubtitleLanguagesDelegate = () => series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).Where(a => a != null)
                .SelectMany(a => a.Subtitles.Select(b => b.LanguageName)).ToHashSet(),
            SharedSubtitleLanguagesDelegate = () =>
            {
                var subtitles = new HashSet<string>();
                var subtitleLanguageNames = series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).Where(a => a != null)
                    .Select(a => a.Subtitles.Select(b => b.LanguageName));
                if (subtitleLanguageNames.Any())
                    subtitles = subtitleLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet();
                return subtitles;
            },
            ResolutionsDelegate =
                () => series.SelectMany(a => a.VideoLocals).Where(a => a.MediaInfo?.VideoStream != null).Select(a =>
                    MediaInfoUtils.GetStandardResolution(Tuple.Create(a.MediaInfo.VideoStream.Width, a.MediaInfo.VideoStream.Height))).ToHashSet(),
            FilePathsDelegate = () => series.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace.FilePath)).ToHashSet()
        };

        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this SVR_AnimeGroup group, int userID)
    {
        var series = group.AllSeries;
        var anime = group.Anime;
        var user = RepoFactory.AnimeGroup_User.GetByUserAndGroupID(userID, group.AnimeGroupID);
        var vote = anime.Select(a => a.UserVote).Where(a => a is { VoteType: (int)VoteType.AnimePermanent or (int)VoteType.AnimeTemporary })
            .Select(a => a.VoteValue).OrderBy(a => a).ToList();
        var watchedDates = series.SelectMany(a => a.VideoLocals)
            .Select(a => RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate).Where(a => a != null).OrderBy(a => a)
            .ToList();
        var episodes = series.SelectMany(se => se.AnimeEpisodes).ToList();
        var watchedEpisodesDelegate = () => user != null ? episodes.Count(ep => ep.GetUserRecord(userID)?.IsWatched() ?? false) : 0;

        var filterable = new FilterableUserInfo
        {
            IsFavoriteDelegate = () => user?.IsFave == 1,
            WatchedEpisodesDelegate = watchedEpisodesDelegate,
            UnwatchedEpisodesDelegate = () => episodes.Count - watchedEpisodesDelegate(),
            LowestUserRatingDelegate = () => vote.FirstOrDefault(),
            HighestUserRatingDelegate = () => vote.LastOrDefault(),
            HasVotesDelegate = () => vote.Any(),
            HasPermanentVotesDelegate = () => anime.Select(a => a.UserVote).Any(a => a is { VoteType: (int)VoteType.AnimePermanent }),
            MissingPermanentVotesDelegate = () => anime.Any(a => a.UserVote is not { VoteType: (int)VoteType.AnimePermanent } && a.EndDate != null && a.EndDate > DateTime.Now),
            WatchedDateDelegate = () => watchedDates.FirstOrDefault(),
            LastWatchedDateDelegate = () => watchedDates.LastOrDefault()
        };

        return filterable;
    }

    private static bool HasMissingTMDbLink(IEnumerable<SVR_AnimeSeries> series)
    {
        return series.Any(s =>
        {
            var anime = s.AniDB_Anime;
            if (anime == null)
            {
                return false;
            }

            // TODO update this with the TMDB refactor
            if (anime.AnimeType != (int)AnimeType.Movie)
            {
                return false;
            }

            if (anime.Restricted > 0)
            {
                return false;
            }

            return s.CrossRefMovieDB != null;
        });
    }

    private static bool HasMissingTvDBLink(SVR_AnimeGroup group)
    {
        return group.AllSeries.Any(series =>
        {
            var anime = series.AniDB_Anime;
            if (anime == null)
            {
                return false;
            }

            if (anime.AnimeType == (int)AnimeType.Movie)
            {
                return false;
            }

            if (anime.Restricted > 0)
            {
                return false;
            }

            return !RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any();
        });
    }
}
