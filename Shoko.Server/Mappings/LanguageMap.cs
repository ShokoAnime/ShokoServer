using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
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