using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.UserData.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.MediaInfo;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Filters;

public static class FilterExtensions
{
    #region Series

    public static Filterable ToFilterable(this AnimeSeries series, DateTime now)
    {
        var filterable = new Filterable
        {
            NameDelegate = () =>
                series.Title,
            NamesDelegate = () =>
            {
                var titles = series.Titles.Select(t => t.Value).ToHashSet();
                foreach (var group in series.AllGroupsAbove)
                    titles.Add(group.GroupName);

                return titles;
            },
            AniDBIDsDelegate = () =>
                new HashSet<string>() { series.AniDB_ID.ToString() },
            SortingNameDelegate = () =>
                series.Title.ToSortName(),
            SeriesCountDelegate = () => 1,
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
                series.AniDB_Anime?.YearlySeasons.ToHashSet() ?? [],
            AvailableImageTypesDelegate = () =>
                series.GetAvailableImageTypes(),
            PreferredImageTypesDelegate = () =>
                series.GetPreferredImageTypes(),
            CharacterAppearancesDelegate = () =>
                RepoFactory.AniDB_Anime_Character.GetByAnimeID(series.AniDB_ID)
                    .GroupBy(a => a.CastRoleType)
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
                RepoFactory.AniDB_Anime_Staff.GetByAnimeID(series.AniDB_ID).Select(a => (a.CrewRoleType, a.CreatorID))
                    .Concat(RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(series.AniDB_ID).Select(a => (CrewRoleType: CrewRoleType.Actor, a.CreatorID)))
                    .GroupBy(a => a.CrewRoleType)
                    .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CreatorID.ToString()).ToHashSet()),
            HasTmdbLinkDelegate = () =>
                series.TmdbShowCrossReferences.Count is > 0 || series.TmdbMovieCrossReferences.Count is > 0,
            HasTmdbAutoLinkingDisabledDelegate = () =>
                series.IsTMDBAutoMatchingDisabled,
            AutomaticTmdbEpisodeLinksDelegate = () =>
                series.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified) +
                series.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified),
            UserVerifiedTmdbEpisodeLinksDelegate = () =>
                series.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified) +
                series.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified),
            MissingTmdbEpisodeLinksDelegate = () =>
            {
                var allTmdbLinkedEpisodes = series.TmdbEpisodeCrossReferences.Select(a => a.AnidbEpisodeID)
                    .Concat(series.TmdbMovieCrossReferences.Select(a => a.AnidbEpisodeID))
                    .ToHashSet();
                return series.AnimeEpisodes.Count(a => !allTmdbLinkedEpisodes.Contains(a.AnimeEpisodeID));
            },
            IsFinishedDelegate = () =>
                series.AniDB_Anime?.EndDate is { } endDate && endDate < now.Date,
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
                double.Round(Convert.ToDouble(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            HighestAniDBRatingDelegate = () =>
                double.Round(Convert.ToDouble(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AverageAniDBRatingDelegate = () =>
                double.Round(Convert.ToDouble(series.AniDB_Anime?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero),
            AnimeTypesDelegate = () =>
                series.AniDB_Anime is { } anime
                    ? new HashSet<AnimeType> { anime.AnimeType }
                    : [],
            VideoSourcesDelegate = () =>
                series.VideoLocals.Select(a => a.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet(),
            SharedVideoSourcesDelegate = () =>
                series.VideoLocals.Select(b => b.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet() is { Count: > 0 } sources ? sources : [],
            AudioLanguagesDelegate = () => series.VideoLocals
                .Select(a => a.ReleaseInfo)
                .WhereNotNull()
                .SelectMany(a => a.AudioLanguages?.Select(b => b.ToString()) ?? [])
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedAudioLanguagesDelegate = () =>
                series.VideoLocals.Select(b => b.ReleaseInfo).WhereNotNull().Select(a => a.AudioLanguages?.Select(b => b.GetString()) ?? []).ToList() is { Count: > 0 } audioNames
                    ? audioNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            SubtitleLanguagesDelegate = () => series.VideoLocals
                .Select(a => a.ReleaseInfo)
                .WhereNotNull()
                .SelectMany(a => a.SubtitleLanguages?.Select(b => b.ToString()) ?? [])
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase),
            SharedSubtitleLanguagesDelegate = () =>
                series.VideoLocals.Select(b => b.ReleaseInfo).WhereNotNull().Select(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? []).ToList() is { Count: > 0 } subtitleNames
                    ? subtitleNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            ResolutionsDelegate = () =>
                series.VideoLocals
                    .Where(a => a.MediaInfo?.VideoStream is not null)
                    .Select(a => MediaInfoUtility.GetStandardResolution(Tuple.Create(a.MediaInfo!.VideoStream!.Width, a.MediaInfo!.VideoStream!.Height)))
                    .ToHashSet(),
            ManagedFolderIDsDelegate = () =>
                series.VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolderID.ToString()).WhereNotNull().ToHashSet(),
            ManagedFolderNamesDelegate = () =>
                series.VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolder?.Name).WhereNotNull().ToHashSet(),
            FilePathsDelegate = () =>
                series.VideoLocals.Select(a => a.FirstValidPlace?.RelativePath).WhereNotNull().ToHashSet(),
            ReleaseGroupNamesDelegate = () =>
                series.VideoLocals.Select(a => a.ReleaseGroup?.Name).WhereNotNull().ToHashSet(),

        };

        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this AnimeSeries series, int userID, DateTime now)
    {
        var anime = series.AniDB_Anime;
        var user = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, series.AnimeSeriesID);
        var watchedDates = series.VideoLocals
            .Select(a => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate)
            .WhereNotNull()
            .Order()
            .ToList();
        var filterable = new FilterableUserInfo
        {
            IsFavoriteDelegate = () => user?.IsFavorite ?? false,
            UserTagsDelegate = () => user?.UserTags.ToHashSet() ?? [],
            WatchedEpisodesDelegate = () => user?.WatchedEpisodeCount ?? 0,
            UnwatchedEpisodesDelegate = () => user?.UnwatchedEpisodeCount ?? 0,
            LowestUserRatingDelegate = () => user?.UserRating ?? 0,
            HighestUserRatingDelegate = () => user?.UserRating ?? 0,
            HasVotesDelegate = () => user is { HasUserRating: true },
            HasPermanentVotesDelegate = () => user is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent },
            SeriesVoteCountDelegate = () =>
                user is { HasUserRating: true } ? 1 : 0,
            SeriesTemporaryVoteCountDelegate = () =>
                user is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Temporary } ? 1 : 0,
            SeriesPermanentVoteCountDelegate = () =>
                user is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent } ? 1 : 0,
            MissingPermanentVotesDelegate = () =>
                user is not { HasUserRating: true } && anime?.EndDate is not null && anime.EndDate > now.Date,
            WatchedDateDelegate = () => watchedDates.FirstOrDefault(),
            LastWatchedDateDelegate = () => watchedDates.LastOrDefault(),
        };
        return filterable;
    }

    #endregion

    #region Group

    public static Filterable ToFilterable(this AnimeGroup group, DateTime now)
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
                result.UnionWith(series.SelectMany(a => a.Titles.Select(t => t.Value)));
                return result;
            },
            AniDBIDsDelegate = () =>
                series.Select(a => a.AniDB_ID.ToString()).ToHashSet(),
            SortingNameDelegate = () =>
                group.GroupName.ToSortName(),
            SeriesCountDelegate = () =>
                series.Count,
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
                group.YearlySeasons,
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
                    .DistinctBy(a => (a.CastRoleType, a.CharacterID))
                    .Select(a => (a.CastRoleType, a.CharacterID))
                    .GroupBy(a => a.CastRoleType)
                    .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CharacterID.ToString()).ToHashSet()),
            CreatorIDsDelegate = () =>
                series.SelectMany(ser => RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(ser.AniDB_ID))
                    .Select(a => a.CreatorID.ToString())
                    .Concat(series.SelectMany(ser => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(ser.AniDB_ID).Select(a => a.CreatorID.ToString())))
                    .ToHashSet(),
            CreatorRolesDelegate = () =>
                series.SelectMany(ser => RepoFactory.AniDB_Anime_Staff.GetByAnimeID(ser.AniDB_ID))
                    .Select(a => (a.CrewRoleType, a.CreatorID))
                    .DistinctBy(a => (a.CrewRoleType, a.CreatorID))
                    .Concat(
                        series.SelectMany(ser => RepoFactory.AniDB_Anime_Character_Creator.GetByAnimeID(ser.AniDB_ID)
                            .DistinctBy(a => a.CreatorID)
                            .Select(a => (CrewRoleType: CrewRoleType.Actor, a.CreatorID)))
                    )
                    .GroupBy(a => a.CrewRoleType)
                    .ToDictionary(a => a.Key, a => (IReadOnlySet<string>)a.Select(b => b.CreatorID.ToString()).ToHashSet()),
            HasTmdbLinkDelegate = () =>
                series.Any(a => a.TmdbShowCrossReferences.Count is > 0 || a.TmdbMovieCrossReferences.Count is > 0),
            HasTmdbAutoLinkingDisabledDelegate = () =>
                series.Any(a => a.IsTMDBAutoMatchingDisabled),
            AutomaticTmdbEpisodeLinksDelegate = () =>
                series.Sum(a =>
                    a.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified) +
                    a.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is not MatchRating.UserVerified)
                ),
            UserVerifiedTmdbEpisodeLinksDelegate = () =>
                series.Sum(a =>
                    a.TmdbEpisodeCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified) +
                    a.TmdbMovieCrossReferences.Count(xref => xref.MatchRating is MatchRating.UserVerified)
                ),
            MissingTmdbEpisodeLinksDelegate = () => series.Aggregate(0, (acc, ser) =>
            {
                var allTmdbLinkedEpisodes = ser.TmdbEpisodeCrossReferences.Select(a => a.AnidbEpisodeID)
                    .Concat(ser.TmdbMovieCrossReferences.Select(a => a.AnidbEpisodeID))
                    .ToHashSet();
                return acc + ser.AnimeEpisodes.Count(a => !allTmdbLinkedEpisodes.Contains(a.AnimeEpisodeID));
            }),
            IsFinishedDelegate = () =>
                series.All(a => a.EndDate is not null && a.EndDate <= now.Date),
            AddedDateDelegate = () =>
                group.DateTimeCreated,
            LastAddedDateDelegate = () =>
                series.SelectMany(a => a.VideoLocals).Select(a => a.DateTimeCreated).DefaultIfEmpty().Max(),
            EpisodeCountDelegate = () =>
                series.Sum(a => a.AniDB_Anime?.EpisodeCountNormal ?? 0),
            TotalEpisodeCountDelegate = () =>
                series.Sum(a => a.AniDB_Anime?.EpisodeCount ?? 0),
            LowestAniDBRatingDelegate = () =>
                anime.Select(a => double.Round(Convert.ToDouble(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Min(),
            HighestAniDBRatingDelegate = () =>
                anime.Select(a => double.Round(Convert.ToDouble(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Max(),
            AverageAniDBRatingDelegate = () =>
                anime.Select(a => double.Round(Convert.ToDouble(a?.Rating ?? 0) / 100, 1, MidpointRounding.AwayFromZero)).DefaultIfEmpty().Average(),
            AnimeTypesDelegate = () =>
                new HashSet<AnimeType>(anime.Select(a => a.AnimeType)),
            VideoSourcesDelegate = () =>
                series.SelectMany(a => a.VideoLocals).Select(a => a.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet(),
            SharedVideoSourcesDelegate = () =>
                series.SelectMany(a => a.VideoLocals).Select(b => b.ReleaseInfo).WhereNotNull().Select(a => a.LegacySource).ToHashSet() is { Count: > 0 } sources ? sources : [],
            AudioLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.ReleaseInfo)).WhereNotNull().SelectMany(a => a.AudioLanguages?.Select(b => b.GetString()) ?? []).ToHashSet(),
            SharedAudioLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.ReleaseInfo)).WhereNotNull().Select(a => a.AudioLanguages?.Select(b => b.GetString()) ?? []).ToList() is { Count: > 0 } audioLanguageNames
                    ? audioLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            SubtitleLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.ReleaseInfo)).WhereNotNull().SelectMany(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? []).ToHashSet(),
            SharedSubtitleLanguagesDelegate = () =>
                series.SelectMany(a => a.VideoLocals.Select(b => b.ReleaseInfo)).WhereNotNull().Select(a => a.SubtitleLanguages?.Select(b => b.GetString()) ?? []).ToList() is { Count: > 0 } subtitleLanguageNames
                    ? subtitleLanguageNames.Aggregate((a, b) => a.Intersect(b, StringComparer.InvariantCultureIgnoreCase)).ToHashSet()
                    : [],
            ResolutionsDelegate = () =>
                series
                    .SelectMany(a => a.VideoLocals)
                    .Where(a => a.MediaInfo?.VideoStream is not null)
                    .Select(a => MediaInfoUtility.GetStandardResolution(Tuple.Create(a.MediaInfo!.VideoStream!.Width, a.MediaInfo!.VideoStream!.Height)))
                    .ToHashSet(),
            ManagedFolderIDsDelegate = () =>
                series.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolderID.ToString())).WhereNotNull().ToHashSet(),
            ManagedFolderNamesDelegate = () =>
                series.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.ManagedFolder?.Name)).WhereNotNull().ToHashSet(),
            FilePathsDelegate = () =>
                series.SelectMany(s => s.VideoLocals.Select(a => a.FirstValidPlace?.RelativePath)).WhereNotNull().ToHashSet(),
            ReleaseGroupNamesDelegate = () =>
                series.SelectMany(s => s.VideoLocals.Select(a => a.ReleaseGroup?.Name)).WhereNotNull().ToHashSet(),
        };
        return filterable;
    }

    public static FilterableUserInfo ToFilterableUserInfo(this AnimeGroup group, int userID, DateTime now)
    {
        var series = group.AllSeries;
        var anime = group.Anime;
        var user = RepoFactory.AnimeGroup_User.GetByUserAndGroupID(userID, group.AnimeGroupID);
        var seriesUserDict = series.Select(a => RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, a.AnimeSeriesID))
            .WhereNotNull()
            .ToDictionary(a => a.AnimeSeriesID);
        var ratings = seriesUserDict.Values
            .Where(u => u.HasUserRating)
            .Select(a => a.UserRating!.Value)
            .Order()
            .ToList();
        var watchedDates = series
            .SelectMany(a => a.VideoLocals)
            .Select(a => RepoFactory.VideoLocalUser.GetByUserAndVideoLocalID(userID, a.VideoLocalID)?.WatchedDate)
            .WhereNotNull()
            .OrderBy(a => a)
            .ToList();

        // we only want to filter by watched states from files that we actually have and exclude trailers/credits, etc
        int GetEpCount(bool getWatched)
        {
            var count = 0;
            foreach (var ep in series.SelectMany(s => s.AnimeEpisodes))
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

        var filterable = new FilterableUserInfo
        {
            IsFavoriteDelegate = () => seriesUserDict.Values.Any(a => a.IsFavorite),
            UserTagsDelegate = () => seriesUserDict.Values.SelectMany(a => a.UserTags).ToHashSet(),
            WatchedEpisodesDelegate = () => GetEpCount(true),
            UnwatchedEpisodesDelegate = () => GetEpCount(false),
            LowestUserRatingDelegate = () => ratings.FirstOrDefault(),
            HighestUserRatingDelegate = () => ratings.LastOrDefault(),
            HasVotesDelegate = () => ratings.Count > 0,
            HasPermanentVotesDelegate = () => seriesUserDict.Values.Any(a => a is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent }),
            SeriesVoteCountDelegate = () =>
                seriesUserDict.Count,
            SeriesTemporaryVoteCountDelegate = () =>
                seriesUserDict.Values.Count(userData => userData is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Temporary }),
            SeriesPermanentVoteCountDelegate = () =>
                seriesUserDict.Values.Count(userData => userData is { HasUserRating: true, UserRatingVoteType: SeriesVoteType.Permanent }),
            MissingPermanentVotesDelegate = () => series.Any(ser => !(seriesUserDict.TryGetValue(ser.AnimeSeriesID, out var userData) && userData.HasUserRating) && ser.EndDate is not null && ser.EndDate > now.Date),
            WatchedDateDelegate = () => watchedDates.FirstOrDefault(),
            LastWatchedDateDelegate = () => watchedDates.LastOrDefault()
        };
        return filterable;
    }

    #endregion
}
