using FluentNHibernate.Mapping;
using Shoko.Server.Models.Anilist;

namespace Shoko.Server.Mappings.Anilist;

public class Anilist_Anime_SuggestionMap : ClassMap<Anilist_Anime_Suggestion>
{
    public Anilist_Anime_SuggestionMap()
    {
        Table("Anilist_Anime_Suggestion");

        Not.LazyLoad();
        Id(x => x.Anilist_Anime_SuggestionID);

        Map(x => x.AnilistAnimeID).Not.Nullable();
        Map(x => x.SuggestedAnilistAnimeID).Not.Nullable();
        Map(x => x.Rating).Not.Nullable();
        Map(x => x.Ordering).Not.Nullable();
    }
}
