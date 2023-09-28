using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_MovieMap : ClassMap<TMDB_Movie>
{
    public TMDB_MovieMap()
    {
        Table("TMDB_Movie");

        Not.LazyLoad();
        Id(x => x.TMDB_MovieID);

        Map(x => x.TmdbMovieID).Not.Nullable();
        Map(x => x.TmdbCollectionID);
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.OriginalTitle).Not.Nullable();
        Map(x => x.OriginalLanguageCode).Not.Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.IsVideo).Not.Nullable();
        Map(x => x.Genres).Not.Nullable();
        Map(x => x.ContentRatings).Not.Nullable();
        Map(x => x.RuntimeMintues);
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
        Map(x => x.ReleasedAt);
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
