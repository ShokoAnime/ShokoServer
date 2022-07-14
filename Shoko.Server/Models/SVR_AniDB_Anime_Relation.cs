using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;
using RType = Shoko.Plugin.Abstractions.DataModels.RelationType;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_Anime_Relation : AniDB_Anime_Relation, IRelatedAnime
    {
        public IAnime RelatedAnime => RepoFactory.AniDB_Anime.GetByAnimeID(RelatedAnimeID);
        RType IRelatedAnime.RelationType =>
            RelationType.ToLowerInvariant() switch
            {
                "prequel" => RType.Prequel,
                "sequel" => RType.Sequel,
                "parent story" => RType.MainStory,
                "side story" => RType.SideStory,
                "full story" => RType.FullStory,
                "summary" => RType.Summary,
                "other" => RType.Other,
                "alternative setting" => RType.AlternativeSetting,
                "alternative version" => RType.AlternativeVersion,
                "same setting" => RType.SameSetting,
                "character" => RType.SharedCharacters,
                _ => RType.Other
            };
    }
}
