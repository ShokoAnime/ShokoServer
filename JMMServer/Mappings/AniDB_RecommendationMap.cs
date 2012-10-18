using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_RecommendationMap : ClassMap<AniDB_Recommendation>
	{
		public AniDB_RecommendationMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_RecommendationID);

			Map(x => x.AnimeID).Not.Nullable();
			Map(x => x.UserID).Not.Nullable();
			Map(x => x.RecommendationType).Not.Nullable();
			Map(x => x.RecommendationText);

			
        }
	}
}
