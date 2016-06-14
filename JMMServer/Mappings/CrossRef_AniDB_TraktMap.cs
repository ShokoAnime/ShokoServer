using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class CrossRef_AniDB_TraktMap : ClassMap<CrossRef_AniDB_Trakt>
	{
		public CrossRef_AniDB_TraktMap()
        {
			Not.LazyLoad();
            Id(x => x.CrossRef_AniDB_TraktID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.CrossRefSource).Not.Nullable();
			Map(x => x.TraktID);
			Map(x => x.TraktSeasonNumber).Not.Nullable();
			
        }
	}
}
