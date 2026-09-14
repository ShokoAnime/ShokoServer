using FluentNHibernate.Mapping;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases.NHibernate;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_AnimeMap : ClassMap<Anilist_Anime>
{
    public Anilist_AnimeMap()
    {
        Table("Anilist_Anime");

        Not.LazyLoad();
        Id(x => x.Anilist_AnimeID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.MalID);
        Map(x => x.EnglishTitle).Not.Nullable();
        Map(x => x.MainTitle).Not.Nullable();
        Map(x => x.NativeTitle).Not.Nullable();
        Map(x => x.Synonyms).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.EnglishOverview).Not.Nullable();
        Map(x => x.OriginalLanguageCode).Not.Nullable();
        Map(x => x.Type).CustomType<AnimeType>().Not.Nullable();
        Map(x => x.ReleasingStatus).CustomType<AnilistMediaStatus>().Not.Nullable();
        Map(x => x.MediaSource).CustomType<AnilistMediaSource>().Not.Nullable();
        Map(x => x.Season).CustomType<YearlySeason>().Nullable();
        Map(x => x.SeasonYear).Nullable();
        Map(x => x.CoverImagePath).Not.Nullable();
        Map(x => x.TrailerSite).Nullable();
        Map(x => x.TrailerID).Nullable();
        Map(x => x.BannerImagePath).Not.Nullable();
        Map(x => x.EpisodeCount).Not.Nullable();
        Map(x => x.EpisodeDuration).Nullable();
        Map(x => x.UserRating).Not.Nullable();
        Map(x => x.MeanScore).Not.Nullable();
        Map(x => x.UserVotes).Not.Nullable();
        Map(x => x.Popularity).Not.Nullable();
        Map(x => x.FavoriteCount).Not.Nullable();
        Map(x => x.IsLicensed).Not.Nullable();
        Map(x => x.IsRestricted).Not.Nullable();
        Map(x => x.Color).Not.Nullable();
        Map(x => x.Genres).Not.Nullable().CustomType<StringListConverter>();
        Map(x => x.FirstAiredAt).CustomType<PartialDateOnlyConverter>();
        Map(x => x.LastAiredAt).CustomType<PartialDateOnlyConverter>();
        Map(x => x.CreatedAt).Not.Nullable();
        Map(x => x.LastUpdatedAt).Not.Nullable();
    }
}
