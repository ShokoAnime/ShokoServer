using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.CrossReference;

namespace Shoko.Server.Mappings;

public class CrossRef_AniDB_Metadata_EpisodeMap : ClassMap<CrossRef_AniDB_Metadata_Episode>
{
    public CrossRef_AniDB_Metadata_EpisodeMap()
    {
        Table("CrossRef_AniDB_Metadata_Episode");

        Not.LazyLoad();
        Id(x => x.CrossRef_AniDB_Metadata_EpisodeID);

        Map(x => x.Source).CustomType<MetadataSourceType>().Not.Nullable();
        Map(x => x.AnidbAnimeID).Not.Nullable();
        Map(x => x.AnidbEpisodeID).Not.Nullable();
        Map(x => x.ProviderID).Not.Nullable();
        Map(x => x.ProviderParentID).Not.Nullable();
        Map(x => x.ProviderSeasonID).Nullable();
        Map(x => x.SeasonNumber).Nullable();
        Map(x => x.EpisodeNumber).Nullable();
        Map(x => x.MatchRating).CustomType<MatchRating>().Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
        Map(x => x.WrittenBy).Nullable();
    }
}
