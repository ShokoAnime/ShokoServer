using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class GroupFilterMap : ClassMap<SVR_GroupFilter>
    {
        public GroupFilterMap()
        {
            Table("GroupFilter");

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
            Map(x => x.SeriesIdsVersion).Not.Nullable();
            Map(x => x.SeriesIdsString).Nullable().CustomType("StringClob");
            Map(x => x.GroupConditionsVersion).Not.Nullable();
            Map(x => x.GroupConditions).Nullable().CustomType("StringClob");
            Map(x => x.ParentGroupFilterID);
            Map(x => x.InvisibleInClients);
        }
    }
}