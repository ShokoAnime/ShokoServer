using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Models.Filters.Info;
using Shoko.Server.Models.Filters.Interfaces;
using Shoko.Server.Models.Filters.Logic;
using Shoko.Server.Models.Filters.User;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.Filters;

public static class FilterExtensions
{
    public static IFilterable ToFilterable(this SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
        // TODO optimize this a bunch. Lots of duplicate calls. Contract should be severely trimmed
        var filterable = new Filterable
        {
            AirDate = anime?.AirDate,
            MissingEpisodes = series.Contract?.MissingEpisodeCount ?? 0,
            MissingEpisodesCollecting = series.Contract?.MissingEpisodeCountGroups ?? 0,
            Tags = anime?.GetAllTags() ?? new HashSet<string>(),
            CustomTags = series.Contract?.AniDBAnime?.CustomTags?.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? new HashSet<string>(),
            Years = GetYears(series),
            Seasons = anime.GetSeasons().ToHashSet(),
            HasTvDBLink = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTvDbLink = HasMissingTvDBLink(series),
            HasTMDbLink = series.Contract?.CrossRefAniDBMovieDB != null,
            HasMissingTMDbLink = HasMissingTMDbLink(series),
            HasTraktLink = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTraktLink = !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            IsFinished = series.Contract?.AniDBAnime?.AniDBAnime?.EndDate != null && series.Contract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now,
            LastAirDate = series.EndDate ?? series.GetAnimeEpisodes().Select(a => a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            AddedDate = series.DateTimeCreated,
            LastAddedDate = series.GetVideoLocals().Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCount = anime?.EpisodeCountNormal ?? 0,
            TotalEpisodeCount = anime?.EpisodeCount ?? 0,
            LowestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 10, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 10, 1, MidpointRounding.AwayFromZero),
            AnimeTypes = anime == null ? new HashSet<string>() : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ((AnimeType)anime.AnimeType).ToString() },
            VideoSources = series.Contract?.AniDBAnime?.Stat_AllVideoQuality_Episodes ?? new HashSet<string>(),
            AudioLanguages = series.Contract?.AniDBAnime?.Stat_AudioLanguages ?? new HashSet<string>(),
            SubtitleLanguages = series.Contract?.AniDBAnime?.Stat_SubtitleLanguages ?? new HashSet<string>()
        };

        return filterable;
    }

    private static IReadOnlySet<int> GetYears(SVR_AnimeSeries series)
    {
        var contract = series.Contract?.AniDBAnime;
        var startyear = contract?.AniDBAnime?.BeginYear ?? 0;
        if (startyear == 0) return new HashSet<int>();
        var endyear = contract?.AniDBAnime?.EndYear ?? 0;
        if (endyear == 0) endyear = DateTime.Today.Year;
        if (endyear < startyear) endyear = startyear;
        if (startyear == endyear) return new HashSet<int> { startyear };
        return new HashSet<int>(Enumerable.Range(startyear, endyear - startyear + 1).Where(contract.IsInYear));
    }

    private static bool HasMissingTMDbLink(SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
        if (anime == null) return false;
        // TODO update this with the TMDB refactor
        if (anime.AnimeType != (int)AnimeType.Movie) return false;
        if (anime.Restricted > 0) return false;
        return series.Contract?.CrossRefAniDBMovieDB == null;
    }

    private static bool HasMissingTvDBLink(SVR_AnimeSeries series)
    {
        var anime = series.GetAnime();
        if (anime == null) return false;
        if (anime.AnimeType == (int)AnimeType.Movie) return false;
        if (anime.Restricted > 0) return false;
        return !RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any();
    }

    public static UserDependentFilterable ToUserDependentFilterable(this SVR_AnimeSeries series, int userID)
    {
        var anime = series.GetAnime();
        var user = series.GetUserRecord(userID);
        var vote = anime?.UserVote;
        var watchedDates = series.GetVideoLocals().Select(a => a.GetUserRecord(userID)?.WatchedDate).Where(a => a != null).OrderBy(a => a).ToList();
        var filterable = new UserDependentFilterable
        {
            AirDate = anime?.AirDate,
            MissingEpisodes = series.Contract?.MissingEpisodeCount ?? 0,
            MissingEpisodesCollecting = series.Contract?.MissingEpisodeCountGroups ?? 0,
            Tags = anime?.GetAllTags() ?? new HashSet<string>(),
            CustomTags = series.Contract?.AniDBAnime?.CustomTags?.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? new HashSet<string>(),
            Years = GetYears(series),
            Seasons = anime?.GetSeasons().ToHashSet(),
            HasTvDBLink = RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTvDbLink = HasMissingTvDBLink(series),
            HasTMDbLink = series.Contract?.CrossRefAniDBMovieDB != null,
            HasMissingTMDbLink = HasMissingTMDbLink(series),
            HasTraktLink = RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            HasMissingTraktLink = !RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(series.AniDB_ID).Any(),
            IsFinished = series.Contract?.AniDBAnime?.AniDBAnime?.EndDate != null && series.Contract.AniDBAnime.AniDBAnime.EndDate.Value < DateTime.Now,
            LastAirDate = series.EndDate ?? series.GetAnimeEpisodes().Select(a => a.AniDB_Episode?.GetAirDateAsDate()).Where(a => a != null).DefaultIfEmpty().Max(),
            AddedDate = series.DateTimeCreated,
            LastAddedDate = series.GetVideoLocals().Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCount = anime?.EpisodeCountNormal ?? 0,
            TotalEpisodeCount = anime?.EpisodeCount ?? 0,
            LowestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 10, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRating = decimal.Round(Convert.ToDecimal(anime?.Rating ?? 00) / 10, 1, MidpointRounding.AwayFromZero),
            AnimeTypes = anime == null ? new HashSet<string>() : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) { ((AnimeType)anime.AnimeType).ToString() },
            VideoSources = series.Contract?.AniDBAnime?.Stat_AllVideoQuality_Episodes ?? new HashSet<string>(),
            AudioLanguages = series.Contract?.AniDBAnime?.Stat_AudioLanguages ?? new HashSet<string>(),
            SubtitleLanguages = series.Contract?.AniDBAnime?.Stat_SubtitleLanguages ?? new HashSet<string>(),
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
}
