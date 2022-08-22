using System;
using System.Collections.Generic;
using Shoko.Models.Server;

namespace Shoko.Server.Providers.AniDB.Http.GetAnime
{
    public class ResponseEpisode
    {
        public int EpisodeID { get; set; }

        public int AnimeID { get; set; }

        public int LengthSeconds { get; set; }

        public decimal Rating { get; set; }

        public int Votes { get; set; }

        public int EpisodeNumber { get; set; }

        public EpisodeType EpisodeType { get; set; }

        public string Description { get; set; }

        public DateTime? AirDate { get; set; }

        public List<ResponseTitle> Titles = new();
    }
}
