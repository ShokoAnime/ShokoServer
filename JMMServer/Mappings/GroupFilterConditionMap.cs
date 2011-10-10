using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
	public class GroupFilterConditionMap : ClassMap<GroupFilterCondition>
	{
		public GroupFilterConditionMap()
        {
			Not.LazyLoad();
            Id(x => x.GroupFilterConditionID);

			Map(x => x.ConditionOperator).Not.Nullable();
			Map(x => x.ConditionParameter);
			Map(x => x.ConditionType).Not.Nullable();
			Map(x => x.GroupFilterID).Not.Nullable();
        }
	}
}
