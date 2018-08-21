
using Shoko.Models.Server;

namespace Shoko.Server.Mappings
{
    public class AnimeStaffMap : ClassMap<AnimeStaff>
    {
        public AnimeStaffMap()
        {
            Table("AnimeStaff");
            Not.LazyLoad();
            Id(x => x.StaffID);
            Map(x => x.AniDBID).Not.Nullable();
            Map(x => x.Name).Not.Nullable();
            Map(x => x.AlternateName);
            Map(x => x.Description).CustomType("StringClob").CustomSqlType("nvarchar(max)");
            Map(x => x.ImagePath);
        }
    }
}