using JMMServer.AniDB_API.Raws;

namespace JMMServer.Entities
{
    public class AniDB_MylistStats
    {
        public AniDB_MylistStats()
        {
        }

        public AniDB_MylistStats(Raw_AniDB_MyListStats raw)
        {
            Populate(raw);
        }

        public int AniDB_MylistStatsID { get; private set; }
        public int Animes { get; set; }
        public int Episodes { get; set; }
        public int Files { get; set; }
        public long SizeOfFiles { get; set; }
        public int AddedAnimes { get; set; }
        public int AddedEpisodes { get; set; }
        public int AddedFiles { get; set; }
        public int AddedGroups { get; set; }
        public int LeechPct { get; set; }
        public int GloryPct { get; set; }
        public int ViewedPct { get; set; }
        public int MylistPct { get; set; }
        public int ViewedMylistPct { get; set; }
        public int EpisodesViewed { get; set; }
        public int Votes { get; set; }
        public int Reviews { get; set; }
        public int ViewiedLength { get; set; }

        public void Populate(Raw_AniDB_MyListStats raw)
        {
            Animes = raw.Animes;
            Episodes = raw.Episodes;
            Files = raw.Files;
            SizeOfFiles = raw.SizeOfFiles;
            AddedAnimes = raw.AddedAnimes;
            AddedEpisodes = raw.AddedEpisodes;
            AddedFiles = raw.AddedFiles;
            AddedGroups = raw.AddedGroups;
            LeechPct = raw.LeechPct;
            GloryPct = raw.GloryPct;
            ViewedPct = raw.ViewedPct;
            MylistPct = raw.MylistPct;
            ViewedMylistPct = raw.ViewedMylistPct;
            EpisodesViewed = raw.EpisodesViewed;
            Votes = raw.Votes;
            Reviews = raw.Reviews;
            ViewiedLength = raw.ViewiedLength;
        }

        public override string ToString()
        {
            return string.Format("AniDB_MylistStats:: Animes: {0} | Episodes: {1} | Files: {2}", Animes, Episodes, Files);
        }
    }
}