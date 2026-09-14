using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Mappings.CrossReference;

public class CrossRef_AniDB_Anilist_EpisodeMap : ClassMap<CrossRef_AniDB_Anilist_Episode>
{
    public CrossRef_AniDB_Anilist_EpisodeMap()
    {
        Table("CrossRef_AniDB_Anilist_Episode");

        Not.LazyLoad();
        Id(x => x.CrossRef_AniDB_Anilist_EpisodeID);

        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.AnidbEpisodeID).Not.Nullable();
        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.AnilistEpisodeID).Not.Nullable();
        Map(x => x.EpisodeNumber).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.MatchRating).CustomType<MatchRating>().Not.Nullable();
    }
}
