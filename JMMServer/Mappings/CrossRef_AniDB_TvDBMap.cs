using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class CrossRef_AniDB_TvDBMap : ClassMap<CrossRef_AniDB_TvDB>
	{
		public CrossRef_AniDB_TvDBMap()
        {
			Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_TvDBID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.CrossRefSource).Not.Nullable();
			Map(x => x.TvDBID).Not.Nullable();
			Map(x => x.TvDBSeasonNumber).Not.Nullable();
        }
	}
}
