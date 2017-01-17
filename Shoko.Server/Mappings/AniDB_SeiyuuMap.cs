using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models;

namespace Shoko.Server.Mappings
{
    public class AniDB_SeiyuuMap : ClassMap<AniDB_Seiyuu>
    {
        public AniDB_SeiyuuMap()
        {
            Table("AniDB_Seiyuu");
            Not.LazyLoad();
            Id(x => x.AniDB_SeiyuuID);

            Map(x => x.SeiyuuID).Not.Nullable();
            Map(x => x.SeiyuuName).Not.Nullable();
            Map(x => x.PicName);
        }
    }
}