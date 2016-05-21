using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AnimeEpisodeMap : ClassMap<AnimeEpisode>
	{
		public AnimeEpisodeMap()
        {
			Not.LazyLoad();
            Id(x => x.AnimeEpisodeID);

			Map(x => x.AniDB_EpisodeID).Not.Nullable();
			Map(x => x.AnimeSeriesID).Not.Nullable();
			Map(x => x.DateTimeCreated).Not.Nullable();
			Map(x => x.DateTimeUpdated).Not.Nullable();
            Map(x => x.PlexContractVersion).Not.Nullable();
            Map(x => x.PlexContractString).Nullable().CustomType("StringClob");
        }
	}
}
