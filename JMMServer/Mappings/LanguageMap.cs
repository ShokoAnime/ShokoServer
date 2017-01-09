using FluentNHibernate.Mapping;
using JMMServer.Entities;
using Shoko.Models.Server;

namespace JMMServer.Mappings
{
    public class LanguageMap : ClassMap<Language>
    {
        public LanguageMap()
        {
            Not.LazyLoad();
            Id(x => x.LanguageID);

            Map(x => x.LanguageName).Not.Nullable();
        }
    }
}