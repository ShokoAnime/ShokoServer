using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_Metadata_MovieMap : ClassMap<CrossRef_AniDB_Metadata_Movie>
{
    public CrossRef_AniDB_Metadata_MovieMap()
    {
        Table("CrossRef_AniDB_Metadata_Movie");

        Not.LazyLoad();
        Id(x => x.CrossRef_AniDB_Metadata_MovieID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.AnidbEpisodeID).Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.MatchRating).CustomType<MatchRating>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.WrittenBy).Nullable();
    }
}
