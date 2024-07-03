using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Repositories;

using RType = Shoko.Plugin.Abstractions.DataModels.RelationType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AniDB_Anime_Relation : AniDB_Anime_Relation, IRelatedAnime, IRelatedMetadata<ISeries>
{
    #region IMetadata implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    #endregion

    #region IRelatedMetadata implementation

    int IRelatedMetadata.RelatedID => RelatedAnimeID;

    RType IRelatedMetadata.RelationType =>
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

    #endregion

    #region IRelatedMetadata<ISeries> implementation

    ISeries? IRelatedMetadata<ISeries>.Related =>
        RepoFactory.AniDB_Anime.GetByAnimeID(RelatedAnimeID);

    #endregion

    #region IRelatedAnime implementation

    ISeries? IRelatedAnime.RelatedAnime =>
        RepoFactory.AniDB_Anime.GetByAnimeID(RelatedAnimeID);

    RType IRelatedAnime.RelationType => (this as IRelatedMetadata).RelationType;

    #endregion


}
