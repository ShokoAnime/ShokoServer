using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v3.Models.Shoko;

public class File
{
    /// <summary>
    /// The ID of the File. You'll need this to play it.
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// The Cross Reference Models for every episode this file belongs to, created in a reverse tree and
    /// transformed back into a tree. Series -> Episode such that only episodes that this file is linked to are
    /// shown. In many cases, this will have arrays of 1 item
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<SeriesCrossReference> SeriesIDs { get; set; }

    /// <summary>
    /// The Filesize in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// If this file is marked as a file variation.
    /// </summary>
    public bool IsVariation { get; set; }

    /// <summary>
    /// The calculated hashes of the file
    /// </summary>
    /// <returns></returns>
    public Hashes Hashes { get; set; }

    /// <summary>
    /// All of the Locations that this file exists in
    /// </summary>
    public List<Location> Locations { get; set; }

    /// <summary>
    /// AVDump info for the file.
    /// </summary>
    public AVDumpInfo AVDump { get; set; }

    /// <summary>
    /// Try to fit this file's resolution to something like 1080p, 480p, etc
    /// </summary>
    public string Resolution { get; set; }

    /// <summary>
    /// The duration of the file.
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Where to resume the next playback.
    /// </summary>
    public TimeSpan? ResumePosition { get; set; }

    /// <summary>
    /// The last time the current user viewed the file. Will be null if the user
    /// have not viewed the file yet.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? Viewed { get; set; }

    /// <summary>
    /// The last time the current user watched the file until completion, or
    /// otherwise marked the file was watched. Will be null if the user have not
    /// watched the file yet.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? Watched { get; set; }

    /// <summary>
    /// When the file was last imported. Usually is a file only imported once,
    /// but there may be exceptions.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime? Imported { get; set; }

    /// <summary>
    /// The file creation date of this file
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Created { get; set; }

    /// <summary>
    /// When the file was last updated (e.g. the hashes were added/updated).
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Updated { get; set; }

    public File() { }

    /// <summary>
    /// The <see cref="File.AniDB"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty("AniDB", NullValueHandling = NullValueHandling.Ignore)]
    public AniDB _AniDB { get; set; }

    /// <summary>
    /// The <see cref="MediaInfo"/>, if to-be included in the response data.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public MediaInfo MediaInfo { get; set; }

    public File(HttpContext context, SVR_VideoLocal file, bool withXRefs = false, HashSet<DataSource> includeDataFrom = null, bool includeMediaInfo = false, bool includeAbsolutePaths = false) :
        this(file.GetUserRecord(context?.GetUser()?.JMMUserID ?? 0), file, withXRefs, includeDataFrom, includeMediaInfo, includeAbsolutePaths) {}

    public File(SVR_VideoLocal_User userRecord, SVR_VideoLocal file, bool withXRefs = false, HashSet<DataSource> includeDataFrom = null, bool includeMediaInfo = false, bool includeAbsolutePaths = false)
    {
        ID = file.VideoLocalID;
        Size = file.FileSize;
        IsVariation = file.IsVariation;
        Hashes = new Hashes { ED2K = file.Hash, MD5 = file.MD5, CRC32 = file.CRC32, SHA1 = file.SHA1 };
        Resolution = FileQualityFilter.GetResolution(file);
        Locations = file.Places.Select(a => new Location
        {
            ImportFolderID = a.ImportFolderID,
            RelativePath = a.FilePath,
            AbsolutePath = includeAbsolutePaths ? a.FullServerPath : null,
            IsAccessible = a.GetFile() != null,
        }).ToList();
        AVDump = new AVDumpInfo(file);
        Duration = file.DurationTimeSpan;
        ResumePosition = userRecord?.ResumePositionTimeSpan;
        Viewed = userRecord?.LastUpdated.ToUniversalTime();
        Watched = userRecord?.WatchedDate?.ToUniversalTime();
        Imported = file.DateTimeImported?.ToUniversalTime();
        Created = file.DateTimeCreated.ToUniversalTime();
        Updated = file.DateTimeUpdated.ToUniversalTime();
        if (withXRefs)
        {
            SeriesIDs = file.GetAnimeEpisodes()
                .GroupBy(episode => episode.AnimeSeriesID, episode => new CrossReferenceIDs
                {
                    ID = episode.AnimeEpisodeID,
                    AniDB = episode.AniDB_EpisodeID,
                    TvDB = episode.TvDBEpisodes.Select(b => b.Id).ToList(),
                })
                .Select(tuples =>
                {
                    var series = RepoFactory.AnimeSeries.GetByID(tuples.Key);
                    if (series == null)
                    {
                        return new SeriesCrossReference
                        {
                            EpisodeIDs = tuples.ToList(),
                        };
                    }

                    return new SeriesCrossReference
                    {
                        SeriesID = new CrossReferenceIDs
                        {
                            ID = series.AnimeSeriesID,
                            AniDB = series.AniDB_ID,
                            TvDB = series.GetTvDBSeries().Select(b => b.SeriesID).ToList(),
                        },
                        EpisodeIDs = tuples.ToList(),
                    };
                })
                .ToList();
        }

        if (includeDataFrom?.Contains(DataSource.AniDB) ?? false)
        {
            var anidbFile = file.GetAniDBFile();
            if (anidbFile != null)
                this._AniDB = new File.AniDB(anidbFile);
        }

        if (includeMediaInfo)
        {
            var mediaContainer = file?.Media;
            if (mediaContainer == null)
                throw new Exception("Unable to find media container for File");
            MediaInfo = new MediaInfo(file, mediaContainer);
        }
    }

    public class Location
    {
        /// <summary>
        /// The Import Folder that this file resides in 
        /// </summary>
        public int ImportFolderID { get; set; }

        /// <summary>
        /// The relative path from the import folder's path on the server. The Filename can be easily extracted from this. Using the ImportFolder, you can get the full server path of the file or map it if the client has remote access to the filesystem. 
        /// </summary>
        public string RelativePath { get; set; }

        /// <summary>
        /// The absolute path for the file on the server.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string AbsolutePath { get; set; }

        /// <summary>
        /// Can the server access the file right now
        /// </summary>
        [JsonRequired]
        public bool IsAccessible { get; set; }
    }

    /// <summary>
    /// AniDB_File info
    /// </summary>
    public class AniDB
    {
        public AniDB(SVR_AniDB_File anidb)
        {
            ID = anidb.FileID;
            Source = ParseFileSource(anidb.File_Source);
            ReleaseGroup = new ReleaseGroup(anidb.ReleaseGroup);
            ReleaseDate = anidb.File_ReleaseDate == 0
                ? null
                : Commons.Utils.AniDB.GetAniDBDateAsDate(anidb.File_ReleaseDate);
            Version = anidb.FileVersion;
            IsDeprecated = anidb.IsDeprecated;
            IsCensored = anidb.IsCensored ?? false;
            Chaptered = anidb.IsChaptered;
            OriginalFileName = anidb.FileName;
            FileSize = anidb.FileSize;
            Description = anidb.File_Description;
            Updated = anidb.DateTimeUpdated.ToUniversalTime();
            AudioLanguages = anidb.Languages.Select(a => a.LanguageName).ToList();
            SubLanguages = anidb.Subtitles.Select(a => a.LanguageName).ToList();
        }

        /// <summary>
        /// The AniDB File ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Blu-ray, DVD, LD, TV, etc
        /// </summary>
        public FileSource Source { get; set; }

        /// <summary>
        /// The Release Group. This is usually set, but sometimes is set as "raw/unknown"
        /// </summary>
        public ReleaseGroup ReleaseGroup { get; set; }

        /// <summary>
        /// The file's release date. This is probably not filled in
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// The file's version, Usually 1, sometimes more when there are edits released later
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// Is the file marked as deprecated. Generally, yes if there's a V2, and this isn't it
        /// </summary>
        public bool IsDeprecated { get; set; }

        /// <summary>
        /// Mostly applicable to hentai, but on occasion a TV release is censored enough to earn this.
        /// </summary>
        public bool? IsCensored { get; set; }

        /// <summary>
        /// The original FileName. Useful for when you obtained from a shady source or when you renamed it without thinking. 
        /// </summary>
        public string OriginalFileName { get; set; }

        /// <summary>
        /// The reported FileSize. If you got this far and it doesn't match, something very odd has occurred
        /// </summary>
        public long FileSize { get; set; }

        /// <summary>
        /// Any comments that were added to the file, such as something wrong with it.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The audio languages
        /// </summary>
        public List<string> AudioLanguages { get; set; }

        /// <summary>
        /// Sub languages
        /// </summary>
        public List<string> SubLanguages { get; set; }

        /// <summary>
        /// Does the file have chapters. This may be wrong, since it was only added in AVDump2 (a more recent version at that)
        /// </summary>
        public bool Chaptered { get; set; }

        /// <summary>
        /// When we last got data on this file
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Updated { get; set; }

    }

    public class CrossReferenceIDs
    {
        /// <summary>
        /// The Shoko ID
        /// /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Any AniDB ID linked to this object
        /// </summary>
        public int AniDB { get; set; }

        /// <summary>
        /// Any TvDB IDs linked to this object
        /// </summary>
        public List<int> TvDB { get; set; }
    }

    public class SeriesCrossReference
    {
        /// <summary>
        /// The Series IDs
        /// </summary>
        public CrossReferenceIDs SeriesID { get; set; }

        /// <summary>
        /// The Episode IDs
        /// </summary>
        public List<CrossReferenceIDs> EpisodeIDs { get; set; }
    }

    /// <summary>
    /// User stats for the file.
    /// </summary>
    public class FileUserStats
    {
        public FileUserStats()
        {
            ResumePosition = TimeSpan.Zero;
            WatchedCount = 0;
            LastWatchedAt = null;
            LastUpdatedAt = DateTime.UtcNow;
        }

        public FileUserStats(SVR_VideoLocal_User userStats)
        {
            ResumePosition = userStats.ResumePositionTimeSpan;
            WatchedCount = userStats.WatchedCount;
            LastWatchedAt = userStats.WatchedDate?.ToUniversalTime();
            LastUpdatedAt = userStats.LastUpdated.ToUniversalTime();
        }

        public FileUserStats MergeWithExisting(SVR_VideoLocal_User existing, SVR_VideoLocal file = null)
        {
            // Get the file assosiated with the user entry.
            file ??= existing.GetVideoLocal();

            // Sync the watch date and aggregate the data up to the episode if needed.
            file.ToggleWatchedStatus(LastWatchedAt.HasValue, true, LastWatchedAt?.ToLocalTime(), true, existing.JMMUserID, true, true, LastUpdatedAt.ToLocalTime());

            // Update the rest of the data. The watch count have been bumped when toggling the watch state, so set it to it's intended value.
            existing.WatchedCount = WatchedCount;
            existing.ResumePositionTimeSpan = ResumePosition;
            RepoFactory.VideoLocalUser.Save(existing);

            // Return a new representation
            return new FileUserStats(existing);
        }

        /// <summary>
        /// Where to resume the next playback.
        /// </summary>
        public TimeSpan? ResumePosition { get; set; }

        /// <summary>
        /// Total number of times the file have been watched.
        /// </summary>
        public int WatchedCount { get; set; }

        /// <summary>
        /// When the file was last watched. Will be null if the full is
        /// currently marked as unwatched.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? LastWatchedAt { get; set; }

        /// <summary>
        /// When the entry was last updated.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime LastUpdatedAt { get; set; }
    }

    /// <summary>
    /// Input models.
    /// </summary>
    public class Input
    {
        /// <summary>
        /// Link a file to multiple episodes.
        /// </summary>
        public class LinkEpisodesBody
        {
            /// <summary>
            /// An array of episode identifiers to link to the file.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] EpisodeIDs { get; set; }
        }

        /// <summary>
        /// Link a file to multiple episodes.
        /// </summary>
        public class LinkMultipleFilesBody
        {
            /// <summary>
            /// An array of file identifiers to link in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] FileIDs { get; set; }

            /// <summary>
            /// The episode identifier.
            /// </summary>
            /// <value></value>
            [Required]
            public int EpisodeID { get; set; }
        }

        /// <summary>
        /// Link a file to an episode range in a series.
        /// </summary>
        public class LinkSeriesBody
        {
            /// <summary>
            /// The series identifier.
            /// </summary>
            /// <value></value>
            [Required]
            public int SeriesID { get; set; }

            /// <summary>
            /// The start of the range of episodes to link to the file. Append a type prefix to use another episode type.
            /// </summary>
            /// <value></value>
            [Required]
            public string RangeStart { get; set; }

            /// <summary>
            /// The end of the range of episodes to link to the file. The prefix used should be the same as in <see cref="RangeStart"/>.
            /// </summary>
            /// <value></value>
            [Required]
            public string RangeEnd { get; set; }
        }

        /// <summary>
        /// Link multiple files to an episode range in a series.
        /// </summary>
        public class LinkSeriesMultipleBody
        {
            /// <summary>
            /// An array of file identifiers to link in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] FileIDs { get; set; }

            /// <summary>
            /// The series identifier.
            /// </summary>
            /// <value></value>
            [Required]
            public int SeriesID { get; set; }

            /// <summary>
            /// The start of the range of episodes to link to the file. Append a type prefix to use another episode type.
            /// </summary>
            /// <value></value>
            [Required]
            public string RangeStart { get; set; }

            /// <summary>
            /// If true then files will be linked to a single episode instead of a range spanning the amount of files to add.
            /// </summary>
            /// <value></value>
            [DefaultValue(false)]
            public bool SingleEpisode { get; set; }
        }

        /// <summary>
        /// Unlink the spesified episodes from a file.
        /// </summary>
        public class UnlinkEpisodesBody
        {
            /// <summary>
            /// An array of episode identifiers to unlink from the file.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] EpisodeIDs { get; set; }
        }

        /// <summary>
        /// Unlink multiple files in batch.
        /// </summary>
        public class UnlinkMultipleBody
        {
            /// <summary>
            /// An array of file identifiers to unlink in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] fileIDs { get; set; }
        }

        public class BatchDeleteBody
        {
            /// <summary>
            /// An array of file identifiers to unlink in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] fileIDs { get; set; }

            /// <summary>
            /// Remove all physical file locations and not just the file record.
            /// </summary>
            [DefaultValue(true)]
            public bool removeFiles = true;

            /// <summary>
            /// This causes the empty folder removal to skipped if set to false.
            /// This significantly speeds up batch deleting if you are deleting
            /// many files in the same folder. It may be specified in the query.
            /// </summary>
            [DefaultValue(true)]
            public bool removeFolders = true;
        }
    }

    public enum FileSortCriteria
    {
        None = 0,
        ImportFolderName = 1,
        ImportFolderID = 2,
        AbsolutePath = 3,
        RelativePath = 4,
        FileSize = 5,
        DuplicateCount = 6,
        CreatedAt = 7,
        ImportedAt = 8,
        ViewedAt = 9,
        WatchedAt = 10,
        ED2K = 11,
        MD5 = 12,
        SHA1 = 13,
        CRC32 = 14,
        FileName = 15,
        FileID = 16,
    }

    private static Func<(SVR_VideoLocal Video, SVR_VideoLocal_Place Location, List<SVR_VideoLocal_Place> Locations, SVR_VideoLocal_User UserRecord), object> GetOrderFunction(FileSortCriteria criteria, bool isInverted) =>
        criteria switch
        {
            FileSortCriteria.ImportFolderName => (tuple) => tuple.Location?.ImportFolder?.ImportFolderName ?? string.Empty,
            FileSortCriteria.ImportFolderID => (tuple) => tuple.Location?.ImportFolderID,
            FileSortCriteria.AbsolutePath => (tuple) => tuple.Location?.FullServerPath,
            FileSortCriteria.RelativePath => (tuple) => tuple.Location?.FilePath,
            FileSortCriteria.FileSize => (tuple) => tuple.Video.FileSize,
            FileSortCriteria.FileName => (tuple) => tuple.Location?.FileName,
            FileSortCriteria.FileID => (tuple) => tuple.Video.VideoLocalID,
            FileSortCriteria.DuplicateCount => (tuple) => tuple.Locations.Count,
            FileSortCriteria.CreatedAt => (tuple) => tuple.Video.DateTimeCreated,
            FileSortCriteria.ImportedAt => isInverted ? (tuple) => tuple.Video.DateTimeImported ?? DateTime.MinValue : (tuple) => tuple.Video.DateTimeImported ?? DateTime.MaxValue,
            FileSortCriteria.ViewedAt => isInverted ? (tuple) => tuple.UserRecord?.LastUpdated ?? DateTime.MinValue : (tuple) => tuple.UserRecord?.LastUpdated ?? DateTime.MaxValue,
            FileSortCriteria.WatchedAt => isInverted ? (tuple) => tuple.UserRecord?.WatchedDate ?? DateTime.MinValue : (tuple) => tuple.UserRecord?.WatchedDate ?? DateTime.MaxValue,
            FileSortCriteria.ED2K => (tuple) => tuple.Video.Hash,
            FileSortCriteria.MD5 => (tuple) => tuple.Video.MD5,
            FileSortCriteria.SHA1 => (tuple) => tuple.Video.SHA1,
            FileSortCriteria.CRC32 => (tuple) => tuple.Video.CRC32,
            _ => null,
        };

    public static IEnumerable<(SVR_VideoLocal, SVR_VideoLocal_Place, List<SVR_VideoLocal_Place>, SVR_VideoLocal_User)> OrderBy(IEnumerable<(SVR_VideoLocal, SVR_VideoLocal_Place, List<SVR_VideoLocal_Place>, SVR_VideoLocal_User)> enumerable, List<string> sortCriterias)
    {
        bool first = true;
        return sortCriterias.Aggregate(enumerable, (current, rawSortCriteria) =>
        {
            // Any unrecognised criterias are ignored.
            var (sortCriteria, isInverted) = ParseSortCriteria(rawSortCriteria);
            var orderFunc = GetOrderFunction(sortCriteria, isInverted);
            if (orderFunc == null)
                return current;

            // First criteria in the list.
            if (first)
            {
                first = false;
                return isInverted ? enumerable.OrderByDescending(orderFunc) : enumerable.OrderBy(orderFunc);
            }

            // All other criterias in the list.
            var ordered = current as IOrderedEnumerable<(SVR_VideoLocal, SVR_VideoLocal_Place, List<SVR_VideoLocal_Place>, SVR_VideoLocal_User)>;
            return isInverted ? ordered.ThenByDescending(orderFunc) : ordered.ThenBy(orderFunc);
        });
    }

    private static (FileSortCriteria criteria, bool isInverted) ParseSortCriteria(string input)
    {
        var isInverted = false;
        if (input[0] == '-')
        {
            isInverted = true;
            input = input[1..];
        }

        if (!Enum.TryParse<FileSortCriteria>(input, ignoreCase: true, out var sortCriteria))
            sortCriteria = FileSortCriteria.None;

        return (sortCriteria, isInverted);
    }

    public static FileSource ParseFileSource(string source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return FileSource.Unknown;
        }

        return source.Replace("-", "").ToLower() switch
        {
            "tv" => FileSource.TV,
            "dtv" => FileSource.TV,
            "hdtv" => FileSource.TV,
            "dvd" => FileSource.DVD,
            "hkdvd" => FileSource.DVD,
            "hddvd" => FileSource.DVD,
            "bluray" => FileSource.BluRay,
            "www" => FileSource.Web,
            "web" => FileSource.Web,
            "vhs" => FileSource.VHS,
            "vcd" => FileSource.VCD,
            "svcd" => FileSource.VCD,
            "ld" => FileSource.LaserDisc,
            "laserdisc" => FileSource.LaserDisc,
            "camcorder" => FileSource.Camera,
            _ => FileSource.Unknown
        };
    }

    /// <summary>
    /// AVDump info for the file.
    /// </summary>
    public class AVDumpInfo
    {
        /// <summary>
        /// Indicates if an AVDump session is queued or running.
        /// </summary>
        public string Status;

        /// <summary>
        /// The current progress if an AVDump session is running.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public double? Progress { get; set; }

        /// <summary>
        /// The succeeded AniDB creq count, if an AVDump session is running.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? SucceededCreqCount { get; set; }

        /// <summary>
        /// The failed AniDB creq count, if an AVDump session is running.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? FailedCreqCount { get; set; }

        /// <summary>
        /// The pending AniDB creq count, if an AVDump session is running.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? PendingCreqCount { get; set; }

        /// <summary>
        /// Indicates when the AVDump session was started, if an AVDump session
        /// is running.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public DateTime? StartedAt { get; set; }

        /// <summary>
        /// The last time we did a successful AVDump of the file.
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? LastDumpedAt { get; set; }

        /// <summary>
        /// The version of the AVDump component from the last time we did a
        /// successful AVDump.
        /// </summary>
        public string LastVersion { get; set; }

        public AVDumpInfo(SVR_VideoLocal video)
        {
            var session = AVDumpHelper.GetSessionForVideo(video);
            Status = session == null ? null : session.IsRunning ? "Running" : "Queued";
            if (session != null && session.IsRunning)
            {
                Progress = session.Progress;
                SucceededCreqCount = session.SucceededCreqCount;
                FailedCreqCount = session.FailedCreqCount;
                PendingCreqCount = session.PendingCreqCount;
                StartedAt = session.StartedAt.ToUniversalTime();
            }
            LastDumpedAt = video.LastAVDumped?.ToUniversalTime();
            LastVersion = video.LastAVDumpVersion;
        }
    }
}

[JsonConverter(typeof(StringEnumConverter))]
public enum FileSource
{
    Unknown = 0,
    Other = 1,
    TV = 2,
    DVD = 3,
    BluRay = 4,
    Web = 5,
    VHS = 6,
    VCD = 7,
    LaserDisc = 8,
    Camera = 9
}
