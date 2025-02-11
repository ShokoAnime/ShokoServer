using FluentNHibernate.Mapping;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_SeasonMap : ClassMap<TMDB_Season>
{
    public TMDB_SeasonMap()
    {
        Table("TMDB_Season");

        Not.LazyLoad();
        Id(x => x.TMDB_SeasonID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbSeasonID).Not.Nullable();
        Map(x => x.PosterPath).Nullable();
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.HiddenEpisodeCount).Not.Nullable();
        Map(x => x.SeasonNumber).Not.Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
