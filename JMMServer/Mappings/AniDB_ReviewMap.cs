using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_ReviewMap : ClassMap<AniDB_Review>
	{
		public AniDB_ReviewMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_ReviewID);

			Map(x => x.AuthorID).Not.Nullable();
			Map(x => x.RatingAnimation).Not.Nullable();
			Map(x => x.RatingCharacter).Not.Nullable();
			Map(x => x.RatingEnjoyment).Not.Nullable();
			Map(x => x.RatingSound).Not.Nullable();
			Map(x => x.RatingStory).Not.Nullable();
			Map(x => x.RatingValue).Not.Nullable();
			Map(x => x.ReviewID).Not.Nullable();
			Map(x => x.ReviewText).Not.Nullable();
        }
	}
}
