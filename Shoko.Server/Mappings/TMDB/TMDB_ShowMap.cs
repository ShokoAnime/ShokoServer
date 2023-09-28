using FluentNHibernate.Mapping;
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
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.OriginalTitle).Not.Nullable();
        Map(x => x.OriginalLanguageCode).Not.Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.Genres).Not.Nullable();
        Map(x => x.ContentRatings).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.SeasonCount).Not.Nullable();
        Map(x => x.AlternateOrderingCount).Not.Nullable();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
        Map(x => x.FirstAiredAt).Not.Nullable();
        Map(x => x.LastAiredAt);
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
