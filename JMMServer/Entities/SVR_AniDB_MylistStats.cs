using JMMServer.AniDB_API.Raws;
using Shoko.Models.Server;

namespace JMMServer.Entities
{
    public class SVR_AniDB_MylistStats : AniDB_MylistStats
    {
     
        public SVR_AniDB_MylistStats()
        {
        }

        public void Populate(Raw_AniDB_MyListStats raw)
        {
            this.Animes = raw.Animes;
            this.Episodes = raw.Episodes;
            this.Files = raw.Files;
            this.SizeOfFiles = raw.SizeOfFiles;
            this.AddedAnimes = raw.AddedAnimes;
            this.AddedEpisodes = raw.AddedEpisodes;
            this.AddedFiles = raw.AddedFiles;
            this.AddedGroups = raw.AddedGroups;
            this.LeechPct = raw.LeechPct;
            this.GloryPct = raw.GloryPct;
            this.ViewedPct = raw.ViewedPct;
            this.MylistPct = raw.MylistPct;
            this.ViewedMylistPct = raw.ViewedMylistPct;
            this.EpisodesViewed = raw.EpisodesViewed;
            this.Votes = raw.Votes;
            this.Reviews = raw.Reviews;
            this.ViewiedLength = raw.ViewiedLength;
        }

        public SVR_AniDB_MylistStats(Raw_AniDB_MyListStats raw)
        {
            Populate(raw);
        }

        public override string ToString()
        {
            return string.Format("AniDB_MylistStats:: Animes: {0} | Episodes: {1} | Files: {2}", Animes, Episodes, Files);
        }
    }
}