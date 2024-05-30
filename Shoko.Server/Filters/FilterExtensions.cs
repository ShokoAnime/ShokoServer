using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.Filters.Info;
using Shoko.Server.Filters.SortingSelectors;
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
            NameDelegate = () => series.GetSeriesName(),
            NamesDelegate = () =>
            {
                var result = series.GetAllTitles();
                var group = series.AnimeGroup;
                if (group != null) result.Add(group.GroupName);
                return result;
            },
            AniDBIDsDelegate = () => new HashSet<string>(){series.AniDB_ID.ToString()},
            SortingNameDelegate = () => series.GetSeriesName().ToSortName(),
            SeriesCountDelegate = () => 1,
            AirDateDelegate = () => series.GetAnime()?.AirDate,
            MissingEpisodesDelegate = () => series.MissingEpisodeCount,
            MissingEpisodesCollectingDelegate = () => series.MissingEpisodeCountGroups,
            TagsDelegate = () => series.GetAnime()?.GetTags().Select(a => a.TagName).ToHashSet() ?? new HashSet<string>(),
            CustomTagsDelegate =
                () => series.GetAnime()?.GetCustomTagsForAnime().Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ??
                      new HashSet<string>(),
            YearsDelegate = () => GetYears(series),
            SeasonsDelegate = () => series.GetAnime()?.GetSeasons().ToHashSet() ?? new HashSet<(int Year, AnimeSeason Season)>(),
            HasTvDBLinkDelegate = () => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTvDbLinkDelegate = () => HasMissingTvDBLink(series),
            // expensive, as these are direct
            HasTMDbLinkDelegate = () => movieDBLookup?.Contains(series.AniDB_ID) ?? series.GetMovieDB() != null,
            HasMissingTMDbLinkDelegate = () => HasMissingTMDbLink(series, movieDBLookup),
            HasTraktLinkDelegate = () => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTraktLinkDelegate = () => !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            IsFinishedDelegate =
                () =>
                {
                    var anime = series.GetAnime();
                    return anime?.EndDate != null && anime.EndDate.Value < DateTime.Now;
                },
            LastAirDateDelegate = () =>
                series.EndDate ?? series.GetAnimeEpisodes().Select(a => a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            AddedDateDelegate = () => series.DateTimeCreated,
            LastAddedDateDelegate = () => series.GetVideoLocals().Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCountDelegate = () => series.GetAnime()?.EpisodeCountNormal ?? 0,
            TotalEpisodeCountDelegate = () => series.GetAnime()?.EpisodeCount ?? 0,
            LowestAniDBRatingDelegate = () => decimal.Round(Convert.ToDecimal(series.GetAnime()?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRatingDelegate = () => decimal.Round(Convert.ToDecimal(series.GetAnime()?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AverageAniDBRatingDelegate = () => decimal.Round(Convert.ToDecimal(series.GetAnime()?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AnimeTypesDelegate = () =>
            {
                var anime = series.GetAnime();
                return anime == null
                    ? new HashSet<string>()
                    : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                    {
                        ((AnimeType)anime.AnimeType).ToString()
                    };
            },
            VideoSourcesDelegate = () => series.GetVideoLocals().Select(a => a.GetAniDBFile()).Where(a => a != null).Select(a => a.File_Source).ToHashSet(),
            SharedVideoSourcesDelegate = () =>
            {
                var sources = series.GetVideoLocals().Select(b => b.GetAniDBFile()).Where(a => a != null).Select(a => a.File_Source).ToHashSet();
                return sources.Count == 1 ? sources : new HashSet<string>();
            },
            AudioLanguagesDelegate =
                () => series.GetVideoLocals().Select(a => a.GetAniDBFile()).Where(a => a != null).SelectMany(a => a.Languages.Select(b => b.LanguageName))
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedAudioLanguagesDelegate = () =>
            {
                var audio = new HashSet<string>();
                var audioNames = series.GetVideoLocals().Select(b => b.GetAniDBFile()).Where(a => a != null)
                    .Select(a => a.Languages.Select(b => b.LanguageName));
                if (audioNames.Any()) audio = audioNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet();
                return audio;
            },
            SubtitleLanguagesDelegate =
                () => series.GetVideoLocals().Select(a => a.GetAniDBFile()).Where(a => a != null).SelectMany(a => a.Subtitles.Select(b => b.LanguageName))
                    .ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedSubtitleLanguagesDelegate = () =>
            {
                var subtitles = new HashSet<string>();
                var subtitleNames = series.GetVideoLocals().Select(b => b.GetAniDBFile()).Where(a => a != null)
                    .Select(a => a.Subtitles.Select(b => b.LanguageName));
                if (subtitleNames.Any()) subtitles = subtitleNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet();
                return subtitles;
            },
            ResolutionsDelegate = () =>
                series.GetVideoLocals().Where(a => a.Media?.VideoStream != null).Select(a =>
                    MediaInfoUtils.GetStandardResolution(Tuple.Create(a.Media.VideoStream.Width, a.Media.VideoStream.Height))).ToHashSet(),
            FilePathsDelegate = () => series.GetVideoLocals().Select(a => a.GetBestVideoLocalPlace().FilePath).ToHashSet()
        };

        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this SVR_AnimeSeries series, int userID)
    {
        var anime = series.GetAnime();
        var user = series.GetUserRecord(userID);
        var vote = anime?.UserVote;
        var watchedDates = series.GetVideoLocals().Select(a => a.GetUserRecord(userID)?.WatchedDate).Where(a => a != null).OrderBy(a => a).ToList();

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

    private static HashSet<int> GetYears(SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
        var startyear = anime?.BeginYear ?? 0;
        if (startyear == 0)
        {
            return new HashSet<int>();
        }

        var endyear = anime?.EndYear ?? 0;
        if (endyear == 0)
        {
            endyear = DateTime.Today.Year;
        }

        if (endyear < startyear)
        {
            endyear = startyear;
        }

        if (startyear == endyear)
        {
            return new HashSet<int>
            {
                startyear
            };
        }

        return new HashSet<int>(Enumerable.Range(startyear, endyear - startyear + 1).Where(anime.IsInYear));
    }

    private static bool HasMissingTMDbLink(SVR_AnimeSeries series, ILookup<int, CrossRef_AniDB_Other> movieDBLookup)
    {
        var anime = series.GetAnime();
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

        return !movieDBLookup?.Contains(series.AniDB_ID) ?? series.GetMovieDB() == null;
    }

    private static bool HasMissingTvDBLink(SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
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
        var series = group.GetAllSeries(true);
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
                result.UnionWith(group.GetAllSeries().SelectMany(a =>
                {
                    var titles = new HashSet<string>();
                    if (!string.IsNullOrEmpty(a.SeriesNameOverride)) titles.Add(a.SeriesNameOverride);
                    var ani = a.GetAnime();
                    if (ani != null) titles.UnionWith(ani.GetAllTitles());
                    var tvdb = a.GetTvDBSeries()?.Select(t => t.SeriesName).WhereNotNull();
                    if (tvdb != null) titles.UnionWith(tvdb);
                    return titles;
                }));
                return result;
            },
            AniDBIDsDelegate = () => group.GetAllSeries().Select(a => a.AniDB_ID.ToString()).ToHashSet(),
            SortingNameDelegate = () => group.GroupName.ToSortName(),
            SeriesCountDelegate = () => series.Count,
            AirDateDelegate = () => group.GetAllSeries().Select(a => a.AirDate).DefaultIfEmpty(DateTime.MaxValue).Min(),
            LastAirDateDelegate = () => group.GetAllSeries().SelectMany(a => a.GetAnimeEpisodes()).Select(a =>
                a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            MissingEpisodesDelegate = () => group.MissingEpisodeCount,
            MissingEpisodesCollectingDelegate = () => group.MissingEpisodeCount,
            TagsDelegate = () => group.Contract?.Stat_AllTags ?? new HashSet<string>(),
            CustomTagsDelegate = () => group.Contract?.Stat_AllCustomTags ?? new HashSet<string>(),
            YearsDelegate = () => group.Contract?.Stat_AllYears ?? new HashSet<int>(),
            SeasonsDelegate = () => group.Contract?.Stat_AllSeasons.Select(a =>
            {
                var parts = a.Split(' ');
                return (int.Parse(parts[1]), Enum.Parse<AnimeSeason>(parts[0]));
            }).ToHashSet() ?? new HashSet<(int, AnimeSeason)>(),
            HasTvDBLinkDelegate = () => series.Any(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a.AniDB_ID).Any()),
            HasMissingTvDbLinkDelegate = () => HasMissingTvDBLink(group),
            HasTMDbLinkDelegate =
                () => movieDBLookup != null ? series.All(a => movieDBLookup.Contains(a.AniDB_ID)) : group.Contract?.Stat_HasMovieDBLink ?? false,
            HasMissingTMDbLinkDelegate = () => HasMissingTMDbLink(series, movieDBLookup),
            HasTraktLinkDelegate = () => series.Any(a => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(a.AniDB_ID).Any()),
            HasMissingTraktLinkDelegate = () => series.Any(a => !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(a.AniDB_ID).Any()),
            IsFinishedDelegate = () => group.GetAllSeries().All(a => a.EndDate != null && a.EndDate <= DateTime.Today),
            AddedDateDelegate = () => group.DateTimeCreated,
            LastAddedDateDelegate = () => series.SelectMany(a => a.GetVideoLocals()).Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCountDelegate = () => series.Sum(a => a.GetAnime()?.EpisodeCountNormal ?? 0),
            TotalEpisodeCountDelegate = () => series.Sum(a => a.GetAnime()?.EpisodeCount ?? 0),
            LowestAniDBRatingDelegate =
                () => anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Min(),
            HighestAniDBRatingDelegate =
                () => anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Max(),
            AverageAniDBRatingDelegate =
                () => anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Average(),
            AnimeTypesDelegate = () => new HashSet<string>(anime.Select(a => ((AnimeType)a.AnimeType).ToString()), StringComparer.InvariantCultureIgnoreCase),
            VideoSourcesDelegate = () => group.Contract?.Stat_AllVideoQuality ?? new HashSet<string>(),
            SharedVideoSourcesDelegate = () => group.Contract?.Stat_AllVideoQuality_Episodes ?? new HashSet<string>(),
            AudioLanguagesDelegate = () => series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                .SelectMany(a => a.Languages.Select(b => b.LanguageName)).ToHashSet(),
            SharedAudioLanguagesDelegate = () =>
            {
                var audio = new HashSet<string>();
                var audioLanguageNames = series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                    .Select(a => a.Languages.Select(b => b.LanguageName));
                if (audioLanguageNames.Any())
                    audio = audioLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase))
                        .ToHashSet();
                return audio;
            },
            SubtitleLanguagesDelegate = () => series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                .SelectMany(a => a.Subtitles.Select(b => b.LanguageName)).ToHashSet(),
            SharedSubtitleLanguagesDelegate = () =>
            {
                var subtitles = new HashSet<string>();
                var subtitleLanguageNames = series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                    .Select(a => a.Subtitles.Select(b => b.LanguageName));
                if (subtitleLanguageNames.Any())
                    subtitles = subtitleLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet();
                return subtitles;
            },
            ResolutionsDelegate =
                () => series.SelectMany(a => a.GetVideoLocals()).Where(a => a.Media?.VideoStream != null).Select(a =>
                    MediaInfoUtils.GetStandardResolution(Tuple.Create(a.Media.VideoStream.Width, a.Media.VideoStream.Height))).ToHashSet(),
            FilePathsDelegate = () => series.SelectMany(s => s.GetVideoLocals().Select(a => a.GetBestVideoLocalPlace().FilePath)).ToHashSet()
        };

        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this SVR_AnimeGroup group, int userID)
    {
        var series = group.GetAllSeries(true);
        var anime = group.Anime;
        var user = group.GetUserRecord(userID);
        var vote = anime.Select(a => a.UserVote).Where(a => a is { VoteType: (int)VoteType.AnimePermanent or (int)VoteType.AnimeTemporary })
            .Select(a => a.VoteValue).OrderBy(a => a).ToList();
        var watchedDates = series.SelectMany(a => a.GetVideoLocals()).Select(a => a.GetUserRecord(userID)?.WatchedDate).Where(a => a != null).OrderBy(a => a)
            .ToList();

        var filterable = new FilterableUserInfo
        {
            IsFavoriteDelegate = () => user?.IsFave == 1,
            WatchedEpisodesDelegate = () => user?.WatchedEpisodeCount ?? 0,
            UnwatchedEpisodesDelegate = () => user?.UnwatchedEpisodeCount ?? 0,
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

    private static bool HasMissingTMDbLink(IEnumerable<SVR_AnimeSeries> series, ILookup<int, CrossRef_AniDB_Other> movieDB)
    {
        return series.Any(s =>
        {
            var anime = s.GetAnime();
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

            return movieDB?.Contains(s.AniDB_ID) ?? s.GetMovieDB() == null;
        });
    }

    private static bool HasMissingTvDBLink(SVR_AnimeGroup group)
    {
        return group.GetAllSeries().Any(series =>
        {
            var anime = series.GetAnime();
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

    public static IReadOnlyList<FilterPreset> GetAllYearFilters()
    {
        var years = GetYears(RepoFactory.AnimeSeries.GetAll().Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AniDB_ID)).ToList());
        return years.Select(s => new FilterPreset
        {
            Name = s.ToString(),
            FilterType = GroupFilterType.Year,
            Locked = true,
            ApplyAtSeriesLevel = true,
            Expression = new InYearExpression
            {
                Parameter = s
            },
            SortingExpression = new NameSortingSelector()
        }).ToList();
    }

    private static IEnumerable<int> GetYears(IEnumerable<AniDB_Anime> objects)
    {
        var anime = objects.WhereNotNull().Where(a => a.AirDate != null).ToList();
        var minDate = anime.Min(a => a.AirDate!.Value);
        var maxDate = anime.Max(o => o.EndDate ?? DateTime.Today);

        for (var year = minDate.Year; year <= maxDate.Year; year++)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);

            if (anime.Any(o => o.AirDate <= yearEnd && (o.EndDate >= yearStart || o.EndDate == null)))
            {
                yield return year;
            }
        }
    }

    public static IReadOnlyList<FilterPreset> GetAllSeasonFilters()
    {
        var seasons = GetSeasons(RepoFactory.AnimeSeries.GetAll().Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AniDB_ID)).ToList());
        return seasons.Select(season => new FilterPreset
        {
            Name = season.Season + " " + season.Year,
            Locked = true,
            FilterType = GroupFilterType.Season,
            ApplyAtSeriesLevel = true,
            Expression = new InSeasonExpression { Season = season.Season, Year = season.Year },
            SortingExpression = new NameSortingSelector()
        }).ToList();
    }

    private static IEnumerable<(int Year, AnimeSeason Season)> GetSeasons(IEnumerable<AniDB_Anime> objects)
    {
        var anime = objects.Where(a => a?.AirDate != null).ToList();
        var seasons = new SortedSet<(int Year, AnimeSeason Season)>();
        foreach (var current in anime)
        {
            var beginYear = current.AirDate!.Value.Year;
            var endYear = current.EndDate?.Year ?? DateTime.Today.Year;
            for (var year = beginYear; year <= endYear; year++)
            {
                if (beginYear < year && year < endYear)
                {
                    seasons.Add((year, AnimeSeason.Winter));
                    seasons.Add((year, AnimeSeason.Spring));
                    seasons.Add((year, AnimeSeason.Summer));
                    seasons.Add((year, AnimeSeason.Fall));
                    continue;
                }

                if (!seasons.Contains((year, AnimeSeason.Winter)) && current.IsInSeason(AnimeSeason.Winter, year)) seasons.Add((year, AnimeSeason.Winter));
                if (!seasons.Contains((year, AnimeSeason.Spring)) && current.IsInSeason(AnimeSeason.Spring, year)) seasons.Add((year, AnimeSeason.Spring));
                if (!seasons.Contains((year, AnimeSeason.Summer)) && current.IsInSeason(AnimeSeason.Summer, year)) seasons.Add((year, AnimeSeason.Summer));
                if (!seasons.Contains((year, AnimeSeason.Fall)) && current.IsInSeason(AnimeSeason.Fall, year)) seasons.Add((year, AnimeSeason.Fall));
            }
        }

        return seasons;
    }

    public static IReadOnlyList<FilterPreset> GetAllTagFilters()
    {
        var allTags = new HashSet<string>(
            RepoFactory.AniDB_Tag.GetAllForLocalSeries().Select(a => a.TagName.Replace('`', '\'')),
            StringComparer.InvariantCultureIgnoreCase);
        var info = new CultureInfo("en-US", false).TextInfo;
        return allTags.Select(s => new FilterPreset
        {
            FilterType = GroupFilterType.Tag,
            ApplyAtSeriesLevel = true,
            Name = info.ToTitleCase(s),
            Locked = true,
            Expression = new HasTagExpression
            {
                Parameter = s
            },
            SortingExpression = new NameSortingSelector()
        }).ToList();
    }

    public static IReadOnlyList<FilterPreset> GetAllFilters()
    {
        var filters = RepoFactory.FilterPreset.GetTopLevel().Where(a => (a.FilterType & GroupFilterType.Directory) == 0).ToList();
        filters.AddRange(GetAllYearFilters());
        filters.AddRange(GetAllSeasonFilters());
        filters.AddRange(GetAllTagFilters());
        return filters;
    }
}
