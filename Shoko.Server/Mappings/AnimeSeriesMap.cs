using FluentNHibernate.Mapping;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Server;

namespace Shoko.Server.Mappings;

public class AnimeSeriesMap : ClassMap<AnimeSeries>
{
    public AnimeSeriesMap()
    {
        Table("AnimeSeries");
        Not.LazyLoad();
        Id(x => x.AnimeSeriesID);

        Map(x => x.AniDB_ID).Not.Nullable();
        Map(x => x.AnimeGroupID).Not.Nullable();
        Map(x => x.DateTimeCreated).Not.Nullable();
        Map(x => x.DateTimeUpdated).Not.Nullable();
        Map(x => x.DefaultAudioLanguage);
        Map(x => x.DefaultSubtitleLanguage);
        Map(x => x.LatestLocalEpisodeNumber).Not.Nullable();
        Map(x => x.EpisodeAddedDate);
        Map(x => x.LatestEpisodeAirDate);
        Map(x => x.MissingEpisodeCount).Not.Nullable();
        Map(x => x.MissingEpisodeCountGroups).Not.Nullable();
        Map(x => x.HiddenMissingEpisodeCount).Not.Nullable();
        Map(x => x.HiddenMissingEpisodeCountGroups).Not.Nullable();
        Map(x => x.SeriesNameOverride);
        Map(x => x.AirsOn);
        Map(x => x.UpdatedAt).Not.Nullable();
        Map(x => x.DisableAutoMatchFlags).Not.Nullable().CustomType<DisabledAutoMatchFlag>();
    }
}
