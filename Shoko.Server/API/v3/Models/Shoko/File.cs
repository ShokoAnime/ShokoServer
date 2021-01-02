using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.MediaInfo;
using Shoko.Models.Server;
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
        /// The last watched date for the current user. Is null if unwatched
        /// </summary>
        /// <value></value>
        public DateTime? Watched { get; set; }

        /// <summary>
        /// Number of ticks into the video to resume from
        /// </summary>
        public long? ResumePosition { get; set; }
        
        /// <summary>
        /// Try to fit this file's resolution to something like 1080p, 480p, etc
        /// </summary>
        public string RoundedStandardResolution { get; set; }
        
        /// <summary>
        /// The file creation date of this file
        /// </summary>
        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Created { get; set; }

        public File() {}

        public File(SVR_VideoLocal vl) : this(null, vl) {}
        
        public File(HttpContext ctx, SVR_VideoLocal vl)
        {
            ID = vl.VideoLocalID;
            Size = vl.FileSize;
            Hashes = new Hashes
            {
                ED2K = vl.Hash,
                MD5 = vl.MD5,
                CRC32 = vl.CRC32,
                SHA1 = vl.SHA1
            };
            RoundedStandardResolution = FileQualityFilter.GetResolution(vl);
            Locations = vl.Places.Select(a => new Location
            {
                ImportFolderID = a.ImportFolderID,
                RelativePath = a.FilePath,
                Accessible = a.GetFile() != null
            }).ToList();
            Created = vl.DateTimeCreated;
            var ur = vl.GetUserRecord(ctx?.GetUser()?.JMMUserID ?? 0);
            ResumePosition = ur?.ResumePosition ?? 0;
            Watched = ur?.WatchedDate;
        }

        public static MediaContainer GetMedia(int id)
        {
            var vl = RepoFactory.VideoLocal.GetByID(id);
            return vl?.Media;
        }


        /// <summary>
        /// This isn't a list, because AniDB only has one File mapping, even if there are multiple episodes
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static AniDB GetAniDBInfo(int id)
        {
            var vl = RepoFactory.VideoLocal.GetByID(id);
            if (vl == null) return null;
            var anidb = RepoFactory.AniDB_File.GetByHash(vl.Hash);
            // this will be true for all Manual Links
            if (anidb == null) return null;

            return new AniDB
            {
                Chaptered = anidb.IsChaptered == 1,
                Duration = new TimeSpan(0, 0, anidb.File_LengthSeconds),
                Resolution = anidb.File_VideoResolution,
                VideoCodec = anidb.File_VideoCodec,
                OriginalFileName = anidb.FileName,
                Source = anidb.File_Source,
                FileSize = anidb.FileSize,
                ID = anidb.FileID,
                ReleaseDate = anidb.File_ReleaseDate == 0
                    ? null
                    : Commons.Utils.AniDB.GetAniDBDateAsDate(anidb.File_ReleaseDate),
                IsCensored = anidb.IsCensored == 1,
                IsDeprecated = anidb.IsDeprecated == 1,
                Version = anidb.FileVersion,
                Description = anidb.File_Description,
                Updated = anidb.DateTimeUpdated,
                ReleaseGroup = new AniDB.AniDBReleaseGroup
                {
                    ID = anidb.GroupID,
                    Name = anidb.Anime_GroupName,
                    ShortName = anidb.Anime_GroupNameShort
                },
                AudioCodecs = anidb.File_AudioCodec.Split(new[] {'\'', '`', '"'}, StringSplitOptions.RemoveEmptyEntries)
                    .ToList(),
                AudioLanguages = anidb.Languages.Select(a => a.LanguageName).ToList(),
                SubLanguages = anidb.Subtitles.Select(a => a.LanguageName).ToList()
            };
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
            /// <summary>
            /// The AniDB File ID
            /// </summary>
            public int ID { get; set; }

            /// <summary>
            /// Blu-ray, DVD, LD, TV, etc
            /// </summary>
            public string Source { get; set; }

            /// <summary>
            /// The Release Group. This is usually set, but sometimes is set as "raw/unknown"
            /// </summary>
            public AniDBReleaseGroup ReleaseGroup { get; set; }

            /// <summary>
            /// The file's release date. This is probably not filled in
            /// </summary>
            public DateTime? ReleaseDate { get; set; }

            /// <summary>
            /// Is the file marked as deprecated. Generally, yes if there's a V2, and this isn't it
            /// </summary>
            public bool IsDeprecated { get; set; }
            
            /// <summary>
            /// The file's version, Usually 1, sometimes more when there are edits released later
            /// </summary>
            public int Version { get; set; }

            /// <summary>
            /// Mostly applicable to hentai, but on occasion a TV release is censored enough to earn this.
            /// </summary>
            public bool IsCensored { get; set; }

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
            /// The reported resolution in 1920x1080 format. Not modelled further because there's no point
            /// </summary>
            public string Resolution { get; set; }
            
            /// <summary>
            /// Any comments that were added to the file, such as something wrong with it.
            /// </summary>
            public string Description { get; set; }

            /// <summary>
            /// The reported audio codecs. This may be very wrong on large files with lots of audio tracks, as AniDB's API has a hard limit on data
            /// </summary>
            public List<string> AudioCodecs { get; set; }
            
            /// <summary>
            /// The audio languages
            /// </summary>
            public List<string> AudioLanguages { get; set; }
            
            /// <summary>
            /// Sub languages
            /// </summary>
            public List<string> SubLanguages { get; set; }

            /// <summary>
            /// The reported Video Codec. Technically, there is a possibility of this needing a list, but it should only have one video track. 
            /// </summary>
            public string VideoCodec { get; set; }
            
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
    }
}
