using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class CloudAccountMap : ClassMap<CloudAccount>
    {
        public CloudAccountMap()
        {
            Not.LazyLoad();
            Id(x => x.CloudID);

            Map(x => x.ConnectionString).Not.Nullable();
            Map(x => x.Provider).Not.Nullable();
        }
    }
}
