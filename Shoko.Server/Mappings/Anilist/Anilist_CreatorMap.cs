using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_CreatorMap : ClassMap<Anilist_Creator>
{
    public Anilist_CreatorMap()
    {
        Table("Anilist_Creator");

        Not.LazyLoad();
        Id(x => x.Anilist_CreatorID);

        Map(x => x.AnilistCreatorID).Not.Nullable();
        Map(x => x.Name).Not.Nullable();
        Map(x => x.OriginalName);
        Map(x => x.AlternativeNames).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.Description).Not.Nullable().CustomType("StringClob");
        Map(x => x.ImagePath);
        Map(x => x.Language);
        Map(x => x.PrimaryOccupations).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.Gender).CustomType<PersonGender>().Not.Nullable();
        Map(x => x.DateOfBirth).CustomType<PartialDateOnlyConverter>();
        Map(x => x.HomeTown);
        Map(x => x.FavoriteCount).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
