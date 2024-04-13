using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NLog;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.Extensions;

public static class ModelClients
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    public static CL_ServerSettings ToContract(this IServerSettings settings)
    {
        return new CL_ServerSettings
        {
            AniDB_Username = settings.AniDb.Username,
            AniDB_Password = settings.AniDb.Password,
            AniDB_ServerAddress = Regex.Match(settings.AniDb.HTTPServerUrl, @"https?://[^:]+").ToString(),
            AniDB_ServerPort = Regex.Match(settings.AniDb.HTTPServerUrl, @"(?<=:)\d+").ToString(),
            AniDB_ClientPort = settings.AniDb.ClientPort.ToString(),
            AniDB_AVDumpClientPort = settings.AniDb.AVDumpClientPort.ToString(),
            AniDB_AVDumpKey = settings.AniDb.AVDumpKey,
            AniDB_DownloadRelatedAnime = settings.AniDb.DownloadRelatedAnime,
            AniDB_DownloadSimilarAnime = settings.AniDb.DownloadSimilarAnime,
            AniDB_DownloadReviews = settings.AniDb.DownloadReviews,
            AniDB_DownloadReleaseGroups = settings.AniDb.DownloadReleaseGroups,
            AniDB_MyList_AddFiles = settings.AniDb.MyList_AddFiles,
            AniDB_MyList_StorageState = (int)settings.AniDb.MyList_StorageState,
            AniDB_MyList_DeleteType = (int)settings.AniDb.MyList_DeleteType,
            AniDB_MyList_ReadWatched = settings.AniDb.MyList_ReadWatched,
            AniDB_MyList_ReadUnwatched = settings.AniDb.MyList_ReadUnwatched,
            AniDB_MyList_SetWatched = settings.AniDb.MyList_SetWatched,
            AniDB_MyList_SetUnwatched = settings.AniDb.MyList_SetUnwatched,
            AniDB_MyList_UpdateFrequency = (int)settings.AniDb.MyList_UpdateFrequency,
            AniDB_Calendar_UpdateFrequency = (int)settings.AniDb.Calendar_UpdateFrequency,
            AniDB_Anime_UpdateFrequency = (int)settings.AniDb.Anime_UpdateFrequency,
            AniDB_MyListStats_UpdateFrequency = (int)settings.AniDb.MyListStats_UpdateFrequency,
            AniDB_File_UpdateFrequency = (int)settings.AniDb.File_UpdateFrequency,
            AniDB_DownloadCharacters = settings.AniDb.DownloadCharacters,
            AniDB_DownloadCreators = settings.AniDb.DownloadCreators,
            AniDB_MaxRelationDepth = settings.AniDb.MaxRelationDepth,

            // Web Cache
            WebCache_Address = settings.WebCache.Address,
            WebCache_XRefFileEpisode_Get = settings.WebCache.XRefFileEpisode_Get,
            WebCache_XRefFileEpisode_Send = settings.WebCache.XRefFileEpisode_Send,
            WebCache_TvDB_Get = settings.WebCache.TvDB_Get,
            WebCache_TvDB_Send = settings.WebCache.TvDB_Send,
            WebCache_Trakt_Get = settings.WebCache.Trakt_Get,
            WebCache_Trakt_Send = settings.WebCache.Trakt_Send,

            // TvDB
            TvDB_AutoLink = settings.TvDB.AutoLink,
            TvDB_AutoFanart = settings.TvDB.AutoFanart,
            TvDB_AutoFanartAmount = settings.TvDB.AutoFanartAmount,
            TvDB_AutoPosters = settings.TvDB.AutoPosters,
            TvDB_AutoPostersAmount = settings.TvDB.AutoPostersAmount,
            TvDB_AutoWideBanners = settings.TvDB.AutoWideBanners,
            TvDB_AutoWideBannersAmount = settings.TvDB.AutoWideBannersAmount,
            TvDB_UpdateFrequency = (int)settings.TvDB.UpdateFrequency,
            TvDB_Language = settings.TvDB.Language,

            // MovieDB
            MovieDB_AutoFanart = settings.MovieDb.AutoFanart,
            MovieDB_AutoFanartAmount = settings.MovieDb.AutoFanartAmount,
            MovieDB_AutoPosters = settings.MovieDb.AutoPosters,
            MovieDB_AutoPostersAmount = settings.MovieDb.AutoPostersAmount,

            // Import settings
            VideoExtensions = string.Join(",", settings.Import.VideoExtensions),
            AutoGroupSeries = settings.AutoGroupSeries,
            AutoGroupSeriesUseScoreAlgorithm = settings.AutoGroupSeriesUseScoreAlgorithm,
            AutoGroupSeriesRelationExclusions = string.Join("|", settings.AutoGroupSeriesRelationExclusions).Replace("alternative", "alternate", StringComparison.InvariantCultureIgnoreCase),
            FileQualityFilterEnabled = settings.FileQualityFilterEnabled,
            FileQualityFilterPreferences = SettingsProvider.Serialize(settings.FileQualityPreferences),
            Import_MoveOnImport = settings.Import.MoveOnImport,
            Import_RenameOnImport = settings.Import.RenameOnImport,
            Import_UseExistingFileWatchedStatus = settings.Import.UseExistingFileWatchedStatus,
            RunImportOnStart = settings.Import.RunOnStart,
            ScanDropFoldersOnStart = settings.Import.ScanDropFoldersOnStart,
            Hash_CRC32 = settings.Import.Hash_CRC32,
            Hash_MD5 = settings.Import.Hash_MD5,
            Hash_SHA1 = settings.Import.Hash_SHA1,
            SkipDiskSpaceChecks = settings.Import.SkipDiskSpaceChecks,

            // Language
            LanguagePreference = string.Join(",", settings.LanguagePreference),
            LanguageUseSynonyms = settings.LanguageUseSynonyms,
            EpisodeTitleSource = (int)settings.EpisodeTitleSource,
            SeriesDescriptionSource = (int)settings.SeriesDescriptionSource,
            SeriesNameSource = (int)settings.SeriesNameSource,

            // trakt
            Trakt_IsEnabled = settings.TraktTv.Enabled,
            Trakt_AuthToken = settings.TraktTv.AuthToken,
            Trakt_RefreshToken = settings.TraktTv.RefreshToken,
            Trakt_TokenExpirationDate = settings.TraktTv.TokenExpirationDate,
            Trakt_UpdateFrequency = (int)settings.TraktTv.UpdateFrequency,
            Trakt_SyncFrequency = (int)settings.TraktTv.SyncFrequency,

            // LogRotator
            RotateLogs = settings.LogRotator.Enabled,
            RotateLogs_Delete = settings.LogRotator.Delete,
            RotateLogs_Delete_Days = settings.LogRotator.Delete_Days,
            RotateLogs_Zip = settings.LogRotator.Zip,

            //WebUI
            WebUI_Settings = settings.WebUI_Settings,

            //Plex
            Plex_Sections = string.Join(",", settings.Plex.Libraries),
            Plex_ServerHost = settings.Plex.Server
        };
    }

    public static CL_AniDB_Anime ToClient(this SVR_AniDB_Anime anime)
    {
        return new CL_AniDB_Anime
        {
            AniDB_AnimeID = anime.AniDB_AnimeID,
            AnimeID = anime.AnimeID,
            Description = anime.Description,
            EpisodeCount = anime.EpisodeCount,
            AirDate = anime.AirDate,
            EndDate = anime.EndDate,
            URL = anime.URL,
            Picname = anime.Picname,
            BeginYear = anime.BeginYear,
            EndYear = anime.EndYear,
            AnimeType = anime.AnimeType,
            MainTitle = anime.MainTitle,
            AllTitles = anime.AllTitles,
            AllTags = anime.AllTags,
            EpisodeCountNormal = anime.EpisodeCountNormal,
            EpisodeCountSpecial = anime.EpisodeCountSpecial,
            Rating = anime.Rating,
            VoteCount = anime.VoteCount,
            TempRating = anime.TempRating,
            TempVoteCount = anime.TempVoteCount,
            AvgReviewRating = anime.AvgReviewRating,
            ReviewCount = anime.ReviewCount,
            DateTimeUpdated = anime.GetDateTimeUpdated(),
            DateTimeDescUpdated = anime.DateTimeDescUpdated,
            ImageEnabled = anime.ImageEnabled,
            Restricted = anime.Restricted,
            ANNID = anime.ANNID,
            AllCinemaID = anime.AllCinemaID,
            LatestEpisodeNumber = anime.LatestEpisodeNumber,
            DisableExternalLinksFlag = 0
        };
    }


    public static CL_AniDB_Anime_Relation ToClient(this AniDB_Anime_Relation ar, SVR_AniDB_Anime anime,
        SVR_AnimeSeries ser, int userID)
    {
        var cl = new CL_AniDB_Anime_Relation
        {
            AniDB_Anime_RelationID = ar.AniDB_Anime_RelationID,
            AnimeID = ar.AnimeID,
            RelationType = ar.RelationType,
            RelatedAnimeID = ar.RelatedAnimeID
        };
        cl.AniDB_Anime = anime?.Contract?.AniDBAnime;
        cl.AnimeSeries = ser?.GetUserContract(userID);
        return cl;
    }


    public static CL_AniDB_Character ToClient(this AniDB_Character c)
    {
        return new CL_AniDB_Character
        {
            AniDB_CharacterID = c.AniDB_CharacterID,
            CharID = c.CharID,
            PicName = c.PicName,
            CreatorListRaw = c.CreatorListRaw ?? "",
            CharName = c.CharName,
            CharKanjiName = c.CharKanjiName,
            CharDescription = c.CharDescription
        };
    }

    public static CL_AniDB_GroupStatus ToClient(this AniDB_GroupStatus g)
    {
        return new CL_AniDB_GroupStatus
        {
            AniDB_GroupStatusID = g.AniDB_GroupStatusID,
            AnimeID = g.AnimeID,
            GroupID = g.GroupID,
            GroupName = g.GroupName,
            CompletionState = g.CompletionState,
            LastEpisodeNumber = g.LastEpisodeNumber,
            Rating = g.Rating,
            Votes = g.Votes,
            EpisodeRange = g.EpisodeRange
        };
    }

    public static CL_AnimeEpisode_User ToClient(this AnimeEpisode_User e)
    {
        return new CL_AnimeEpisode_User
        {
            AnimeEpisode_UserID = e.AnimeEpisode_UserID,
            JMMUserID = e.JMMUserID,
            AnimeEpisodeID = e.AnimeEpisodeID,
            AnimeSeriesID = e.AnimeSeriesID,
            WatchedDate = e.WatchedDate,
            PlayedCount = e.PlayedCount,
            WatchedCount = e.WatchedCount,
            StoppedCount = e.StoppedCount
        };
    }

    public static CL_AnimeGroup_User ToClient(this AnimeGroup_User g)
    {
        return new CL_AnimeGroup_User
        {
            AnimeGroup_UserID = g.AnimeGroup_UserID,
            JMMUserID = g.JMMUserID,
            AnimeGroupID = g.AnimeGroupID,
            IsFave = g.IsFave,
            UnwatchedEpisodeCount = g.UnwatchedEpisodeCount,
            WatchedEpisodeCount = g.WatchedEpisodeCount,
            WatchedDate = g.WatchedDate,
            PlayedCount = g.PlayedCount,
            WatchedCount = g.WatchedCount,
            StoppedCount = g.StoppedCount
        };
    }

    public static CL_AnimeSeries_User ToClient(this AnimeSeries_User s)
    {
        return new CL_AnimeSeries_User
        {
            AnimeSeries_UserID = s.AnimeSeries_UserID,
            JMMUserID = s.JMMUserID,
            AnimeSeriesID = s.AnimeSeriesID,
            UnwatchedEpisodeCount = s.UnwatchedEpisodeCount,
            WatchedEpisodeCount = s.WatchedEpisodeCount,
            WatchedDate = s.WatchedDate,
            PlayedCount = s.PlayedCount,
            WatchedCount = s.WatchedCount,
            StoppedCount = s.StoppedCount
        };
    }


    public static CL_IgnoreAnime ToClient(this IgnoreAnime i)
    {
        var c = new CL_IgnoreAnime
        {
            IgnoreAnimeID = i.IgnoreAnimeID, JMMUserID = i.JMMUserID, AnimeID = i.AnimeID, IgnoreType = i.IgnoreType
        };
        c.Anime = RepoFactory.AniDB_Anime.GetByAnimeID(i.AnimeID).ToClient();
        return c;
    }

    public static CL_Trakt_Season ToClient(this Trakt_Season season)
    {
        return new CL_Trakt_Season
        {
            Trakt_SeasonID = season.Trakt_SeasonID,
            Trakt_ShowID = season.Trakt_ShowID,
            Season = season.Season,
            URL = season.URL,
            Episodes = season.GetEpisodes()
        };
    }

    public static CL_Trakt_Show ToClient(this Trakt_Show show)
    {
        return new CL_Trakt_Show
        {
            Trakt_ShowID = show.Trakt_ShowID,
            TraktID = show.TraktID,
            Title = show.Title,
            Year = show.Year,
            URL = show.URL,
            Overview = show.Overview,
            TvDB_ID = show.TvDB_ID,
            Seasons = show.GetSeasons().Select(a => a.ToClient()).ToList()
        };
    }


    public static CL_AniDB_Anime_DefaultImage ToClient(this AniDB_Anime_DefaultImage defaultImage)
    {
        var imgType = (ImageEntityType)defaultImage.ImageParentType;
        IImageEntity parentImage = null;

        switch (imgType)
        {
            case ImageEntityType.TvDB_Banner:
                parentImage = RepoFactory.TvDB_ImageWideBanner.GetByID(defaultImage.ImageParentID);
                break;
            case ImageEntityType.TvDB_Cover:
                parentImage = RepoFactory.TvDB_ImagePoster.GetByID(defaultImage.ImageParentID);
                break;
            case ImageEntityType.TvDB_FanArt:
                parentImage = RepoFactory.TvDB_ImageFanart.GetByID(defaultImage.ImageParentID);
                break;
            case ImageEntityType.MovieDB_Poster:
                parentImage = RepoFactory.MovieDB_Poster.GetByID(defaultImage.ImageParentID);
                break;
            case ImageEntityType.MovieDB_FanArt:
                parentImage = RepoFactory.MovieDB_Fanart.GetByID(defaultImage.ImageParentID);
                break;
        }

        return defaultImage.ToClient(parentImage);
    }

    public static CL_AniDB_Anime_DefaultImage ToClient(this AniDB_Anime_DefaultImage defaultimage,
        IImageEntity parentImage)
    {
        var contract = new CL_AniDB_Anime_DefaultImage
        {
            AniDB_Anime_DefaultImageID = defaultimage.AniDB_Anime_DefaultImageID,
            AnimeID = defaultimage.AnimeID,
            ImageParentID = defaultimage.ImageParentID,
            ImageParentType = defaultimage.ImageParentType,
            ImageType = defaultimage.ImageType
        };
        var imgType = (ImageEntityType)defaultimage.ImageParentType;

        switch (imgType)
        {
            case ImageEntityType.TvDB_Banner:
                contract.TVWideBanner = parentImage as TvDB_ImageWideBanner;
                break;
            case ImageEntityType.TvDB_Cover:
                contract.TVPoster = parentImage as TvDB_ImagePoster;
                break;
            case ImageEntityType.TvDB_FanArt:
                contract.TVFanart = parentImage as TvDB_ImageFanart;
                break;
            case ImageEntityType.MovieDB_Poster:
                contract.MoviePoster = parentImage as MovieDB_Poster;
                break;
            case ImageEntityType.MovieDB_FanArt:
                contract.MovieFanart = parentImage as MovieDB_Fanart;
                break;
        }

        return contract;
    }

    public static CL_AniDB_Anime_Similar ToClient(this AniDB_Anime_Similar similar, SVR_AniDB_Anime anime,
        SVR_AnimeSeries ser, int userID)
    {
        var cl = new CL_AniDB_Anime_Similar
        {
            AniDB_Anime_SimilarID = similar.AniDB_Anime_SimilarID,
            AnimeID = similar.AnimeID,
            SimilarAnimeID = similar.SimilarAnimeID,
            Approval = similar.Approval,
            Total = similar.Total
        };
        cl.AniDB_Anime = anime?.Contract?.AniDBAnime;
        cl.AnimeSeries = ser?.GetUserContract(userID);
        return cl;
    }

    public static CL_AniDB_Character ToClient(this AniDB_Character character, string charType, AniDB_Seiyuu seiyuu)
    {
        var contract = character.ToClient();
        if (seiyuu != null)
        {
            contract.Seiyuu = seiyuu;
        }

        return contract;
    }

    public static CL_AniDB_Character ToClient(this AniDB_Character character, string charType)
    {
        var seiyuu = character.GetSeiyuu();

        return character.ToClient(charType, seiyuu);
    }

    public static CL_BookmarkedAnime ToClient(this BookmarkedAnime bookmarkedanime)
    {
        var cl = new CL_BookmarkedAnime
        {
            BookmarkedAnimeID = bookmarkedanime.BookmarkedAnimeID,
            AnimeID = bookmarkedanime.AnimeID,
            Priority = bookmarkedanime.Priority,
            Notes = bookmarkedanime.Notes,
            Downloading = bookmarkedanime.Downloading
        };
        cl.Anime = null;
        var an = RepoFactory.AniDB_Anime.GetByAnimeID(bookmarkedanime.AnimeID);
        if (an != null)
        {
            cl.Anime = an.Contract.AniDBAnime;
        }

        return cl;
    }

    public static CL_DuplicateFile ToClient(this DuplicateFile duplicatefile)
    {
        var cl = new CL_DuplicateFile
        {
            DuplicateFileID = duplicatefile.DuplicateFileID,
            FilePathFile1 = duplicatefile.FilePathFile1,
            FilePathFile2 = duplicatefile.FilePathFile2,
            Hash = duplicatefile.Hash,
            ImportFolderIDFile1 = duplicatefile.ImportFolderIDFile1,
            ImportFolderIDFile2 = duplicatefile.ImportFolderIDFile2,
            ImportFolder1 = RepoFactory.ImportFolder.GetByID(duplicatefile.ImportFolderIDFile1),
            ImportFolder2 = RepoFactory.ImportFolder.GetByID(duplicatefile.ImportFolderIDFile2),
            DateTimeUpdated = duplicatefile.DateTimeUpdated
        };
        if (duplicatefile.GetAniDBFile() != null)
        {
            var eps = duplicatefile.GetAniDBFile().Episodes;
            if (eps.Count > 0)
            {
                cl.EpisodeNumber = eps[0].EpisodeNumber;
                cl.EpisodeType = eps[0].EpisodeType;
                cl.EpisodeName = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(eps[0].EpisodeID)?.Title;
                cl.AnimeID = eps[0].AnimeID;
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(eps[0].AnimeID);
                if (anime != null)
                {
                    cl.AnimeName = anime.MainTitle;
                }
            }
        }

        return cl;
    }

    public static CL_AniDB_Episode ToClient(this AniDB_Episode ep)
    {
        var titles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.EpisodeID);
        return new CL_AniDB_Episode
        {
            AniDB_EpisodeID = ep.AniDB_EpisodeID,
            EpisodeID = ep.EpisodeID,
            AnimeID = ep.AnimeID,
            LengthSeconds = ep.LengthSeconds,
            Rating = ep.Rating,
            Votes = ep.Votes,
            EpisodeNumber = ep.EpisodeNumber,
            EpisodeType = ep.EpisodeType,
            Description = ep.Description,
            AirDate = ep.AirDate,
            DateTimeUpdated = ep.DateTimeUpdated,
            Titles = titles.ToDictionary(a => a.LanguageCode, a => a.Title)
        };
    }

    public static CL_VideoLocal_Place ToClient(this SVR_VideoLocal_Place vlocalplace)
    {
        var v = new CL_VideoLocal_Place
        {
            FilePath = vlocalplace.FilePath,
            ImportFolderID = vlocalplace.ImportFolderID,
            ImportFolderType = vlocalplace.ImportFolderType,
            VideoLocalID = vlocalplace.VideoLocalID,
            ImportFolder = vlocalplace.ImportFolder,
            VideoLocal_Place_ID = vlocalplace.VideoLocal_Place_ID
        };
        return v;
    }

    public static CL_AnimeGroup_User DeepCopy(this CL_AnimeGroup_User c)
    {
        var contract = new CL_AnimeGroup_User
        {
            AnimeGroupID = c.AnimeGroupID,
            AnimeGroupParentID = c.AnimeGroupParentID,
            DefaultAnimeSeriesID = c.DefaultAnimeSeriesID,
            GroupName = c.GroupName,
            Description = c.Description,
            IsFave = c.IsFave,
            IsManuallyNamed = c.IsManuallyNamed,
            UnwatchedEpisodeCount = c.UnwatchedEpisodeCount,
            DateTimeUpdated = c.DateTimeUpdated,
            WatchedEpisodeCount = c.WatchedEpisodeCount,
            SortName = c.SortName,
            WatchedDate = c.WatchedDate,
            EpisodeAddedDate = c.EpisodeAddedDate,
            LatestEpisodeAirDate = c.LatestEpisodeAirDate,
            PlayedCount = c.PlayedCount,
            WatchedCount = c.WatchedCount,
            StoppedCount = c.StoppedCount,
            OverrideDescription = c.OverrideDescription,
            MissingEpisodeCount = c.MissingEpisodeCount,
            MissingEpisodeCountGroups = c.MissingEpisodeCountGroups,
            Stat_AirDate_Min = c.Stat_AirDate_Min,
            Stat_AirDate_Max = c.Stat_AirDate_Max,
            Stat_EndDate = c.Stat_EndDate,
            Stat_SeriesCreatedDate = c.Stat_SeriesCreatedDate,
            Stat_UserVotePermanent = c.Stat_UserVotePermanent,
            Stat_UserVoteTemporary = c.Stat_UserVoteTemporary,
            Stat_UserVoteOverall = c.Stat_UserVoteOverall,
            Stat_IsComplete = c.Stat_IsComplete,
            Stat_HasFinishedAiring = c.Stat_HasFinishedAiring,
            Stat_IsCurrentlyAiring = c.Stat_IsCurrentlyAiring,
            Stat_HasTvDBLink = c.Stat_HasTvDBLink,
            Stat_HasTraktLink = c.Stat_HasTraktLink,
            Stat_HasMALLink = c.Stat_HasMALLink,
            Stat_HasMovieDBLink = c.Stat_HasMovieDBLink,
            Stat_HasMovieDBOrTvDBLink = c.Stat_HasMovieDBOrTvDBLink,
            Stat_SeriesCount = c.Stat_SeriesCount,
            Stat_EpisodeCount = c.Stat_EpisodeCount,
            Stat_AniDBRating = c.Stat_AniDBRating,
            ServerPosterPath = c.ServerPosterPath,
            SeriesForNameOverride = c.SeriesForNameOverride,
            Stat_AllCustomTags =
                new HashSet<string>(c.Stat_AllCustomTags, StringComparer.InvariantCultureIgnoreCase),
            Stat_AllTags = new HashSet<string>(c.Stat_AllTags, StringComparer.InvariantCultureIgnoreCase),
            Stat_AllYears = new HashSet<int>(c.Stat_AllYears),
            Stat_AllTitles = new HashSet<string>(c.Stat_AllTitles, StringComparer.InvariantCultureIgnoreCase),
            Stat_AnimeTypes = new HashSet<string>(c.Stat_AnimeTypes,
                StringComparer.InvariantCultureIgnoreCase),
            Stat_AllVideoQuality =
                new HashSet<string>(c.Stat_AllVideoQuality, StringComparer.InvariantCultureIgnoreCase),
            Stat_AllVideoQuality_Episodes = new HashSet<string>(c.Stat_AllVideoQuality_Episodes,
                StringComparer.InvariantCultureIgnoreCase),
            Stat_AudioLanguages =
                new HashSet<string>(c.Stat_AudioLanguages, StringComparer.InvariantCultureIgnoreCase),
            Stat_SubtitleLanguages = new HashSet<string>(c.Stat_SubtitleLanguages,
                StringComparer.InvariantCultureIgnoreCase)
        };
        return contract;
    }
}
