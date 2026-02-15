using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Server.API.v1.Models;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Release;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.Extensions;

public static class ModelClients
{
    public static CL_ServerSettings ToClient(this IServerSettings settings)
        => new()
        {
            AniDB_Username = settings.AniDb.Username,
            AniDB_Password = settings.AniDb.Password,
            AniDB_ServerAddress = new Uri(settings.AniDb.HTTPServerUrl).Host,
            AniDB_ServerPort = new Uri(settings.AniDb.HTTPServerUrl).Port.ToString(),
            AniDB_ClientPort = settings.AniDb.ClientPort.ToString(),
            AniDB_AVDumpClientPort = settings.AniDb.AVDumpClientPort.ToString(),
            AniDB_AVDumpKey = settings.AniDb.AVDumpKey,
            AniDB_DownloadRelatedAnime = settings.AniDb.DownloadRelatedAnime,
            AniDB_DownloadSimilarAnime = false,
            AniDB_DownloadReviews = false,
            AniDB_DownloadReleaseGroups = false,
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
            AniDB_MyListStats_UpdateFrequency = (int)ScheduledUpdateFrequency.Never,
            AniDB_File_UpdateFrequency = (int)settings.AniDb.File_UpdateFrequency,
            AniDB_DownloadCharacters = settings.AniDb.DownloadCharacters,
            AniDB_DownloadCreators = settings.AniDb.DownloadCreators,
            AniDB_MaxRelationDepth = settings.AniDb.MaxRelationDepth,

            // TvDB
            TvDB_AutoLink = false,
            TvDB_AutoFanart = false,
            TvDB_AutoFanartAmount = 0,
            TvDB_AutoPosters = false,
            TvDB_AutoPostersAmount = 0,
            TvDB_AutoWideBanners = false,
            TvDB_AutoWideBannersAmount = 0,
            TvDB_UpdateFrequency = (int)ScheduledUpdateFrequency.Never,
            TvDB_Language = "en",

            // TMDB
            MovieDB_AutoFanart = settings.TMDB.AutoDownloadBackdrops,
            MovieDB_AutoFanartAmount = settings.TMDB.MaxAutoBackdrops,
            MovieDB_AutoPosters = settings.TMDB.AutoDownloadPosters,
            MovieDB_AutoPostersAmount = settings.TMDB.MaxAutoPosters,

            // Import settings
            VideoExtensions = string.Join(",", settings.Import.VideoExtensions),
            AutoGroupSeries = settings.AutoGroupSeries,
            AutoGroupSeriesUseScoreAlgorithm = settings.AutoGroupSeriesUseScoreAlgorithm,
            AutoGroupSeriesRelationExclusions = string.Join("|", settings.AutoGroupSeriesRelationExclusions).Replace("alternative", "alternate", StringComparison.InvariantCultureIgnoreCase),
            FileQualityFilterEnabled = settings.FileQualityFilterEnabled,
            FileQualityFilterPreferences = SettingsProvider.Serialize(settings.FileQualityPreferences),
            Import_MoveOnImport = settings.Plugins.Renamer.MoveOnImport,
            Import_RenameOnImport = settings.Plugins.Renamer.RenameOnImport,
            Import_UseExistingFileWatchedStatus = settings.Import.UseExistingFileWatchedStatus,
            RunImportOnStart = settings.Import.RunOnStart,
            ScanDropFoldersOnStart = settings.Import.ScanDropFoldersOnStart,
            SkipDiskSpaceChecks = settings.Import.SkipDiskSpaceChecks,

            // Language
            LanguagePreference = string.Join(",", settings.Language.SeriesTitleLanguageOrder),
            LanguageUseSynonyms = settings.Language.UseSynonyms,
            EpisodeTitleSource = (int)settings.Language.EpisodeTitleSourceOrder.FirstOrDefault(),
            SeriesDescriptionSource = (int)settings.Language.DescriptionSourceOrder.FirstOrDefault(),
            SeriesNameSource = (int)settings.Language.SeriesTitleSourceOrder.FirstOrDefault(),

            // trakt
            Trakt_IsEnabled = settings.TraktTv.Enabled,
            Trakt_AuthToken = settings.TraktTv.AuthToken,
            Trakt_RefreshToken = settings.TraktTv.RefreshToken,
            Trakt_TokenExpirationDate = settings.TraktTv.TokenExpirationDate,
            Trakt_UpdateFrequency = 0,
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

    public static CL_AniDB_Anime ToClient(this AniDB_Anime anime)
        => new()
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
            AnimeType = (int)anime.AnimeType,
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
#pragma warning disable CS0618
            DateTimeUpdated = anime.DateTimeUpdated,
#pragma warning restore CS0618
            DateTimeDescUpdated = anime.DateTimeDescUpdated,
            ImageEnabled = anime.ImageEnabled,
            Restricted = anime.Restricted,
            ANNID = anime.ANNID,
            AllCinemaID = anime.AllCinemaID,
            LatestEpisodeNumber = anime.LatestEpisodeNumber,
            DisableExternalLinksFlag = 0
        };

    public static CL_AniDB_GroupStatus ToClient(this AniDB_GroupStatus g)
        => new CL_AniDB_GroupStatus
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

    public static CL_CrossRef_AniDB_Other? ToClient(this CrossRef_AniDB_TMDB_Movie? xref)
        => xref is null ? null : new()
        {
            CrossRef_AniDB_OtherID = xref.CrossRef_AniDB_TMDB_MovieID,
            AnimeID = xref.AnidbAnimeID,
            CrossRefType = 1 /* CrossRefType.MovieDB */,
            CrossRefID = xref.TmdbMovieID.ToString(),
            CrossRefSource = 2 /* CrossRefSource.User */,
        };

    public static CL_MovieDB_Movie ToClient(this TMDB_Movie movie)
        => new()
        {
            MovieDB_MovieID = movie.TMDB_MovieID,
            MovieId = movie.Id,
            MovieName = movie.EnglishTitle,
            OriginalName = movie.OriginalTitle,
            Overview = movie.EnglishOverview,
            Rating = (int)Math.Round(movie.UserRating * 10),
        };

    public static CL_MovieDB_Fanart ToClientFanart(this TMDB_Image image)
        => new()
        {
            MovieDB_FanartID = image.TMDB_ImageID,
            Enabled = image.IsEnabled ? 1 : 0,
            ImageHeight = image.Height,
            ImageID = string.Empty,
            ImageSize = "original",
            ImageType = "backdrop",
            ImageWidth = image.Width,
            MovieId = 0,
            URL = image.RemoteFileName,
        };

    public static CL_MovieDB_Poster ToClientPoster(this TMDB_Image image)
        => new()
        {
            MovieDB_PosterID = image.TMDB_ImageID,
            Enabled = image.IsEnabled ? 1 : 0,
            ImageHeight = image.Height,
            ImageID = string.Empty,
            ImageSize = "original",
            ImageType = "poster",
            ImageWidth = image.Width,
            MovieId = 0,
            URL = image.RemoteFileName,
        };

    public static CL_AniDB_Anime_DefaultImage? ToClient(this AniDB_Anime_PreferredImage image)
    {
        if (image.ImageSource is not DataSource.TMDB)
            return null;

        var contract = new CL_AniDB_Anime_DefaultImage()
        {
            AniDB_Anime_DefaultImageID = image.AniDB_Anime_PreferredImageID,
            AnimeID = image.AnidbAnimeID,
            ImageParentID = image.ImageID,
            ImageParentType = (int)image.ImageType.ToClient(image.ImageSource),
            ImageType = image.ImageType switch
            {
                ImageEntityType.Poster => 1,
                ImageEntityType.Banner => 3,
                _ => 2,
            },
        };

        switch ((CL_ImageEntityType)contract.ImageParentType)
        {
            case CL_ImageEntityType.MovieDB_Poster:
                contract.MoviePoster = ((TMDB_Image?)image.GetImageMetadata())?.ToClientPoster();
                if (contract.MoviePoster is null)
                    return null;
                break;
            case CL_ImageEntityType.MovieDB_FanArt:
                contract.MovieFanart = ((TMDB_Image?)image.GetImageMetadata())?.ToClientFanart();
                if (contract.MovieFanart is null)
                    return null;
                break;
        }

        return contract;
    }

    public static ImageEntityType ToServerType(this CL_ImageEntityType type)
        => type switch
        {
            CL_ImageEntityType.AniDB_Character => ImageEntityType.Character,
            CL_ImageEntityType.AniDB_Cover => ImageEntityType.Poster,
            CL_ImageEntityType.AniDB_Creator => ImageEntityType.Creator,
            CL_ImageEntityType.Character => ImageEntityType.Character,
            CL_ImageEntityType.MovieDB_FanArt => ImageEntityType.Backdrop,
            CL_ImageEntityType.MovieDB_Poster => ImageEntityType.Poster,
            CL_ImageEntityType.Staff => ImageEntityType.Creator,
            CL_ImageEntityType.UserAvatar => ImageEntityType.Thumbnail,
            _ => ImageEntityType.None,
        };

    public static DataSource ToServerSource(this CL_ImageEntityType type)
        => type switch
        {
            CL_ImageEntityType.AniDB_Character => DataSource.AniDB,
            CL_ImageEntityType.AniDB_Cover => DataSource.AniDB,
            CL_ImageEntityType.AniDB_Creator => DataSource.AniDB,
            CL_ImageEntityType.Character => DataSource.Shoko,
            CL_ImageEntityType.MovieDB_FanArt => DataSource.TMDB,
            CL_ImageEntityType.MovieDB_Poster => DataSource.TMDB,
            CL_ImageEntityType.Staff => DataSource.Shoko,
            CL_ImageEntityType.UserAvatar => DataSource.User,
            _ => DataSource.None,
        };

    public static CL_ImageEntityType ToClient(this ImageEntityType imageType, DataSource source)
        => source switch
        {
            DataSource.AniDB => imageType switch
            {
                ImageEntityType.Character => CL_ImageEntityType.AniDB_Character,
                ImageEntityType.Poster => CL_ImageEntityType.AniDB_Cover,
                ImageEntityType.Creator => CL_ImageEntityType.AniDB_Creator,
                _ => CL_ImageEntityType.None,
            },
            DataSource.Shoko => imageType switch
            {
                ImageEntityType.Character => CL_ImageEntityType.Character,
                ImageEntityType.Creator => CL_ImageEntityType.Staff,
                _ => CL_ImageEntityType.None,
            },
            DataSource.TMDB => imageType switch
            {
                ImageEntityType.Backdrop => CL_ImageEntityType.MovieDB_FanArt,
                ImageEntityType.Poster => CL_ImageEntityType.MovieDB_Poster,
                _ => CL_ImageEntityType.None,
            },
            DataSource.User => imageType switch
            {
                ImageEntityType.Thumbnail => CL_ImageEntityType.UserAvatar,
                _ => CL_ImageEntityType.None,
            },
            _ => CL_ImageEntityType.None,
        };

    public static CL_CrossRef_AniDB_TraktV2 ToClient(this CrossRef_AniDB_TraktV2 xref)
        => new()
        {
            CrossRef_AniDB_TraktV2ID = xref.CrossRef_AniDB_TraktV2ID,
            AnimeID = xref.AnimeID,
            TraktID = xref.TraktID,
            TraktSeasonNumber = xref.TraktSeasonNumber,
            AniDBStartEpisodeType = xref.AniDBStartEpisodeType,
            AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber,
            CrossRefSource = xref.CrossRefSource,
            TraktStartEpisodeNumber = xref.TraktStartEpisodeNumber,
            TraktTitle = xref.TraktTitle,
        };

    public static CL_CrossRef_CustomTag ToClient(this CrossRef_CustomTag xref)
        => new()
        {
            CrossRef_CustomTagID = xref.CrossRef_CustomTagID,
            CrossRefID = xref.CrossRefID,
            CrossRefType = 1 /* Anime */,
            CustomTagID = xref.CustomTagID,
        };

    public static CL_AniDB_Character ToClient(this AniDB_Character character)
        => new()
        {
            AniDB_CharacterID = character.AniDB_CharacterID,
            CharID = character.CharacterID,
            PicName = character.ImagePath,
            CreatorListRaw = string.Empty,
            CharName = character.Name,
            CharKanjiName = character.OriginalName,
            CharDescription = character.Description,
            Seiyuu = character.GetCreator()?.ToClient(),
        };

    public static CL_AniDB_Seiyuu ToClient(this AniDB_Creator creator)
        => new()
        {
            AniDB_SeiyuuID = creator.AniDB_CreatorID,
            SeiyuuID = creator.CreatorID,
            SeiyuuName = creator.Name,
            PicName = creator.ImagePath,
        };

    public static CL_AniDB_Episode ToClient(this AniDB_Episode ep)
        => new()
        {
            AniDB_EpisodeID = ep.AniDB_EpisodeID,
            EpisodeID = ep.EpisodeID,
            AnimeID = ep.AnimeID,
            LengthSeconds = ep.LengthSeconds,
            Rating = ep.Rating,
            Votes = ep.Votes,
            EpisodeNumber = ep.EpisodeNumber,
            EpisodeType = (int)ep.EpisodeType,
            Description = ep.Description,
            AirDate = ep.AirDate,
            DateTimeUpdated = ep.DateTimeUpdated,
            Titles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.EpisodeID)
                .ToDictionary(a => a.LanguageCode, a => a.Title),
        };

    public static CL_VideoLocal_Place ToClient(this VideoLocal_Place vlp)
        => new()
        {
            VideoLocal_Place_ID = vlp.ID,
            ImportFolderID = vlp.ManagedFolderID,
            FilePath = vlp.RelativePath,
            ImportFolderType = 1 /* HDD */,
            VideoLocalID = vlp.VideoID,
            ImportFolder = vlp.ManagedFolder?.ToClient(),
        };

    public static CL_ImportFolder ToClient(this ShokoManagedFolder mf)
        => new()
        {
            ImportFolderID = mf.ID,
            ImportFolderLocation = mf.Path,
            ImportFolderName = mf.Name,
            ImportFolderType = 1 /* HDD */,
            IsDropDestination = mf.IsDropDestination ? 1 : 0,
            IsDropSource = mf.IsDropSource ? 1 : 0,
            IsWatched = mf.IsWatched ? 1 : 0,
        };

    public static ShokoManagedFolder ToServer(this CL_ImportFolder mf)
        => new()
        {
            ID = mf.ImportFolderID,
            Path = mf.ImportFolderLocation,
            Name = mf.ImportFolderName,
            IsDropDestination = mf.IsDropDestination == 1,
            IsDropSource = mf.IsDropSource == 1,
            IsWatched = mf.IsWatched == 1,
        };

    public static CL_AnimeGroup_User DeepCopy(this CL_AnimeGroup_User c)
        => new()
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

    public static CL_AniDB_ReleaseGroup? ToClient(this IReleaseGroup? group)
        => group is null ? null : new CL_AniDB_ReleaseGroup
        {
            AniDB_ReleaseGroupID = 0,
            AnimeCount = 0,
            FileCount = 0,
            GroupID = int.TryParse(group.ID, out var id) ? id : 0,
            GroupName = group.Name,
            GroupNameShort = group.ShortName,
            IRCChannel = null,
            IRCServer = null,
            Picname = null,
            Rating = 0,
            URL = null,
            Votes = 0,
        };

    public static CL_MovieDBMovieSearch_Response ToClient(this SearchMovie movie)
        => new()
        {
            MovieID = movie.Id,
            MovieName = movie.Title,
            OriginalName = movie.OriginalTitle,
            Overview = movie.Overview,
        };
}
