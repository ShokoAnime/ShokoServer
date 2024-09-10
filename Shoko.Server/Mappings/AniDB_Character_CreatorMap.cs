using FluentNHibernate.Mapping;
using Shoko.Models.Server;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_Character_CreatorMap : ClassMap<AniDB_Character_Creator>
{
    public AniDB_Character_CreatorMap()
    {
        Not.LazyLoad();
        Id(x => x.AniDB_Character_CreatorID);

        Map(x => x.CharacterID).Not.Nullable();
        Map(x => x.CreatorID).Not.Nullable();
    }
}
