using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class CrossRef_AniDB_TvDBV2Map : ClassMap<CrossRef_AniDB_TvDBV2>
	{
		public CrossRef_AniDB_TvDBV2Map()
        {
			Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_TvDBV2ID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.CrossRefSource).Not.Nullable();
			Map(x => x.TvDBID).Not.Nullable();
			Map(x => x.TvDBSeasonNumber).Not.Nullable();
			Map(x => x.AniDBStartEpisodeType).Not.Nullable();
			Map(x => x.AniDBStartEpisodeNumber).Not.Nullable();
			Map(x => x.TvDBStartEpisodeNumber).Not.Nullable();
			Map(x => x.TvDBTitle);
        }
	}
}
