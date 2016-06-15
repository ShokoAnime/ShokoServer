using FluentNHibernate.Mapping;
using JMMServer.Entities;

namespace JMMServer.Mappings
{
    public class AniDB_SeiyuuMap : ClassMap<AniDB_Seiyuu>
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