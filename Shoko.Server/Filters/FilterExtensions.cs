using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using AnimeType = Shoko.Models.Enums.AnimeType;

namespace Shoko.Server.Filters;

public static class FilterExtensions
{
    public static bool IsDirectory(this FilterPreset filter) => (filter.FilterType & GroupFilterType.Directory) != 0;
    
    public static Filterable ToFilterable(this SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
        var name = series.GetSeriesName();
        // TODO optimize this a bunch. Lots of duplicate calls. Contract should be severely trimmed
        var filterable = new Filterable
        {
            Name = name,
            SortingName = name.GetSortName(),
            SeriesCount = 1,
            AirDate = anime?.AirDate,
            MissingEpisodes = series.Contract?.MissingEpisodeCount ?? 0,
            MissingEpisodesCollecting = series.Contract?.MissingEpisodeCountGroups ?? 0,
            Tags = anime?.GetAllTags() ?? new HashSet<string>(),
            CustomTags =
                series.Contract?.AniDBAnime?.CustomTags?.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? new HashSet<string>(),
            Years = GetYears(series),
            Seasons = anime.GetSeasons().ToHashSet(),
            HasTvDBLink = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTvDbLink = HasMissingTvDBLink(series),
            HasTMDbLink = series.Contract?.CrossRefAniDBMovieDB != null,
            HasMissingTMDbLink = HasMissingTMDbLink(series),
            HasTraktLink = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTraktLink = !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            IsFinished = series.Contract?.AniDBAnime?.AniDBAnime?.EndDate != null && series.Contract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now,
            LastAirDate =
                series.EndDate ?? series.GetAnimeEpisodes().Select(a => a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            AddedDate = series.DateTimeCreated,
            LastAddedDate = series.GetVideoLocals().Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCount = anime?.EpisodeCountNormal ?? 0,
            TotalEpisodeCount = anime?.EpisodeCount ?? 0,
            LowestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero),
            AnimeTypes = anime == null
                ? new HashSet<string>()
                : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    ((AnimeType)anime.AnimeType).ToString()
                },
            VideoSources = series.Contract?.AniDBAnime?.Stat_AllVideoQuality ?? new HashSet<string>(),
            SharedVideoSources = series.Contract?.AniDBAnime?.Stat_AllVideoQuality_Episodes ?? new HashSet<string>(),
            AudioLanguages = series.Contract?.AniDBAnime?.Stat_AudioLanguages ?? new HashSet<string>(),
            SharedAudioLanguages =
                series.GetVideoLocals().Select(b => b.GetAniDBFile()).Where(a => a != null).Select(a => a.Languages.Select(b => b.LanguageName))
                    .Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet(),
            SubtitleLanguages = series.Contract?.AniDBAnime?.Stat_SubtitleLanguages ?? new HashSet<string>(),
            SharedSubtitleLanguages =
                series.GetVideoLocals().Select(b => b.GetAniDBFile()).Where(a => a != null).Select(a => a.Subtitles.Select(b => b.LanguageName))
                    .Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet(),
        };

        return filterable;
    }

    public static UserDependentFilterable ToUserDependentFilterable(this SVR_AnimeSeries series, int userID)
    {
        var anime = series.GetAnime();
        var user = series.GetUserRecord(userID);
        var vote = anime?.UserVote;
        var watchedDates = series.GetVideoLocals().Select(a => a.GetUserRecord(userID)?.WatchedDate).Where(a => a != null).OrderBy(a => a).ToList();
        var name = series.GetSeriesName();
        var filterable = new UserDependentFilterable
        {
            Name = name,
            SortingName = name.GetSortName(),
            SeriesCount = 1,
            AirDate = anime?.AirDate,
            MissingEpisodes = series.Contract?.MissingEpisodeCount ?? 0,
            MissingEpisodesCollecting = series.Contract?.MissingEpisodeCountGroups ?? 0,
            Tags = anime?.GetAllTags() ?? new HashSet<string>(),
            CustomTags =
                series.Contract?.AniDBAnime?.CustomTags?.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? new HashSet<string>(),
            Years = GetYears(series),
            Seasons = anime?.GetSeasons().ToHashSet(),
            HasTvDBLink = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTvDbLink = HasMissingTvDBLink(series),
            HasTMDbLink = series.Contract?.CrossRefAniDBMovieDB != null,
            HasMissingTMDbLink = HasMissingTMDbLink(series),
            HasTraktLink = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTraktLink = !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            IsFinished = series.Contract?.AniDBAnime?.AniDBAnime?.EndDate != null && series.Contract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now,
            LastAirDate =
                series.EndDate ?? series.GetAnimeEpisodes().Select(a => a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            AddedDate = series.DateTimeCreated,
            LastAddedDate = series.GetVideoLocals().Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCount = anime?.EpisodeCountNormal ?? 0,
            TotalEpisodeCount = anime?.EpisodeCount ?? 0,
            LowestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero),
            AnimeTypes = anime == null
                ? new HashSet<string>()
                : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
                {
                    ((AnimeType)anime.AnimeType).ToString()
                },
            VideoSources = series.Contract?.AniDBAnime?.Stat_AllVideoQuality ?? new HashSet<string>(),
            SharedVideoSources = series.Contract?.AniDBAnime?.Stat_AllVideoQuality_Episodes ?? new HashSet<string>(),
            AudioLanguages = series.Contract?.AniDBAnime?.Stat_AudioLanguages ?? new HashSet<string>(),
            SharedAudioLanguages =
                series.GetVideoLocals().Select(b => b.GetAniDBFile()).Where(a => a != null).Select(a => a.Languages.Select(b => b.LanguageName))
                    .Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet(),
            SubtitleLanguages = series.Contract?.AniDBAnime?.Stat_SubtitleLanguages ?? new HashSet<string>(),
            SharedSubtitleLanguages =
                series.GetVideoLocals().Select(b => b.GetAniDBFile()).Where(a => a != null).Select(a => a.Subtitles.Select(b => b.LanguageName))
                    .Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet(),
            IsFavorite = false,
            WatchedEpisodes = user?.WatchedCount ?? 0,
            UnwatchedEpisodes = (anime?.EpisodeCount ?? 0) - (user?.WatchedCount ?? 0),
            LowestUserRating = vote?.VoteValue ?? 0,
            HighestUserRating = vote?.VoteValue ?? 0,
            HasVotes = vote != null,
            HasPermanentVotes = vote is { VoteType: (int)AniDBVoteType.Anime },
            MissingPermanentVotes = vote is not { VoteType: (int)AniDBVoteType.Anime } && anime?.EndDate != null && anime.EndDate > DateTime.Now,
            WatchedDate = watchedDates.FirstOrDefault(),
            LastWatchedDate = watchedDates.LastOrDefault()
        };

        return filterable;
    }

    private static HashSet<int> GetYears(SVR_AnimeSeries series)
    {
        var contract = series.Contract?.AniDBAnime;
        var startyear = contract?.AniDBAnime?.BeginYear ?? 0;
        if (startyear == 0)
        {
            return new HashSet<int>();
        }

        var endyear = contract?.AniDBAnime?.EndYear ?? 0;
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

        return new HashSet<int>(Enumerable.Range(startyear, endyear - startyear + 1).Where(contract.IsInYear));
    }

    private static bool HasMissingTMDbLink(SVR_AnimeSeries series)
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

        return series.Contract?.CrossRefAniDBMovieDB == null;
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

    public static Filterable ToFilterable(this SVR_AnimeGroup group)
    {
        var series = group.GetAllSeries();
        var hasTrakt = series.All(a => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(a.AniDB_ID).Any());
        // TODO optimize this a bunch. Lots of duplicate calls. Contract should be severely trimmed
        var filterable = new Filterable
        {
            Name = group.GroupName,
            SortingName = group.GroupName.GetSortName(),
            SeriesCount = series.Count,
            AirDate = group.Contract.Stat_AirDate_Min,
            LastAirDate = group.Contract?.Stat_EndDate ?? group.GetAllSeries().SelectMany(a => a.GetAnimeEpisodes()).Select(a =>
                a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            MissingEpisodes = group.Contract?.MissingEpisodeCount ?? 0,
            MissingEpisodesCollecting = group.Contract?.MissingEpisodeCountGroups ?? 0,
            Tags = group.Contract?.Stat_AllTags ?? new HashSet<string>(),
            CustomTags = group.Contract?.Stat_AllCustomTags ?? new HashSet<string>(),
            Years = group.Contract?.Stat_AllYears ?? new HashSet<int>(),
            Seasons = group.Contract?.Stat_AllSeasons.Select(a =>
            {
                var parts = a.Split(' ');
                return (int.Parse(parts[1]), Enum.Parse<AnimeSeason>(parts[0]));
            }).ToHashSet(),
            HasTvDBLink = series.All(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a.AniDB_ID).Any()),
            HasMissingTvDbLink = HasMissingTvDBLink(group),
            HasTMDbLink = group.Contract?.Stat_HasMovieDBLink ?? false,
            HasMissingTMDbLink = HasMissingTMDbLink(group),
            HasTraktLink = hasTrakt,
            HasMissingTraktLink = !hasTrakt,
            IsFinished = group.Contract?.Stat_HasFinishedAiring ?? false,
            AddedDate = group.DateTimeCreated,
            LastAddedDate = series.SelectMany(a => a.GetVideoLocals()).Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCount = series.Sum(a => a.GetAnime()?.EpisodeCountNormal ?? 0),
            TotalEpisodeCount = series.Sum(a => a.GetAnime()?.EpisodeCount ?? 0),
            LowestAniDBRating =
                group.Anime.DefaultIfEmpty().Min(anime => decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero)),
            HighestAniDBRating =
                group.Anime.DefaultIfEmpty().Max(anime => decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero)),
            AnimeTypes = new HashSet<string>(group.Anime.Select(a => ((AnimeType)a.AnimeType).ToString()), StringComparer.InvariantCultureIgnoreCase),
            VideoSources = group.Contract?.Stat_AllVideoQuality ?? new HashSet<string>(),
            SharedVideoSources = group.Contract?.Stat_AllVideoQuality_Episodes ?? new HashSet<string>(),
            AudioLanguages = group.Contract?.Stat_AudioLanguages ?? new HashSet<string>(),
            SharedAudioLanguages =
                series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                    .Select(a => a.Languages.Select(b => b.LanguageName)).Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase))
                    .ToHashSet(),
            SubtitleLanguages = group.Contract?.Stat_SubtitleLanguages ?? new HashSet<string>(),
            SharedSubtitleLanguages =
                series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                    .Select(a => a.Subtitles.Select(b => b.LanguageName)).Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase))
                    .ToHashSet(),
        };

        return filterable;
    }

    public static Filterable ToUserDependentFilterable(this SVR_AnimeGroup group, int userID)
    {
        var series = group.GetAllSeries(true);
        var hasTrakt = series.All(a => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(a.AniDB_ID).Any());
        var user = group.GetUserRecord(userID);
        var vote = group.Anime.Select(a => a.UserVote).Where(a => a is { VoteType: (int)VoteType.AnimePermanent or (int)VoteType.AnimeTemporary })
            .Select(a => a.VoteValue).OrderBy(a => a).ToList();
        var hasPermanent = group.Anime.Select(a => a.UserVote).Any(a => a is { VoteType: (int)VoteType.AnimePermanent });
        var missingPermanent =
            group.Anime.Any(a => a.UserVote is not { VoteType: (int)VoteType.AnimePermanent } && a.EndDate != null && a.EndDate > DateTime.Now);
        var watchedDates = series.SelectMany(a => a.GetVideoLocals()).Select(a => a.GetUserRecord(userID)?.WatchedDate).Where(a => a != null).OrderBy(a => a)
            .ToList();
        // TODO optimize this a bunch. Lots of duplicate calls. Contract should be severely trimmed
        var filterable = new UserDependentFilterable
        {
            Name = group.GroupName,
            SortingName = group.GroupName.GetSortName(),
            SeriesCount = series.Count,
            AirDate = group.Contract.Stat_AirDate_Min,
            LastAirDate = group.Contract?.Stat_EndDate ?? group.GetAllSeries().SelectMany(a => a.GetAnimeEpisodes()).Select(a =>
                a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            MissingEpisodes = group.Contract?.MissingEpisodeCount ?? 0,
            MissingEpisodesCollecting = group.Contract?.MissingEpisodeCountGroups ?? 0,
            Tags = group.Contract?.Stat_AllTags ?? new HashSet<string>(),
            CustomTags = group.Contract?.Stat_AllCustomTags ?? new HashSet<string>(),
            Years = group.Contract?.Stat_AllYears ?? new HashSet<int>(),
            Seasons = group.Contract?.Stat_AllSeasons.Select(a =>
            {
                var parts = a.Split(' ');
                return (int.Parse(parts[1]), Enum.Parse<AnimeSeason>(parts[0]));
            }).ToHashSet(),
            HasTvDBLink = series.All(a => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(a.AniDB_ID).Any()),
            HasMissingTvDbLink = HasMissingTvDBLink(group),
            HasTMDbLink = group.Contract?.Stat_HasMovieDBLink ?? false,
            HasMissingTMDbLink = HasMissingTMDbLink(group),
            HasTraktLink = hasTrakt,
            HasMissingTraktLink = !hasTrakt,
            IsFinished = group.Contract?.Stat_HasFinishedAiring ?? false,
            AddedDate = group.DateTimeCreated,
            LastAddedDate = series.SelectMany(a => a.GetVideoLocals()).Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCount = series.Sum(a => a.GetAnime()?.EpisodeCountNormal ?? 0),
            TotalEpisodeCount = series.Sum(a => a.GetAnime()?.EpisodeCount ?? 0),
            LowestAniDBRating =
                group.Anime.DefaultIfEmpty().Min(anime => decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero)),
            HighestAniDBRating =
                group.Anime.DefaultIfEmpty().Max(anime => decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 100, 1, MidpointRounding.AwayFromZero)),
            AnimeTypes = new HashSet<string>(group.Anime.Select(a => ((AnimeType)a.AnimeType).ToString()), StringComparer.InvariantCultureIgnoreCase),
            VideoSources = group.Contract?.Stat_AllVideoQuality ?? new HashSet<string>(),
            SharedVideoSources = group.Contract?.Stat_AllVideoQuality_Episodes ?? new HashSet<string>(),
            AudioLanguages = group.Contract?.Stat_AudioLanguages ?? new HashSet<string>(),
            SharedAudioLanguages =
                series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                    .Select(a => a.Languages.Select(b => b.LanguageName)).Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase))
                    .ToHashSet(),
            SubtitleLanguages = group.Contract?.Stat_SubtitleLanguages ?? new HashSet<string>(),
            SharedSubtitleLanguages =
                series.SelectMany(a => a.GetVideoLocals().Select(b => b.GetAniDBFile())).Where(a => a != null)
                    .Select(a => a.Subtitles.Select(b => b.LanguageName)).Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase))
                    .ToHashSet(),
            IsFavorite = user?.IsFave == 1,
            WatchedEpisodes = user?.WatchedCount ?? 0,
            UnwatchedEpisodes = user?.UnwatchedEpisodeCount ?? 0,
            LowestUserRating = vote.FirstOrDefault(),
            HighestUserRating = vote.LastOrDefault(),
            HasVotes = vote.Any(),
            HasPermanentVotes = hasPermanent,
            MissingPermanentVotes = missingPermanent,
            WatchedDate = watchedDates.FirstOrDefault(),
            LastWatchedDate = watchedDates.LastOrDefault()
        };

        return filterable;
    }

    private static bool HasMissingTMDbLink(SVR_AnimeGroup group)
    {
        return group.GetAllSeries().Any(series =>
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

            return series.Contract?.CrossRefAniDBMovieDB == null;
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
}
