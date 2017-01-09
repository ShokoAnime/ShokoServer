using System;
using Shoko.Models.Client;
using Shoko.Models.Server;

namespace Shoko.Models
{
    public class Contract_Trakt_Comment
    {
        public int CommentType { get; set; } // episode, show
        public string Text { get; set; }
        public bool Spoiler { get; set; }
        public DateTime? Inserted { get; set; }

        public string Comment_Url { get; set; }

        // if episode
        public string Episode_Season { get; set; }
        public string Episode_Number { get; set; }
        public string Episode_Title { get; set; }
        public string Episode_Overview { get; set; }
        public string Episode_Url { get; set; }
        public string Episode_Screenshot { get; set; }

        // if episode or show
        public Contract_TraktTVShowResponse TraktShow { get; set; }
        public int? AnimeSeriesID { get; set; }
        public Client.CL_AniDB_Anime Anime { get; set; }
    }
}