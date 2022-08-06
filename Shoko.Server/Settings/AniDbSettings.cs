using System.ComponentModel.DataAnnotations;
using Shoko.Models.Enums;

namespace Shoko.Server.Settings
{
    public class AniDbSettings
    {
        [Required(AllowEmptyStrings = false)]
        public string Username { get; set; }
        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; }

        [Required(AllowEmptyStrings = false)]
        public string ServerAddress { get; set; } = "api.anidb.net";

        public ushort ServerPort { get; set; } = 9000;

        public ushort ClientPort { get; set; } = 4556;

        public string AVDumpKey { get; set; }

        public ushort AVDumpClientPort { get; set; } = 4557;

        public bool DownloadRelatedAnime { get; set; } = true;

        public bool DownloadSimilarAnime { get; set; } = true;

        public bool DownloadReviews { get; set; } = false;

        public bool DownloadReleaseGroups { get; set; } = false;

        public bool MyList_AddFiles { get; set; } = true;

        public AniDBFile_State MyList_StorageState { get; set; } = AniDBFile_State.HDD;

        public AniDBFileDeleteType MyList_DeleteType { get; set; } = AniDBFileDeleteType.MarkUnknown;

        public bool MyList_ReadUnwatched { get; set; } = true;

        public bool MyList_ReadWatched { get; set; } = true;

        public bool MyList_SetWatched { get; set; } = true;

        public bool MyList_SetUnwatched { get; set; } = true;
        public ScheduledUpdateFrequency MyList_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

        public ScheduledUpdateFrequency Calendar_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.HoursTwelve;

        public ScheduledUpdateFrequency Anime_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.HoursTwelve;

        public ScheduledUpdateFrequency MyListStats_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

        public ScheduledUpdateFrequency File_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

        public bool DownloadCharacters { get; set; } = true;

        public bool DownloadCreators { get; set; } = true;

        [Range(0, 5, ErrorMessage = "Max Relation Depth may only be between 0 and 5")]
        public int MaxRelationDepth { get; set; } = 3;

        public int MinimumHoursToRedownloadAnimeInfo { get; set; } = 24;

        public bool AutomaticallyImportSeries { get; set; } = false;
    }
}