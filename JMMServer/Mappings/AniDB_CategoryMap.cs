using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class AniDB_CategoryMap : ClassMap<AniDB_Category>
	{
		public AniDB_CategoryMap()
        {
			Not.LazyLoad();
            Id(x => x.AniDB_CategoryID);

			Map(x => x.CategoryDescription).Not.Nullable();
			Map(x => x.CategoryID).Not.Nullable();
			Map(x => x.CategoryName).Not.Nullable();
			Map(x => x.IsHentai).Not.Nullable();
			Map(x => x.ParentID).Not.Nullable();
        }
	}
}
