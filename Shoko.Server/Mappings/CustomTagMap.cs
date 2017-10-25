using FluentNHibernate.Mapping;
using Shoko.Server.Models;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class CustomTagMap : ClassMap<CustomTag>
    {
        public CustomTagMap()
        {
            Not.LazyLoad();
            Id(x => x.CustomTagID);

            Map(x => x.TagName);
            Map(x => x.TagDescription);
        }
    }
}