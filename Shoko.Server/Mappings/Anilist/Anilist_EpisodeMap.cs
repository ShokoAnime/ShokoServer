using FluentNHibernate.Mapping;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_EpisodeMap : ClassMap<Anilist_Episode>
{
    public Anilist_EpisodeMap()
    {
        Table("Anilist_Episode");

        Not.LazyLoad();
        Id(x => x.Anilist_EpisodeID);

        Map(x => x.AnilistEpisodeID).Not.Nullable();
        Map(x => x.AnilistScheduleEpisodeID).Nullable();
        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.EpisodeNumber).Not.Nullable();
        Map(x => x.RuntimeMinutes).Nullable();
        Map(x => x.AiredAt).Nullable();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
