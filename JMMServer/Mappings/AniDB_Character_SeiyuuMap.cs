using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_Character_SeiyuuMap : ClassMap<AniDB_Character_Seiyuu>
	{
		public AniDB_Character_SeiyuuMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_Character_SeiyuuID);

			Map(x => x.CharID).Not.Nullable();
			Map(x => x.SeiyuuID).Not.Nullable();
        }
	}
}
