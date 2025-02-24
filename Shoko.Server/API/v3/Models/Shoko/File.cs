using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Release;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

public partial class File
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
    public List<FileCrossReference>? SeriesIDs { get; set; }

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
    public HashesDict Hashes { get; set; }

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
    public string? Resolution { get; set; }

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

    /// <summary>
    /// The <see cref="File.Release"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public ReleaseInfo? Release { get; set; }

    /// <summary>
    /// The <see cref="MediaInfo"/>, if to-be included in the response data.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public MediaInfo? MediaInfo { get; set; }

    public File(HttpContext context, SVR_VideoLocal file, bool withXRefs = false, bool includeReleaseInfo = false, bool includeMediaInfo = false, bool includeAbsolutePaths = false) :
        this(RepoFactory.VideoLocalUser.GetByUserIDAndVideoLocalID(context?.GetUser()?.JMMUserID ?? 0, file.VideoLocalID), file, withXRefs, includeReleaseInfo, includeMediaInfo, includeAbsolutePaths)
    { }

    public File(SVR_VideoLocal_User? userRecord, SVR_VideoLocal file, bool withXRefs = false, bool includeReleaseInfo = false, bool includeMediaInfo = false, bool includeAbsolutePaths = false)
    {
        var mediaInfo = file.MediaInfo as IMediaInfo;
        ID = file.VideoLocalID;
        Size = file.FileSize;
        IsVariation = file.IsVariation;
        Hashes = new(file);
        Resolution = mediaInfo?.VideoStream?.Resolution;
        Locations = file.Places.Select(location => new Location(location, includeAbsolutePaths)).ToList();
        AVDump = new AVDumpInfo(file);
        Duration = file.DurationTimeSpan;
        ResumePosition = userRecord?.ResumePositionTimeSpan;
        Viewed = userRecord?.LastUpdated.ToUniversalTime();
        Watched = userRecord?.WatchedDate?.ToUniversalTime();
        Imported = file.DateTimeImported?.ToUniversalTime();
        Created = file.DateTimeCreated.ToUniversalTime();
        Updated = file.DateTimeUpdated.ToUniversalTime();
        if (withXRefs)
            SeriesIDs = FileCrossReference.From(file.EpisodeCrossReferences);

        if (includeReleaseInfo && file.ReleaseInfo is { } releaseInfo)
            Release = new(releaseInfo);

        if (includeMediaInfo && mediaInfo is not null)
            MediaInfo = new MediaInfo(file, mediaInfo);
    }

    /// <summary>
    /// Represents a file location.
    /// </summary>
    public partial class Location
    {
        /// <summary>
        /// The file location id.
        /// </summary>
        [Required]
        public int ID { get; set; }

        /// <summary>
        /// The id of the <see cref="File"/> this location belong to.
        /// </summary>
        [Required]
        public int FileID { get; set; }

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
        public string? AbsolutePath { get; set; }

        /// <summary>
        /// Can the server access the file right now
        /// </summary>
        [JsonRequired]
        public bool IsAccessible { get; set; }

        public Location(SVR_VideoLocal_Place location, bool includeAbsolutePaths)
        {
            ID = location.VideoLocal_Place_ID;
            FileID = location.VideoLocalID;
            ImportFolderID = location.ImportFolderID;
            RelativePath = location.FilePath;
            AbsolutePath = includeAbsolutePaths ? location.FullServerPath : null;
            IsAccessible = location.GetFile() != null;
        }

        /// <summary>
        /// Represents the parameters for the automatic relocation process.
        /// </summary>
        public class AutoRelocateBody
        {
            /// <summary>
            /// Optional. Id of the script to use instead of the default
            /// script.
            /// </summary>
            public int? ScriptID { get; set; }

            /// <summary>
            /// Indicates whether the result should be a preview of the
            /// relocation.
            /// </summary>
            public bool Preview { get; set; } = false;

            /// <summary>
            /// Move the file. Leave as `null` to use the default
            /// setting for move on import.
            /// </summary>
            public bool? Move { get; set; } = null;

            /// <summary>
            /// Rename the file. Leave as `null` to use the default
            /// setting for rename on import.
            /// </summary>
            public bool? Rename { get; set; } = null;

            /// <summary>
            /// Indicates whether empty directories should be deleted after
            /// relocating the file.
            /// </summary>
            public bool DeleteEmptyDirectories { get; set; } = true;
        }

    }

    /// <summary>
    /// Stores all of the hashes for the file.
    /// </summary>
    public class HashesDict(IHashes hashes) : IHashes
    {
        /// <summary>
        /// ED2K is AniDB's base hash.
        /// </summary>
        public string ED2K { get; set; } = hashes.ED2K;

        /// <summary>
        /// SHA1 is not used internally, but it is effortless to calculate with
        /// the others.
        /// </summary>
        public string? SHA1 { get; set; } = hashes.SHA1;

        /// <summary>
        /// CRC. It's got plenty of uses, but the big one is checking for file
        /// corruption.
        /// </summary>
        public string? CRC32 { get; set; } = hashes.CRC;

        /// <summary>
        /// MD5 might be useful for clients, but it's not used internally.
        /// </summary>
        public string? MD5 { get; set; } = hashes.MD5;

        #region IHashes implementation

        string? IHashes.CRC => CRC32;

        string? IHashes.this[HashAlgorithmName algorithm] => null;

        #endregion
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

        public FileUserStats MergeWithExisting(SVR_VideoLocal_User existing, SVR_VideoLocal? file = null)
        {
            // Get the file associated with the user entry.
            file ??= existing.VideoLocal!;

            var userDataService = Utils.ServiceContainer.GetRequiredService<IUserDataService>();
            userDataService.SaveVideoUserData(existing.User, file, new()
            {
                LastPlayedAt = LastWatchedAt,
                LastUpdatedAt = LastUpdatedAt,
                ResumePosition = ResumePosition,
                PlaybackCount = WatchedCount,
            }).GetAwaiter().GetResult();

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
            public int[] EpisodeIDs { get; set; } = [];
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
            public int[] FileIDs { get; set; } = [];

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
            public string RangeStart { get; set; } = string.Empty;

            /// <summary>
            /// The end of the range of episodes to link to the file. The prefix used should be the same as in <see cref="RangeStart"/>.
            /// </summary>
            /// <value></value>
            [Required]
            public string RangeEnd { get; set; } = string.Empty;
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
            public int[] FileIDs { get; set; } = [];

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
            public string RangeStart { get; set; } = string.Empty;

            /// <summary>
            /// If true then files will be linked to a single episode instead of a range spanning the amount of files to add.
            /// </summary>
            /// <value></value>
            [DefaultValue(false)]
            public bool SingleEpisode { get; set; }
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
            public int[] fileIDs { get; set; } = [];
        }

        public class BatchDeleteFilesBody
        {
            /// <summary>
            /// An array of file identifiers to unlink in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] fileIDs { get; set; } = [];

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

        public class BatchDeleteFileLocationsBody
        {
            /// <summary>
            /// An array of file location identifiers to remove in batch.
            /// </summary>
            /// <value></value>
            [Required]
            public int[] locationIDs { get; set; } = [];

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

    [JsonConverter(typeof(StringEnumConverter))]
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

    private static Func<(SVR_VideoLocal Video, SVR_VideoLocal_Place? Location, IReadOnlyList<SVR_VideoLocal_Place>? Locations, SVR_VideoLocal_User? UserRecord), object?>? GetOrderFunction(FileSortCriteria criteria, bool isInverted) =>
        criteria switch
        {
            FileSortCriteria.ImportFolderName => (tuple) => tuple.Location?.ImportFolder?.ImportFolderName ?? string.Empty,
            FileSortCriteria.ImportFolderID => (tuple) => tuple.Location?.ImportFolderID,
            FileSortCriteria.AbsolutePath => (tuple) => tuple.Location?.FullServerPath,
            FileSortCriteria.RelativePath => (tuple) => tuple.Location?.FilePath,
            FileSortCriteria.FileSize => (tuple) => tuple.Video.FileSize,
            FileSortCriteria.FileName => (tuple) => tuple.Location?.FileName,
            FileSortCriteria.FileID => (tuple) => tuple.Video.VideoLocalID,
            FileSortCriteria.DuplicateCount => (tuple) => tuple.Locations?.Count ?? 0,
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

    public static IEnumerable<(SVR_VideoLocal, SVR_VideoLocal_Place?, IReadOnlyList<SVR_VideoLocal_Place>?, SVR_VideoLocal_User?)> OrderBy(IEnumerable<(SVR_VideoLocal, SVR_VideoLocal_Place?, IReadOnlyList<SVR_VideoLocal_Place>?, SVR_VideoLocal_User?)> enumerable, List<string> sortCriterias)
    {
        var first = true;
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

            // All other criteria in the list.
            var ordered = (IOrderedEnumerable<(SVR_VideoLocal, SVR_VideoLocal_Place?, IReadOnlyList<SVR_VideoLocal_Place>?, SVR_VideoLocal_User?)>)current;
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


    /// <summary>
    /// AVDump info for the file.
    /// </summary>
    public class AVDumpInfo
    {
        /// <summary>
        /// Indicates if an AVDump session is queued or running.
        /// </summary>
        public string? Status { get; set; }

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
        public string? LastVersion { get; set; }

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
