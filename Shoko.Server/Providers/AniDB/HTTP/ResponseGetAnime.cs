using System;
using System.Collections.Generic;

namespace Shoko.Server.Providers.AniDB.Http
{
    public class ResponseGetAnime
    {
        public int AnimeID { get; set; }
        public int EpisodeCount { get; set; }
        public DateTime? AirDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string URL { get; set; }
        public string Picname { get; set; }
        public int BeginYear { get; set; }
        public int EndYear { get; set; }
        public string MainTitle { get; set; }
        public string Description { get; set; }
        public int EpisodeCountNormal { get; set; }
        public int EpisodeCountSpecial { get; set; }
        public int Rating { get; set; }
        public int VoteCount { get; set; }
        public int TempRating { get; set; }
        public int TempVoteCount { get; set; }
        public int AvgReviewRating { get; set; }
        public int ReviewCount { get; set; }
        public DateTime DateTimeUpdated { get; set; }
        public DateTime DateTimeDescUpdated { get; set; }

        public int VersionNumber { get; set; }
        public string AwardList { get; set; }
        public int Restricted { get; set; }
        public int AnimePlanetID { get; set; }
        public int ANNID { get; set; }
        public int AllCinemaID { get; set; }
        public string AnimeNfoID { get; set; }
        public string DateRecordUpdated { get; set; }

        public int? LatestEpisodeNumber { get; set; }

        public string AnimeTypeRAW { get; set; }
        public string GenreRAW { get; set; }
        public string RelatedAnimeIdsRAW { get; set; }
        public string RelatedAnimeTypesRAW { get; set; }
        public string CharacterIDListRAW { get; set; }
        public string ReviewIDListRAW { get; set; }

        public List<int> ReviewIDList
        {
            get
            {
                List<int> reviewIDList = new List<int>();

                try
                {
                    if (ReviewIDListRAW.Length > 0)
                    {
                        string[] reviews = ReviewIDListRAW.Split(',');
                        foreach (string rid in reviews)
                        {
                            reviewIDList.Add(int.Parse(rid));
                        }
                    }
                }
                catch
                {
                    //BaseConfig.MyAnimeLog.Write("Error trying to get reviews from anime: {0}", ex);
                }

                return reviewIDList;
            }
        }
    }
}
