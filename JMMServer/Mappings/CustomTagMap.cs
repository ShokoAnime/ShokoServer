using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
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