using System.Dynamic;

namespace Shoko.Models.Client
{
    public class CL_ServerSettings
    {
        public string AniDB_Username { get; set; }
        public string AniDB_Password { get; set; }
        public string AniDB_ServerAddress { get; set; }
        public string AniDB_ServerPort { get; set; }
        public string AniDB_ClientPort { get; set; }
        public string AniDB_AVDumpClientPort { get; set; }
        public string AniDB_AVDumpKey { get; set; }

        public bool AniDB_DownloadRelatedAnime { get; set; }
        public bool AniDB_DownloadSimilarAnime { get; set; }
        public bool AniDB_DownloadReviews { get; set; }
        public bool AniDB_DownloadReleaseGroups { get; set; }

        public bool AniDB_MyList_AddFiles { get; set; }
        public int AniDB_MyList_StorageState { get; set; }
        public int AniDB_MyList_DeleteType { get; set; }
        // these two are used when you add a file on an import, or when you do a mylist sync
        public bool AniDB_MyList_ReadWatched { get; set; } // update local to watched if anidb is watched
        public bool AniDB_MyList_ReadUnwatched { get; set; } // update local to un-watched if anidb is un-watched
        // these two are used when you watch a video, or manually mark a file as watched/unwatched
        public bool AniDB_MyList_SetWatched { get; set; } // set anidb watched if local state is watched
        public bool AniDB_MyList_SetUnwatched { get; set; } // set anidb un-watched if local state is un-watched

        public int AniDB_MyList_UpdateFrequency { get; set; }
        public int AniDB_Calendar_UpdateFrequency { get; set; }
        public int AniDB_Anime_UpdateFrequency { get; set; }
        public int AniDB_MyListStats_UpdateFrequency { get; set; }
        public int AniDB_File_UpdateFrequency { get; set; }

        public bool AniDB_DownloadCharacters { get; set; }
        public bool AniDB_DownloadCreators { get; set; }

        public int AniDB_MaxRelationDepth { get; set; }

        // Web Cache
        public string WebCache_Address { get; set; }
        public bool WebCache_Anonymous { get; set; }
        public bool WebCache_XRefFileEpisode_Get { get; set; }
        public bool WebCache_XRefFileEpisode_Send { get; set; }
        public bool WebCache_TvDB_Get { get; set; }
        public bool WebCache_TvDB_Send { get; set; }
        public bool WebCache_Trakt_Get { get; set; }
        public bool WebCache_Trakt_Send { get; set; }
        public bool WebCache_UserInfo { get; set; }

        // TvDB
        public bool TvDB_AutoLink { get; set; }
        public bool TvDB_AutoFanart { get; set; }
        public int TvDB_AutoFanartAmount { get; set; }
        public bool TvDB_AutoWideBanners { get; set; }
        public int TvDB_AutoWideBannersAmount { get; set; }
        public bool TvDB_AutoPosters { get; set; }
        public int TvDB_AutoPostersAmount { get; set; }
        public int TvDB_UpdateFrequency { get; set; }
        public string TvDB_Language { get; set; }

        // MovieDB
        // Auto Linking will follow TvDB preference
        public bool MovieDB_AutoFanart { get; set; }
        public int MovieDB_AutoFanartAmount { get; set; }
        public bool MovieDB_AutoPosters { get; set; }
        public int MovieDB_AutoPostersAmount { get; set; }

        // Import settings
        public string VideoExtensions { get; set; }
        public bool AutoGroupSeries { get; set; }
        public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }
        public string AutoGroupSeriesRelationExclusions { get; set; }
        public bool FileQualityFilterEnabled { get; set; }
        public string FileQualityFilterPreferences { get; set; }
        public bool RunImportOnStart { get; set; }
        public bool ScanDropFoldersOnStart { get; set; }
        public bool Hash_CRC32 { get; set; }
        public bool Hash_MD5 { get; set; }
        public bool Hash_SHA1 { get; set; }
        public bool Import_UseExistingFileWatchedStatus { get; set; }

        // Language
        public string LanguagePreference { get; set; }
        public bool LanguageUseSynonyms { get; set; }
        public int EpisodeTitleSource { get; set; }
        public int SeriesDescriptionSource { get; set; }
        public int SeriesNameSource { get; set; }

        // Trakt
        // Auto Linking will follow TvDB preference
        public bool Trakt_IsEnabled { get; set; }
        public string Trakt_AuthToken { get; set; }
        public string Trakt_RefreshToken { get; set; }
        public string Trakt_TokenExpirationDate { get; set; }
        public int Trakt_UpdateFrequency { get; set; }
        public int Trakt_SyncFrequency { get; set; }

        // LogRotator
        public bool RotateLogs { get; set; }
        public bool RotateLogs_Zip { get; set; }
        public bool RotateLogs_Delete { get; set; }
        public string RotateLogs_Delete_Days { get; set; }

        // WebUI
        public string WebUI_Settings { get; set; }

        public string Plex_ServerHost { get; set; }
        public string Plex_Sections { get; set; }
    }
}
