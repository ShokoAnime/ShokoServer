using FluentNHibernate.Mapping;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_TMDB_EpisodeMap : ClassMap<CrossRef_AniDB_TMDB_Episode>
{
    public CrossRef_AniDB_TMDB_EpisodeMap()
    {
        Table("CrossRef_AniDB_TMDB_Episode");

        Not.LazyLoad();
        Id(x => x.CrossRef_AniDB_TMDB_EpisodeID);

        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.AnidbEpisodeID).Not.Nullable();
        Map(x => x.TmdbShowID).Not.Nullable();
        Map(x => x.TmdbEpisodeID).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.MatchRating).Not.Nullable();
    }
}
