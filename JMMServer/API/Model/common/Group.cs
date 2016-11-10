using System.Collections.Generic;

namespace JMMServer.API.Model.common
{
    public class Group
    {
        public List<Serie> series { get; set; }
        public int id { get; set; }
        public string name { get; set; }
        public List<Entities.AniDB_Anime_Title> titles { get; set; }
        public HashSet<string> videoqualities { get; set; }

        public Group()
        {
            series = new List<Serie>();
        }


        public Group GenerateFromAnimeGroup(Entities.AnimeGroup ag, int uid, int nocast, int notag, int level)
        {
            Group g = new Group();

            g.name = ag.GroupName;
            g.id = ag.AnimeGroupID;
            g.titles = ag.Titles;
            videoqualities = ag.VideoQualities;

            if (level != 1)
            {
                foreach (Entities.AniDB_Anime ada in ag.Anime)
                {
                    g.series.Add(new Serie().GenerateFromAnimeSeries(Repositories.RepoFactory.AnimeSeries.GetByAnimeID(ada.AnimeID), uid,nocast, notag, (level-1)));
                }
            }

            return g;
        }
    }
}
