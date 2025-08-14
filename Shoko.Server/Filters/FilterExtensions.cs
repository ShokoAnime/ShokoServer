using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.MediaInfo;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

using AnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

#nullable enable
namespace Shoko.Server.Filters;

public static class FilterExtensions
{
    #region Filter

    public static bool IsDirectory(this FilterPreset filter) => filter.FilterType.HasFlag(GroupFilterType.Directory);

    #endregion

    #region Series

    public static Filterable ToFilterable(this SVR_AnimeSeries series)
    {
        var filterable = new Filterable
        {
            NameDelegate = () =>
                series.PreferredTitle,
            NamesDelegate = () =>
            {
                var titles = series.Titles.Select(t => t.Title).ToHashSet();
                foreach (var group in series.AllGroupsAbove)
                    titles.Add(group.GroupName);

                return titles;
            },
            AniDBIDsDelegate = () =>
                new HashSet<string>() { series.AniDB_ID.ToString() },
            SortingNameDelegate = () =>
                series.PreferredTitle.ToSortName(),
            SeriesCountDelegate = () => 1,
            SeriesVoteCountDelegate = () =>
                RepoFactory.AniDB_Vote.GetByAnimeID(series.AniDB_ID) is { VoteValue: >= 0 } ? 1 : 0,
            SeriesTemporaryVoteCountDelegate = () =>
                RepoFactory.AniDB_Vote.GetByAnimeID(series.AniDB_ID) is { VoteValue: >= 0, VoteType: (int)AniDBVoteType.AnimeTemp } ? 1 : 0,
            SeriesPermanentVoteCountDelegate = () =>
                RepoFactory.AniDB_Vote.GetByAnimeID(series.AniDB_ID) is { VoteValue: >= 0, VoteType: (int)AniDBVoteType.Anime } ? 1 : 0,
            AirDateDelegate = () =>
                series.AniDB_Anime?.AirDate,
            MissingEpisodesDelegate = () =>
                series.MissingEpisodeCount,
            MissingEpisodesCollectingDelegate = () =>
                series.MissingEpisodeCountGroups,
            VideoFilesDelegate = () =>
                series.VideoLocals.Count,
            AnidbTagIDsDelegate = () =>
                series.AniDB_Anime?.Tags.Select(a => a.TagID.ToString()).ToHashSet() ?? [],
            AnidbTagsDelegate = () =>
                series.AniDB_Anime?.Tags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? [],
            CustomTagIDsDelegate = () =>
                series.AniDB_Anime?.CustomTags.Select(a => a.CustomTagID.ToString()).ToHashSet() ?? [],
            CustomTagsDelegate = () =>
                series.AniDB_Anime?.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? [],
            YearsDelegate = () =>
                series.Years,
            SeasonsDelegate = () =>
                series.AniDB_Anime?.Seasons.ToHashSet() ?? [],
            AvailableImageTypesDelegate = () =>
                series.GetAvailableImageTypes(),
            PreferredImageTypesDelegate = () =>
                series.GetPreferredImageTypes(),
            CharacterAppearancesDelegate = () =>
                RepoFactory.AniDB_Anime_Character.GetByAnimeID(series.AniDB_ID)
                    .GroupBy(a => a.AppearanceType)
                    .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CharacterID.ToString()).ToHashSet()),
            CharacterIDsDelegate = () =>
                RepoFactory.AniDB_Anime_Character.GetByAnimeID(series.AniDB_ID)
                    .Select(a => a.CharacterID.ToString())
                    .ToHashSet(),
            CreatorIDsDelegate = () =>
                RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(series.AniDB_ID).Select(a => a.CreatorID.ToString())
                    .Concat(RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID).Select(a => a.CreatorID.ToString()))
                    .ToHashSet(),
            CreatorRolesDelegate = () =>
                RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID).Select(a => (a.RoleType, a.CreatorID))
                    .Concat(RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(series.AniDB_ID).Select(a => (RoleType: CreatorRoleType.Actor, a.CreatorID)))
                    .GroupBy(a => a.RoleType)
                    .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CreatorID.ToString()).ToHashSet()),
            HasTmdbLinkDelegate = () =>
                series.TmdbShowCrossReferences.Count is > 0 || series.TmdbMovieCrossReferences.Count is > 0,
            HasTmdbAutoLinkingDisabledDelegate = () =>
                series.IsTMDBAutoMatchingDisabled,
            AutomaticTmdbEpisodeLinksDelegate = () =>
                series.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified) +
                series.TmdbMovieCrossReferences.Count(xref => xref.Source is not CrossRefSource.User),
            UserVerifiedTmdbEpisodeLinksDelegate = () =>
                series.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified) +
                series.TmdbMovieCrossReferences.Count(xref => xref.Source is CrossRefSource.User),
            HasTraktLinkDelegate = () =>
                series.TraktShowCrossReferences.Count is > 0,
            HasTraktAutoLinkingDisabledDelegate = () =>
                series.IsTraktAutoMatchingDisabled,
            IsFinishedDelegate = () =>
                series.AniDB_Anime?.EndDate is { } endDate && endDate < DateTime.Now,
            LastAirDateDelegate = () =>
                series.EndDate ?? series.AllAnimeEpisodes.Select(a => a.AniDB_Episode?.GetAirDateAsDate()).WhereNotNull().DefaultIfEmpty().Max(),
            AddedDateDelegate = () =>
                series.DateTimeCreated,
            LastAddedDateDelegate = () =>
                series.VideoLocals.Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCountDelegate = () =>
                series.AniDB_Anime?.EpisodeCountNormal ?? 0,
            TotalEpisodeCountDelegate = () =>
                series.AniDB_Anime?.EpisodeCount ?? 0,
            LowestAniDBRatingDelegate = () =>
                decimal.Round(Convert.ToDecimal(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRatingDelegate = () =>
                decimal.Round(Convert.ToDecimal(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AverageAniDBRatingDelegate = () =>
                decimal.Round(Convert.ToDecimal(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AnimeTypesDelegate = () =>
                series.AniDB_Anime is { } anime
                    ? new HashSet<AnimeType> { anime.AbstractAnimeType }
                    : [],
            VideoSourcesDelegate = () =>
                series.VideoLocals.Select(a => a.AniDBFile).WhereNotNull().Select(a => a.File_Source).ToHashSet(),
            SharedVideoSourcesDelegate = () =>
                series.VideoLocals.Select(b => b.AniDBFile).WhereNotNull().Select(a => a.File_Source).ToHashSet() is { Count: > 0 } sources ? sources : [],
            AudioLanguagesDelegate = () => series.VideoLocals
                .Select(a => a.AniDBFile)
                .WhereNotNull()
                .SelectMany(a => a.Languages.Select(b => b.LanguageName))
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedAudioLanguagesDelegate = () =>
                series.VideoLocals.Select(b => b.AniDBFile).WhereNotNull().Select(a => a.Languages.Select(b => b.LanguageName)).ToList() is { Count: > 0 } audioNames
                    ? audioNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            SubtitleLanguagesDelegate = () =>
                series.VideoLocals.Select(a => a.AniDBFile).WhereNotNull().SelectMany(a => a.Subtitles.Select(b => b.LanguageName)).ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedSubtitleLanguagesDelegate = () =>
                series.VideoLocals.Select(b => b.AniDBFile).WhereNotNull().Select(a => a.Subtitles.Select(b => b.LanguageName)).ToList() is { Count: > 0 } subtitleNames
                    ? subtitleNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            ResolutionsDelegate = () =>
                series.VideoLocals
                    .Where(a => a.MediaInfo?.VideoStream is not null)
                    .Select(a => MediaInfoUtils.GetStandardResolution(Tuple.Create(a.MediaInfo!.VideoStream!.Width, a.MediaInfo!.VideoStream!.Height)))
                    .ToHashSet(),
            ImportFolderIDsDelegate = () =>
                series.VideoLocals.Select(a => a.FirstValidPlace?.ImportFolderID.ToString()).WhereNotNull().ToHashSet(),
            ImportFolderNamesDelegate = () =>
                series.VideoLocals.Select(a => a.FirstValidPlace?.ImportFolder?.ImportFolderName).WhereNotNull().ToHashSet(),
            FilePathsDelegate = () =>
                series.VideoLocals.Select(a => a.FirstValidPlace?.FilePath).WhereNotNull().ToHashSet(),
        };

        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this SVR_AnimeSeries series, int userID)
    {
        var anime = series.AniDB_Anime;
        var user = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);
        var vote = anime?.UserVote;
        var watchedDates = series.VideoLocals
            .Select(a => RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate)
            .WhereNotNull()
            .OrderBy(a => a)
            .ToList();
        var filterable = new FilterableUserInfo
        {
            IsFavoriteDelegate = () => false,
            WatchedEpisodesDelegate = () => user?.WatchedEpisodeCount ?? 0,
            UnwatchedEpisodesDelegate = () => user?.UnwatchedEpisodeCount ?? 0,
            LowestUserRatingDelegate = () => vote?.VoteValue ?? 0,
            HighestUserRatingDelegate = () => vote?.VoteValue ?? 0,
            HasVotesDelegate = () => vote is not null,
            HasPermanentVotesDelegate = () => vote is { VoteType: (int)AniDBVoteType.Anime },
            MissingPermanentVotesDelegate = () => vote is not { VoteType: (int)AniDBVoteType.Anime } && anime?.EndDate is not null && anime.EndDate > DateTime.Now,
            WatchedDateDelegate = () => watchedDates.FirstOrDefault(),
            LastWatchedDateDelegate = () => watchedDates.LastOrDefault()
        };
        return filterable;
    }

    #endregion

    #region Group

    public static Filterable ToFilterable(this SVR_AnimeGroup group)
    {
        var series = group.AllSeries;
        var anime = group.Anime;
        var filterable = new Filterable
        {
            NameDelegate = () =>
                group.GroupName,
            NamesDelegate = () =>
            {
                var result = new HashSet<string>() { group.GroupName };
                foreach (var grp in group.AllGroupsAbove)
                    result.Add(grp.GroupName);
                result.UnionWith(series.SelectMany(a => a.Titles.Select(t => t.Title)));
                return result;
            },
            AniDBIDsDelegate = () =>
                series.Select(a => a.AniDB_ID.ToString()).ToHashSet(),
            SortingNameDelegate = () =>
                group.GroupName.ToSortName(),
            SeriesCountDelegate = () =>
                series.Count,
            SeriesVoteCountDelegate = () =>
                series.Count(ser => RepoFactory.AniDB_Vote.GetByAnimeID(ser.AniDB_ID) is { VoteValue: >= 0 }),
            SeriesTemporaryVoteCountDelegate = () =>
                series.Count(ser => RepoFactory.AniDB_Vote.GetByAnimeID(ser.AniDB_ID) is { VoteValue: >= 0, VoteType: (int)AniDBVoteType.AnimeTemp }),
            SeriesPermanentVoteCountDelegate = () =>
                series.Count(ser => RepoFactory.AniDB_Vote.GetByAnimeID(ser.AniDB_ID) is { VoteValue: >= 0, VoteType: (int)AniDBVoteType.Anime }),
            AirDateDelegate = () =>
                series.Select(a => a.AirDate).DefaultIfEmpty(DateTime.MaxValue).Min(),
            LastAirDateDelegate = () =>
                series.SelectMany(a => a.AllAnimeEpisodes).Select(a =>
                a.AniDB_Episode?.GetAirDateAsDate()).WhereNotNull().DefaultIfEmpty().Max(),
            MissingEpisodesDelegate = () =>
                group.MissingEpisodeCount,
            MissingEpisodesCollectingDelegate = () =>
                group.MissingEpisodeCountGroups,
            VideoFilesDelegate = () =>
                series.SelectMany(s => s.VideoLocals).DistinctBy(a => a.VideoLocalID).Count(),
            AnidbTagIDsDelegate = () =>
                group.Tags.Select(a => a.TagID.ToString()).ToHashSet(),
            AnidbTagsDelegate = () =>
                group.Tags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            CustomTagIDsDelegate = () =>
                group.CustomTags.Select(a => a.CustomTagID.ToString()).ToHashSet(),
            CustomTagsDelegate = () =>
                group.CustomTags.Select(a => a.TagName).ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            YearsDelegate = () =>
                group.Years,
            SeasonsDelegate = () =>
                group.Seasons,
            AvailableImageTypesDelegate = () =>
                group.AvailableImageTypes,
            PreferredImageTypesDelegate = () =>
                group.PreferredImageTypes,
            CharacterIDsDelegate = () =>
                series.SelectMany(ser => RepoFactory.AniDB_Anime_Character.GetByAnimeID(ser.AniDB_ID))
                    .Select(a => a.CharacterID.ToString())
                    .ToHashSet(),
            CharacterAppearancesDelegate = () =>
                series.SelectMany(ser => RepoFactory.AniDB_Anime_Character.GetByAnimeID(ser.AniDB_ID))
                    .DistinctBy(a => (a.AppearanceType, a.CharacterID))
                    .Select(a => (a.AppearanceType, a.CharacterID))
                    .GroupBy(a => a.AppearanceType)
                    .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CharacterID.ToString()).ToHashSet()),
            CreatorIDsDelegate = () =>
                series.SelectMany(ser => RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(ser.AniDB_ID))
                    .Select(a => a.CreatorID.ToString())
                    .Concat(series.SelectMany(ser => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(ser.AniDB_ID).Select(a => a.CreatorID.ToString())))
                    .ToHashSet(),
            CreatorRolesDelegate = () =>
                series.SelectMany(ser => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(ser.AniDB_ID))
                    .Select(a => (a.RoleType, a.CreatorID))
                    .DistinctBy(a => (a.RoleType, a.CreatorID))
                    .Concat(
                        series.SelectMany(ser => RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(ser.AniDB_ID)
                            .DistinctBy(a => a.CreatorID)
                            .Select(a => (RoleType: CreatorRoleType.Actor, a.CreatorID)))
                    )
                    .GroupBy(a => a.RoleType)
                    .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CreatorID.ToString()).ToHashSet()),
            HasTmdbLinkDelegate = () =>
                series.Any(a => a.TmdbShowCrossReferences.Count is > 0 || a.TmdbMovieCrossReferences.Count is > 0),
            HasTmdbAutoLinkingDisabledDelegate = () =>
                series.Any(a => a.IsTMDBAutoMatchingDisabled),
            AutomaticTmdbEpisodeLinksDelegate = () =>
                series.Sum(a =>
                    a.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified) +
                    a.TmdbMovieCrossReferences.Count(xref => xref.Source is not CrossRefSource.User)
                ),
            UserVerifiedTmdbEpisodeLinksDelegate = () =>
                series.Sum(a =>
                    a.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified) +
                    a.TmdbMovieCrossReferences.Count(xref => xref.Source is CrossRefSource.User)
                ),
            HasTraktLinkDelegate = () =>
                series.Any(a => a.TraktShowCrossReferences.Count is > 0),
            HasTraktAutoLinkingDisabledDelegate = () =>
                series.Any(a => a.IsTraktAutoMatchingDisabled),
            IsFinishedDelegate = () =>
                series.All(a => a.EndDate is not null && a.EndDate <= DateTime.Today),
            AddedDateDelegate = () =>
                group.DateTimeCreated,
            LastAddedDateDelegate = () =>
                series.SelectMany(a => a.VideoLocals).Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCountDelegate = () =>
                series.Sum(a => a.AniDB_Anime?.EpisodeCountNormal ?? 0),
            TotalEpisodeCountDelegate = () =>
                series.Sum(a => a.AniDB_Anime?.EpisodeCount ?? 0),
            LowestAniDBRatingDelegate = () =>
                anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Min(),
            HighestAniDBRatingDelegate = () =>
                anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Max(),
            AverageAniDBRatingDelegate = () =>
                anime.Select(a => decimal.Round(Convert.ToDecimal(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Average(),
            AnimeTypesDelegate = () =>
                new HashSet<AnimeType>(anime.Select(a => a.AbstractAnimeType)),
            VideoSourcesDelegate = () =>
                series.SelectMany(a => a.VideoLocals).Select(a => a.AniDBFile).WhereNotNull().Select(a => a.File_Source).ToHashSet(),
            SharedVideoSourcesDelegate = () =>
                series.SelectMany(a => a.VideoLocals).Select(b => b.AniDBFile).WhereNotNull().Select(a => a.File_Source).ToHashSet() is { Count: > 0 } sources ? sources : [],
            AudioLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).WhereNotNull().SelectMany(a => a.Languages.Select(b => b.LanguageName)).ToHashSet(),
            SharedAudioLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).WhereNotNull().Select(a => a.Languages.Select(b => b.LanguageName)).ToList() is { Count: > 0 } audioLanguageNames
                    ? audioLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            SubtitleLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).WhereNotNull().SelectMany(a => a.Subtitles.Select(b => b.LanguageName)).ToHashSet(),
            SharedSubtitleLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.AniDBFile)).WhereNotNull().Select(a => a.Subtitles.Select(b => b.LanguageName)).ToList() is { Count: > 0 } subtitleLanguageNames
                    ? subtitleLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            ResolutionsDelegate = () =>
                series
                    .SelectMany(a => a.VideoLocals)
                    .Where(a => a.MediaInfo?.VideoStream is not null)
                    .Select(a => MediaInfoUtils.GetStandardResolution(Tuple.Create(a.MediaInfo!.VideoStream!.Width, a.MediaInfo!.VideoStream!.Height)))
                    .ToHashSet(),
            ImportFolderIDsDelegate = () =>
                series.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.ImportFolderID.ToString())).WhereNotNull().ToHashSet(),
            ImportFolderNamesDelegate = () =>
                series.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.ImportFolder?.ImportFolderName)).WhereNotNull().ToHashSet(),
            FilePathsDelegate = () =>
                series.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.FilePath)).WhereNotNull().ToHashSet(),
        };
        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this SVR_AnimeGroup group, int userID)
    {
        var series = group.AllSeries;
        var anime = group.Anime;
        var user = RepoFactory.AnimeGroup_User.GetByUserAndGroupID(userID, group.AnimeGroupID);
        var vote = anime.Select(a => a.UserVote)
            .Where(a => a is { VoteType: (int)VoteType.AnimePermanent or (int)VoteType.AnimeTemporary })
            .WhereNotNull()
            .Select(a => a.VoteValue)
            .OrderBy(a => a)
            .ToList();
        var watchedDates = series.SelectMany(a => a.VideoLocals)
            .Select(a => RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate)
            .WhereNotNull()
            .OrderBy(a => a)
            .ToList();

        // we only want to filter by watched states from files that we actually have and exclude trailers/credits, etc
        int GetEpCount(bool getWatched)
        {
            var count = 0;
            foreach (var ep in series.SelectMany(s => s.AnimeEpisodes))
            {
                if (ep.EpisodeTypeEnum is not (EpisodeType.Episode or EpisodeType.Special)) continue;
                var vls = ep.VideoLocals;
                if (vls.Count == 0 || vls.All(vl => vl.IsIgnored)) continue;

                var isWatched = ep.GetUserRecord(userID)?.IsWatched ?? false;
                if (isWatched == getWatched)
                    count++;
            }
            return count;
        }

        var filterable = new FilterableUserInfo
        {
            IsFavoriteDelegate = () => user?.IsFave == 1,
            WatchedEpisodesDelegate = () => GetEpCount(true),
            UnwatchedEpisodesDelegate = () => GetEpCount(false),
            LowestUserRatingDelegate = () => vote.FirstOrDefault(),
            HighestUserRatingDelegate = () => vote.LastOrDefault(),
            HasVotesDelegate = () => vote.Any(),
            HasPermanentVotesDelegate = () => anime.Select(a => a.UserVote).Any(a => a is { VoteType: (int)VoteType.AnimePermanent }),
            MissingPermanentVotesDelegate = () => anime.Any(a => a.UserVote is not { VoteType: (int)VoteType.AnimePermanent } && a.EndDate is not null && a.EndDate > DateTime.Now),
            WatchedDateDelegate = () => watchedDates.FirstOrDefault(),
            LastWatchedDateDelegate = () => watchedDates.LastOrDefault()
        };
        return filterable;
    }

    #endregion
}
