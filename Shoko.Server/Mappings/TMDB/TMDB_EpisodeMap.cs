using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Mappings;

public class TMDB_EpisodeMap : ClassMap<TMDB_Episode>
{
    public TMDB_EpisodeMap()
    {
        Table("TMDB_Episode");

        Not.LazyLoad();
        Id(x => x.TMDB_EpisodeID);

        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbSeasonID).Not.Nullable();
        Map(x => x.TmdbEpisodeID).Not.Nullable();
        Map(x => x.TvdbEpisodeID).Nullable();
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.EpisodeNumber).Not.Nullable();
        Map(x => x.SeasonNumber).Not.Nullable();
        Map(x => x.RuntimeMinutes).Column("Runtime");
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
        Map(x => x.AiredAt).CustomType<DateOnlyConverter>();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
