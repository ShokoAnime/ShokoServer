using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Mappings;

public class AniDB_Anime_StartSeasonOverrideMap : ClassMap<AniDB_Anime_StartSeasonOverride>
{
    public AniDB_Anime_StartSeasonOverrideMap()
    {
        Table("AniDB_Anime_StartSeasonOverride");

        Not.LazyLoad();
        Id(x => x.AniDB_Anime_StartSeasonOverrideID);

        Map(x => x.AnimeID).Not.Nullable().Unique();
        Map(x => x.Year).Not.Nullable();
        Map(x => x.Season).CustomType<YearlySeason>().Not.Nullable();
        Map(x => x.UserID).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.UpdatedAt).Not.Nullable();
    }
}
