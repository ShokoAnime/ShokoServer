using FluentNHibernate.Mapping;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Mappings;

public class AniDB_CharacterMap : ClassMap<AniDB_Character>
{
    public AniDB_CharacterMap()
    {
        Table("AniDB_Character");
        Not.LazyLoad();
        Id(x => x.AniDB_CharacterID);

        Map(x => x.Description).Not.Nullable().CustomType("StringClob");
        Map(x => x.CharacterID).Not.Nullable();
        Map(x => x.ImagePath).Not.Nullable();
        Map(x => x.OriginalName).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.Gender).CustomType<PersonGender>().Not.Nullable();
    }
}
