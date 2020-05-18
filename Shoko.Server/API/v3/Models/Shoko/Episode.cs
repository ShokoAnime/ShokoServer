using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.v3
{
    public class Episode : BaseModel
    {
        /// <summary>
        /// The relevant IDs for the Episode: Shoko, AniDB, TvDB
        /// </summary>
        public EpisodeIDs IDs { get; set; }
        
        /// <summary>
        /// Length of the Episode
        /// </summary>
        public TimeSpan Length { get; set; }
        
        /// <summary>
        /// The Last Watched Date for the current user. If null, it is unwatched
        /// </summary>
        [Required]
        public DateTime? Watched { get; set; }
        
        public Episode() {}

        public Episode(HttpContext ctx, SVR_AnimeEpisode ep)
        {
            var tvdb = ep.TvDBEpisodes;
            IDs = new EpisodeIDs
            {
                ID = ep.AnimeEpisodeID,
                AniDB = ep.AniDB_EpisodeID,
                TvDB = tvdb.Select(a => a.Id).ToList()
            };

            var anidb = ep.AniDB_Episode;
            if (anidb != null)
            {
                Length = new TimeSpan(0, 0, anidb.LengthSeconds);
            }

            var uid = ctx.GetUser()?.JMMUserID ?? 0;
            Watched = ep.GetUserRecord(uid)?.WatchedDate;

            Size = ep.GetVideoLocals().Count;
        }

        public static AniDB GetAniDBInfo(AniDB_Episode ep)
        {
            decimal rating = 0;
            int votes = 0;
            try
            {
                rating = decimal.Parse(ep.Rating);
                votes = int.Parse(ep.Votes);
            }
            catch {}

            var titles = RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.EpisodeID);
            return new AniDB
            {
                ID = ep.EpisodeID,
                Type = (EpisodeType) ep.EpisodeType,
                EpisodeNumber = ep.EpisodeNumber,
                AirDate = ep.GetAirDateAsDate(),
                Description = ep.Description,
                Rating = new Rating
                {
                    Source = "AniDB",
                    Value = rating,
                    MaxValue = 10,
                    Votes = votes
                },
                Titles = titles.Select(a => new Title
                {
                    Source = "AniDB",
                    Name = a.Title,
                    Language = a.Language
                }).ToList()
            };
        }

        /// <summary>
        /// AniDB specific data for an Episode
        /// </summary>
        public class AniDB
        {
            /// <summary>
            /// AniDB Episode ID
            /// </summary>
            public int ID { get; set; }
            
            /// <summary>
            /// Episode Type
            /// </summary>
            public EpisodeType Type { get; set; }
            
            /// <summary>
            /// Episode Number
            /// </summary>
            public int EpisodeNumber { get; set; }
            
            /// <summary>
            /// First Listed Air Date. This may not be when it aired, but an early release date
            /// </summary>
            public DateTime? AirDate { get; set; }
            
            /// <summary>
            /// Titles for the Episode
            /// </summary>
            public List<Title> Titles { get; set; }
            
            /// <summary>
            /// AniDB Episode Summary
            /// </summary>
            public string Description { get; set; }
            
            /// <summary>
            /// Episode Rating
            /// </summary>
            public Rating Rating { get; set; }
        }


        public class EpisodeIDs : IDs
        {
            #region XRefs

            // These are useful for many things, but for clients, it is mostly auxiliary

            /// <summary>
            /// The AniDB ID
            /// </summary>
            [Required]
            public int AniDB { get; set; }

            /// <summary>
            /// The TvDB IDs
            /// </summary>
            public List<int> TvDB { get; set; } = new List<int>();

            // TODO Support for TvDB string IDs (like in the new URLs) one day maybe
            #endregion
        }
    }
}