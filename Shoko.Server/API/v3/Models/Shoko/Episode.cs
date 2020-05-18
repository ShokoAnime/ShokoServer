using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Shoko.Server.Models;

namespace Shoko.Server.API.v3
{
    public class Episode : BaseModel
    {
        /// <summary>
        /// The relevant IDs for the Episode: Shoko, AniDB, TvDB
        /// </summary>
        public EpisodeIDs IDs { get; set; }
        
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