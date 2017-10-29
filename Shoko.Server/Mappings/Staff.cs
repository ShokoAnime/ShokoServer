using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class StaffMap : ClassMap<Staff>
    {
        public StaffMap()
        {
            Table("Staff");
            Not.LazyLoad();
            Id(x => x.StaffID);
            Map(x => x.AniDBID).Not.Nullable();
            Map(x => x.Name).Not.Nullable();
            Map(x => x.AlternateName);
            Map(x => x.Description).CustomType("StringClob").CustomSqlType("nvarchar(max)");
            Map(x => x.ImagePath);
        }
    }
}