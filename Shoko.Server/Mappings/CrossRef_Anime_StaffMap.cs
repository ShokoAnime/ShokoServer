using FluentNHibernate.Mapping;
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class CrossRef_Anime_StaffMap : ClassMap<CrossRef_Anime_Staff>
    {
        public CrossRef_Anime_StaffMap()
        {
            Table("CrossRef_Anime_Staff");
            Not.LazyLoad();
            Id(x => x.CrossRef_Anime_StaffID);
            Map(x => x.AniDB_AnimeID).Not.Nullable();
            Map(x => x.Language).Not.Nullable();
            Map(x => x.StaffID).Not.Nullable();
            Map(x => x.RoleType).Not.Nullable();
            Map(x => x.Role);
            Map(x => x.RoleID);
        }
    }
}