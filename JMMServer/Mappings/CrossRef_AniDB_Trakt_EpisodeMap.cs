using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class CrossRef_AniDB_Trakt_EpisodeMap : ClassMap<CrossRef_AniDB_Trakt_Episode>
    {
        public CrossRef_AniDB_Trakt_EpisodeMap()
        {
			Not.LazyLoad();
			Id(x => x.CrossRef_AniDB_Trakt_EpisodeID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.AniDBEpisodeID).Not.Nullable();
			Map(x => x.TraktID);
            Map(x => x.Season).Not.Nullable();
            Map(x => x.EpisodeNumber).Not.Nullable();
        }
    }
}
