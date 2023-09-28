using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_PersonMap : ClassMap<TMDB_Person>
{
    public TMDB_PersonMap()
    {
        Table("TMDB_Person");

        Not.LazyLoad();
        Id(x => x.TMDB_PersonID);

        Map(x => x.TmdbPersonID).Not.Nullable();
        Map(x => x.EnglishName).Not.Nullable();
        Map(x => x.EnglishBiography).Not.Nullable();
        Map(x => x.Gender).Not.Nullable().CustomType<PersonGender>();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.BirthDay);
        Map(x => x.DeathDay);
        Map(x => x.PlaceOfBirth);
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
