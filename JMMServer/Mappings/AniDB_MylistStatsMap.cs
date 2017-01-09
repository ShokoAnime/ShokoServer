using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class AniDB_MylistStatsMap : ClassMap<SVR_AniDB_MylistStats>
    {
        public AniDB_MylistStatsMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_MylistStatsID);

            Map(x => x.Animes).Not.Nullable();
            Map(x => x.Episodes).Not.Nullable();
            Map(x => x.Files).Not.Nullable();
            Map(x => x.SizeOfFiles).Not.Nullable();
            Map(x => x.AddedAnimes).Not.Nullable();
            Map(x => x.AddedEpisodes).Not.Nullable();
            Map(x => x.AddedFiles).Not.Nullable();
            Map(x => x.AddedGroups).Not.Nullable();
            Map(x => x.LeechPct).Not.Nullable();
            Map(x => x.GloryPct).Not.Nullable();
            Map(x => x.ViewedPct).Not.Nullable();
            Map(x => x.MylistPct).Not.Nullable();
            Map(x => x.ViewedMylistPct).Not.Nullable();
            Map(x => x.EpisodesViewed).Not.Nullable();
            Map(x => x.Votes).Not.Nullable();
            Map(x => x.Reviews).Not.Nullable();
            Map(x => x.ViewiedLength).Not.Nullable();
        }
    }
}