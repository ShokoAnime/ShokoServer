using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Entities;

namespace Shoko.Server.Mappings
{
    public class AniDB_SeiyuuMap : ClassMap<SVR_AniDB_Seiyuu>
    {
        public AniDB_SeiyuuMap()
        {
            Not.LazyLoad();
            Id(x => x.AniDB_SeiyuuID);

            Map(x => x.SeiyuuID).Not.Nullable();
            Map(x => x.SeiyuuName).Not.Nullable();
            Map(x => x.PicName);
        }
    }
}