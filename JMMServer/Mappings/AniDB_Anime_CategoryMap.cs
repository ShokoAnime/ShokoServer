using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_Anime_CategoryMap : ClassMap<AniDB_Anime_Category>
	{
		public AniDB_Anime_CategoryMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_Anime_CategoryID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.CategoryID).Not.Nullable();
			Map(x => x.Weighting).Not.Nullable();
        }
	}
}
