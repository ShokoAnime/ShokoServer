using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class GroupFilterMap : ClassMap<GroupFilter>
	{
		public GroupFilterMap()
        {
			Not.LazyLoad();
            Id(x => x.GroupFilterID);

			Map(x => x.GroupFilterName);
			Map(x => x.ApplyToSeries).Not.Nullable();
			Map(x => x.BaseCondition).Not.Nullable();
			Map(x => x.SortingCriteria);
			Map(x => x.Locked);
            Map(x => x.FilterType);
            Map(x => x.GroupsIdsVersion).Not.Nullable();
            Map(x => x.GroupsIdsString).Nullable().CustomType("StringClob");

        }
    }
}
