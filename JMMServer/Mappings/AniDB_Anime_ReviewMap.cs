using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_Anime_ReviewMap : ClassMap<AniDB_Anime_Review>
	{
		public AniDB_Anime_ReviewMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_Anime_ReviewID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.ReviewID).Not.Nullable();
        }
	}
}
