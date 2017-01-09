using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class AniDB_Character_SeiyuuMap : ClassMap<AniDB_Character_Seiyuu>
    {
        public AniDB_Character_SeiyuuMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_Character_SeiyuuID);

            Map(x => x.CharID).Not.Nullable();
            Map(x => x.SeiyuuID).Not.Nullable();
        }
    }
}