using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_ShowMap : ClassMap<TMDB_Show>
{
    public TMDB_ShowMap()
    {
        Table("TMDB_Show");

        Not.LazyLoad();
        Id(x => x.TMDB_ShowID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TvdbShowID).Nullable();
        Map(x => x.PosterPath).Nullable();
        Map(x => x.BackdropPath).Nullable();
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.OriginalTitle).Not.Nullable();
        Map(x => x.OriginalLanguageCode).Not.Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.Genres).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.Keywords).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.ContentRatings).Not.Nullable().CustomType<TmdbContentRatingConverter>();
        Map(x => x.ProductionCountries).Not.Nullable().CustomType<TmdbProductionCountryConverter>();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.SeasonCount).Not.Nullable();
        Map(x => x.AlternateOrderingCount).Not.Nullable();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
        Map(x => x.FirstAiredAt).CustomType<DateOnlyConverter>();
        Map(x => x.LastAiredAt).CustomType<DateOnlyConverter>();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
        Map(x => x.PreferredAlternateOrderingID);
    }
}
