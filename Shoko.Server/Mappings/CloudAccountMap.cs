using FluentNHibernate.Mapping;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class CloudAccountMap : ClassMap<SVR_CloudAccount>
    {
        public CloudAccountMap()
        {
            Table("CloudAccount");

            Not.LazyLoad();
            Id(x => x.CloudID);
            Map(x => x.Name).Not.Nullable();
            Map(x => x.ConnectionString).Not.Nullable();
            Map(x => x.Provider).Not.Nullable();
        }
    }
}