using JMMContracts.PlexAndKodi;
using JMMServer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JMMServer.API.Model.v2
{
    public class Episode
    {
        public int id { get; set; }
        public string type { get; set; }
        public ArtCollection art { get; set; }
        public string title { get; set; }
        public string summary { get; set; }
        public string year { get; set; }
        public string air { get; set; }
        public string rating { get; set; }
        public int view { get; set; }
        public int eptype { get; set; }
        public int epnumber { get; set; }
        public List<Role> roles { get; set; }
        public List<Tag> tags { get; set; }
        // Medias

        public Episode()
        {

        }

        public Episode(AnimeEpisode aep, int uid)
        {
            //aep.GetAnimeSeries().
            //Video nv = ser.GetPlexContract(uid);

            art = new ArtCollection();
            roles = new List<Role>();
            tags = new List<Tag>();
            id = aep.AnimeEpisodeID;
            type = aep.EpisodeTypeEnum.ToString();
            //title = 
            //summary = aep.GetUserContract(uid)
            //year = aep.GetUserContract(uid)
            air = aep.GetUserContract(uid).AniDB_AirDate.ToString();
            rating = aep.GetUserContract(uid).AniDB_Rating;
            view = aep.GetUserContract(uid).IsWatched;
            epnumber = aep.GetUserContract(uid).EpisodeNumber;
            eptype = aep.GetUserContract(uid).EpisodeType;

        }
    }
}
