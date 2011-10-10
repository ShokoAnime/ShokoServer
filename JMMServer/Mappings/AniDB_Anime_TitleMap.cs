using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_Anime_TitleMap : ClassMap<AniDB_Anime_Title>
	{
		public AniDB_Anime_TitleMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_Anime_TitleID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.Language).Not.Nullable();
			Map(x => x.Title).Not.Nullable();
			Map(x => x.TitleType).Not.Nullable();
        }
	}
}
