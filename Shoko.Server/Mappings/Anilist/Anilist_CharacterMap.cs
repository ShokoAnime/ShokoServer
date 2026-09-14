using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_CharacterMap : ClassMap<Anilist_Character>
{
    public Anilist_CharacterMap()
    {
        Table("Anilist_Character");

        Not.LazyLoad();
        Id(x => x.Anilist_CharacterID);

        Map(x => x.AnilistCharacterID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.OriginalName);
        Map(x => x.AlternativeNames).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.Description).Not.Nullable().CustomType("StringClob");
        Map(x => x.ImagePath);
        Map(x => x.Gender).CustomType<PersonGender>().Not.Nullable();
        Map(x => x.DateOfBirth).CustomType<PartialDateOnlyConverter>();
        Map(x => x.Age);
        Map(x => x.FavoriteCount).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
