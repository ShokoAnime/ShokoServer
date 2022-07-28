using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
using Shoko.Server.API.Converters;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class File
    {
        /// <summary>
        /// The ID of the File. You'll need this to play it.
        /// </summary>
        public int ID { get; set; }
        
        /// <summary>
        /// The Filesize in bytes
        /// </summary>
        public long Size { get; set; }
        
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
        /// Try to fit this file's resolution to something like 1080p, 480p, etc
        /// </summary>
        public string RoundedStandardResolution { get; set; }

        /// <summary>
        /// The duration of the file.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Where to resume the next playback.
        /// </summary>
        public TimeSpan? ResumePosition { get; set; }

        /// <summary>
        /// The last watched date for the current user. Is null if unwatched
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime? Watched { get; set; }
        
        /// <summary>
        /// The file creation date of this file
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Created { get; set; }

        public File() {}

        public File(SVR_VideoLocal vl) : this(null, vl) {}
        
        public File(HttpContext context, SVR_VideoLocal file)
        {
            var userID = context?.GetUser()?.JMMUserID ?? 0;
            var userRecord = file.GetUserRecord(userID);
            ID = file.VideoLocalID;
            Size = file.FileSize;
            Hashes = new Hashes
            {
                ED2K = file.Hash,
                MD5 = file.MD5,
                CRC32 = file.CRC32,
                SHA1 = file.SHA1,
            };
            RoundedStandardResolution = FileQualityFilter.GetResolution(file);
            Locations = file.Places.Select(a => new Location
            {
                ImportFolderID = a.ImportFolderID,
                RelativePath = a.FilePath,
                Accessible = a.GetFile() != null,
            }).ToList();
            Duration = file.DurationTimeSpan;
            ResumePosition = userRecord?.ResumePositionTimeSpan;
            Watched = userRecord?.WatchedDate;
            Created = file.DateTimeCreated;
        }

        public static MediaContainer GetMedia(int id)
        {
            var vl = RepoFactory.VideoLocal.GetByID(id);
            return vl?.Media;
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
            /// Can the server access the file right now
            /// </summary>
            [JsonRequired]
            public bool Accessible { get; set; }
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
                ReleaseGroup = new AniDB.AniDBReleaseGroup
                {
                    ID = anidb.GroupID,
                    Name = anidb.Anime_GroupName,
                    ShortName = anidb.Anime_GroupNameShort,
                };
                ReleaseDate = anidb.File_ReleaseDate == 0 ? null : Commons.Utils.AniDB.GetAniDBDateAsDate(anidb.File_ReleaseDate);
                Version = anidb.FileVersion;
                IsDeprecated = anidb.IsDeprecated;
                IsCensored = anidb.IsCensored ?? false;
                Chaptered = anidb.IsChaptered;
                OriginalFileName = anidb.FileName;
                FileSize = anidb.FileSize;
                Description = anidb.File_Description;
                Updated = anidb.DateTimeUpdated;
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
            public AniDBReleaseGroup ReleaseGroup { get; set; }

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
            /// The reported duration of the file
            /// </summary>
            public TimeSpan Duration { get; set; }
            
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

            public class AniDBReleaseGroup
            {
                /// <summary>
                /// The Release Group's Name (Unlimited Translation Works)
                /// </summary>
                public string Name { get; set; }
                
                /// <summary>
                /// The Release Group's Name (UTW)
                /// </summary>
                public string ShortName { get; set; }
                
                /// <summary>
                /// AniDB ID
                /// </summary>
                public int ID { get; set; }
            }
        }
        /// <summary>
        /// A more detailed model to prevent too many requests for getting xrefs for many files
        /// </summary>
        public class FileDetailed : File
        {
            public class FileIDs
            {
                /// <summary>
                /// Any AniDB ID linked to this object
                /// </summary>
                public int AniDB { get; set; }
                /// <summary>
                /// Any TvDB IDs linked to this object
                /// </summary>
                public List<int> TvDB { get; set; }
                /// <summary>
                /// The Shoko ID
                /// </summary>
                public int ID { get; set; }
            }
            public class SeriesXRefs
            {
                /// <summary>
                /// The Series IDs
                /// </summary>
                public FileIDs SeriesID { get; set; }
                /// <summary>
                /// The Episode IDs
                /// </summary>
                public List<FileIDs> EpisodeIDs { get; set; }
            }

            /// <summary>
            /// The Cross Reference Models for every episode this file belongs to, created in a reverse tree and
            /// transformed back into a tree. Series -> Episode such that only episodes that this file is linked to are
            /// shown. In many cases, this will have arrays of 1 item
            /// </summary>
            public List<SeriesXRefs> SeriesIDs { get; set; }

            public FileDetailed() {}

            public FileDetailed(SVR_VideoLocal vl) : this(null, vl) {}

            public FileDetailed(HttpContext ctx, SVR_VideoLocal vl) : base(ctx, vl)
            {
                var episodes = vl.GetAnimeEpisodes();
                if (episodes.Count == 0) return;
                SeriesIDs = new List<SeriesXRefs>();
                var epIDs = episodes.Select(a => (a.AnimeSeriesID, FileID: new FileIDs
                {
                    ID = a.AnimeEpisodeID,
                    AniDB = a.AniDB_EpisodeID,
                    TvDB = a.TvDBEpisodes.Select(b => b.Id).ToList()
                })).GroupBy(a => a.AnimeSeriesID, b => b.FileID).Select(a =>
                {
                    var series = RepoFactory.AnimeSeries.GetByID(a.Key);
                    if (series == null)
                    {
                        return new SeriesXRefs
                        {
                            EpisodeIDs = a.ToList()
                        };
                    }

                    return new SeriesXRefs
                    {
                        SeriesID = new FileIDs
                        {
                            ID = series.AnimeSeriesID,
                            AniDB = series.AniDB_ID,
                            TvDB = series.GetTvDBSeries().Select(b => b.SeriesID).ToList()
                        },
                        EpisodeIDs = a.ToList()
                    };
                });
                SeriesIDs.AddRange(epIDs);
            }
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
                LastUpdatedAt = DateTime.Now;
            }

            public FileUserStats(SVR_VideoLocal_User userStats)
            {
                ResumePosition = userStats.ResumePositionTimeSpan;
                WatchedCount = userStats.WatchedCount;
                LastWatchedAt = userStats.WatchedDate;
                LastUpdatedAt = userStats.LastUpdated;
            }

            public FileUserStats MergeWithExisting(SVR_VideoLocal_User existing, SVR_VideoLocal file = null)
            {
                // Get the file assosiated with the user entry.
                if (file == null)
                    file = existing.GetVideoLocal();

                // Update the last updated field. It's needed for calculating the correct series user stats after setting the watch state.
                existing.LastUpdated = LastUpdatedAt;
                RepoFactory.VideoLocalUser.Save(existing);

                // Sync the watch date and aggregate the data up to the episode if needed.
                file.ToggleWatchedStatus(LastWatchedAt.HasValue, true, LastWatchedAt, true, existing.JMMUserID, true, true);

                // Update the rest of the data. The watch count have been bumped when toggling the watch state, so set it to it's intended value.
                existing.WatchedCount = WatchedCount;
                existing.ResumePositionTimeSpan = ResumePosition;
                RepoFactory.VideoLocalUser.Save(existing);

                // Return a new representation
                return new(existing);
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
                public int[] episodeIDs { get; set; }
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
                public int[] fileIDs { get; set; }

                /// <summary>
                /// The episode identifier.
                /// </summary>
                /// <value></value>
                [Required]
                public int episodeID { get; set; }
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
                public int seriesID { get; set; }

                /// <summary>
                /// The start of the range of episodes to link to the file. Append a type prefix to use another episode type.
                /// </summary>
                /// <value></value>
                [Required]
                public string rangeStart { get; set; }

                /// <summary>
                /// The end of the range of episodes to link to the file. The prefix used should be the same as in <see cref="rangeStart"/>.
                /// </summary>
                /// <value></value>
                [Required]
                public  string rangeEnd { get; set; }
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
                public int[] fileIDs { get; set; }

                /// <summary>
                /// The series identifier.
                /// </summary>
                /// <value></value>
                [Required]
                public int seriesID { get; set; }

                /// <summary>
                /// The start of the range of episodes to link to the file. Append a type prefix to use another episode type.
                /// </summary>
                /// <value></value>
                [Required]
                public string rangeStart { get; set; }

                /// <summary>
                /// If true then files will be linked to a single episode instead of a range spanning the amount of files to add.
                /// </summary>
                /// <value></value>
                [DefaultValue(false)]
                public bool singleEpisode { get; set; }
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
                public int[] episodeIDs { get; set; }
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
        }

        public static FileSource ParseFileSource(string source)
        {
            if (string.IsNullOrEmpty(source))
                return FileSource.Unknown;
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
                "vhs" => FileSource.VHS,
                "vcd" => FileSource.VCD,
                "svcd" => FileSource.VCD,
                "ld" => FileSource.LaserDisc,
                "camcorder" => FileSource.Camera,
                _ => FileSource.Unknown,
            };
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
        Camera = 9,
    }
}
