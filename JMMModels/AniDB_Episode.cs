using System;
using System.Collections.Generic;
using JMMModels.Childs;

namespace JMMModels
{
    public class AniDB_Episode : DateUpdate
    {
        public string Id { get; set; }
        public int LengthSeconds { get; set; }
        public float Rating { get; set; }
        public int Votes { get; set; }
        public int Number { get; set; }
        public AniDB_Episode_Type Type { get; set; }
        public string RomajiName { get; set; }
        public string EnglishName { get; set; }
        public DateTime? AirDate { get; set; }


        public float Percentage { get; set; }
        public int EpisodeOrder { get; set; }
        public bool IsSplit { get; set; }


        public List<AniDB_Vote> MyVotes { get; set; }
        public List<AniDB_File> Files { get; set; }
        public List<VideoLocal> VideoLocals { get; set; }

    }
}
