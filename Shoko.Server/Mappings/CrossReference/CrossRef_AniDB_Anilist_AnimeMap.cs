using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Mappings.CrossReference;

public class CrossRef_AniDB_Anilist_AnimeMap : ClassMap<CrossRef_AniDB_Anilist_Anime>
{
    public CrossRef_AniDB_Anilist_AnimeMap()
    {
        Table("CrossRef_AniDB_Anilist_Anime");

        Not.LazyLoad();
        Id(x => x.CrossRef_AniDB_Anilist_AnimeID);

        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.MatchRating).CustomType<MatchRating>().Not.Nullable();
    }
}
