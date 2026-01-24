using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Extensions;

public static class ModelClients
{
    public static CL_ServerSettings ToContract(this IServerSettings settings)
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
            Hash_CRC32 = settings.Import.Hasher.CRC,
            Hash_MD5 = settings.Import.Hasher.MD5,
            Hash_SHA1 = settings.Import.Hasher.SHA1,
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

    public static CL_AniDB_Anime ToClient(this SVR_AniDB_Anime anime)
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

    public static CL_IgnoreAnime ToClient(this IgnoreAnime i)
        => new()
        {
            IgnoreAnimeID = i.IgnoreAnimeID,
            JMMUserID = i.JMMUserID,
            AnimeID = i.AnimeID,
            IgnoreType = i.IgnoreType,
            Anime = RepoFactory.AniDB_Anime.GetByAnimeID(i.AnimeID)?.ToClient(),
        };

    public static CrossRef_AniDB_Other? ToClient(this CrossRef_AniDB_TMDB_Movie? xref)
        => xref is null ? null : new()
        {
            CrossRef_AniDB_OtherID = xref.CrossRef_AniDB_TMDB_MovieID,
            AnimeID = xref.AnidbAnimeID,
            CrossRefType = 1 /* CrossRefType.MovieDB */,
            CrossRefID = xref.TmdbMovieID.ToString(),
            CrossRefSource = (int)CrossRefSource.User,
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
        if (image.ImageSource is not DataSourceType.TMDB)
            return null;

        var contract = new CL_AniDB_Anime_DefaultImage()
        {
            AniDB_Anime_DefaultImageID = image.AniDB_Anime_PreferredImageID,
            AnimeID = image.AnidbAnimeID,
            ImageParentID = image.ImageID,
            ImageParentType = (int)image.ImageType.ToClient(image.ImageSource),
            ImageType = image.ImageType switch
            {
                ImageEntityType.Backdrop => (int)CL_ImageSizeType.Fanart,
                ImageEntityType.Poster => (int)CL_ImageSizeType.Poster,
                ImageEntityType.Banner => (int)CL_ImageSizeType.WideBanner,
                _ => (int)CL_ImageSizeType.Fanart,
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
            CL_ImageEntityType.AniDB_Creator => ImageEntityType.Person,
            CL_ImageEntityType.Character => ImageEntityType.Character,
            CL_ImageEntityType.MovieDB_FanArt => ImageEntityType.Backdrop,
            CL_ImageEntityType.MovieDB_Poster => ImageEntityType.Poster,
            CL_ImageEntityType.Staff => ImageEntityType.Person,
            CL_ImageEntityType.Trakt_Episode => ImageEntityType.Thumbnail,
            CL_ImageEntityType.Trakt_Fanart => ImageEntityType.Backdrop,
            CL_ImageEntityType.Trakt_Friend => ImageEntityType.Person,
            CL_ImageEntityType.Trakt_Poster => ImageEntityType.Poster,
            CL_ImageEntityType.UserAvatar => ImageEntityType.Thumbnail,
            _ => ImageEntityType.None,
        };

    public static DataSourceType ToServerSource(this CL_ImageEntityType type)
        => type switch
        {
            CL_ImageEntityType.AniDB_Character => DataSourceType.AniDB,
            CL_ImageEntityType.AniDB_Cover => DataSourceType.AniDB,
            CL_ImageEntityType.AniDB_Creator => DataSourceType.AniDB,
            CL_ImageEntityType.Character => DataSourceType.Shoko,
            CL_ImageEntityType.MovieDB_FanArt => DataSourceType.TMDB,
            CL_ImageEntityType.MovieDB_Poster => DataSourceType.TMDB,
            CL_ImageEntityType.Staff => DataSourceType.Shoko,
            CL_ImageEntityType.Trakt_Episode => DataSourceType.Trakt,
            CL_ImageEntityType.Trakt_Fanart => DataSourceType.Trakt,
            CL_ImageEntityType.Trakt_Friend => DataSourceType.Trakt,
            CL_ImageEntityType.Trakt_Poster => DataSourceType.Trakt,
            CL_ImageEntityType.UserAvatar => DataSourceType.User,
            _ => DataSourceType.None,
        };

    public static DataSourceType ToDataSourceType(this DataSourceEnum value)
        => value switch
        {
            DataSourceEnum.AniDB => DataSourceType.AniDB,
            DataSourceEnum.AniList => DataSourceType.AniList,
            DataSourceEnum.Animeshon => DataSourceType.Animeshon,
            DataSourceEnum.Shoko => DataSourceType.Shoko,
            DataSourceEnum.TMDB => DataSourceType.TMDB,
            DataSourceEnum.Trakt => DataSourceType.Trakt,
            DataSourceEnum.User => DataSourceType.User,
            _ => DataSourceType.None,
        };

    public static DataSourceEnum ToDataSourceEnum(this DataSourceType value)
        => value switch
        {
            DataSourceType.AniDB => DataSourceEnum.AniDB,
            DataSourceType.AniList => DataSourceEnum.AniList,
            DataSourceType.Animeshon => DataSourceEnum.Animeshon,
            DataSourceType.Shoko => DataSourceEnum.Shoko,
            DataSourceType.TMDB => DataSourceEnum.TMDB,
            DataSourceType.Trakt => DataSourceEnum.Trakt,
            DataSourceType.User => DataSourceEnum.User,
            _ => DataSourceEnum.AniDB,
        };

    public static CL_ImageEntityType ToClient(this ImageEntityType type, DataSourceEnum source)
        => ToClient(source.ToDataSourceType(), type);

    public static CL_ImageEntityType ToClient(this ImageEntityType type, DataSourceType source)
        => ToClient(source, type);

    public static CL_ImageEntityType ToClient(this DataSourceType source, ImageEntityType imageType)
        => source switch
        {
            DataSourceType.AniDB => imageType switch
            {
                ImageEntityType.Character => CL_ImageEntityType.AniDB_Character,
                ImageEntityType.Poster => CL_ImageEntityType.AniDB_Cover,
                ImageEntityType.Person => CL_ImageEntityType.AniDB_Creator,
                _ => CL_ImageEntityType.None,
            },
            DataSourceType.Shoko => imageType switch
            {
                ImageEntityType.Character => CL_ImageEntityType.Character,
                ImageEntityType.Person => CL_ImageEntityType.Staff,
                _ => CL_ImageEntityType.None,
            },
            DataSourceType.TMDB => imageType switch
            {
                ImageEntityType.Backdrop => CL_ImageEntityType.MovieDB_FanArt,
                ImageEntityType.Poster => CL_ImageEntityType.MovieDB_Poster,
                _ => CL_ImageEntityType.None,
            },
            DataSourceType.Trakt => imageType switch
            {
                ImageEntityType.Backdrop => CL_ImageEntityType.MovieDB_FanArt,
                ImageEntityType.Poster => CL_ImageEntityType.MovieDB_Poster,
                _ => CL_ImageEntityType.None,
            },
            DataSourceType.User => imageType switch
            {
                ImageEntityType.Thumbnail => CL_ImageEntityType.UserAvatar,
                _ => CL_ImageEntityType.None,
            },
            _ => CL_ImageEntityType.None,
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

    public static CL_AniDB_Episode ToClient(this SVR_AniDB_Episode ep)
        => new()
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
            Titles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.EpisodeID)
                .ToDictionary(a => a.LanguageCode, a => a.Title),
        };

    public static CL_VideoLocal_Place ToClient(this SVR_VideoLocal_Place vlp)
        => new()
        {
            FilePath = vlp.FilePath,
            ImportFolderID = vlp.ImportFolderID,
            ImportFolderType = vlp.ImportFolderType,
            VideoLocalID = vlp.VideoLocalID,
            ImportFolder = vlp.ImportFolder,
            VideoLocal_Place_ID = vlp.VideoLocal_Place_ID
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

    public static CL_AniDB_ReleaseGroup? ToClient(this AniDB_ReleaseGroup? group)
        => group is null ? null : new CL_AniDB_ReleaseGroup
        {
            AniDB_ReleaseGroupID = group.AniDB_ReleaseGroupID,
            AnimeCount = group.AnimeCount,
            FileCount = group.FileCount,
            GroupID = group.GroupID,
            GroupName = group.GroupName,
            GroupNameShort = group.GroupNameShort,
            IRCChannel = group.IRCChannel,
            IRCServer = group.IRCServer,
            Picname = group.Picname,
            Rating = group.Rating,
            URL = group.URL,
            Votes = group.Votes,
        };
}
