using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_Character_CreatorMap : ClassMap<AniDB_Character_Creator>
	{
		public AniDB_Character_CreatorMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_Character_CreatorID);

			Map(x => x.CharID).Not.Nullable();
			Map(x => x.CreatorID).Not.Nullable();
        }
	}
}
