using System;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Repositories;

using RType = Shoko.Plugin.Abstractions.DataModels.RelationType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AniDB_Anime_Relation : AniDB_Anime_Relation, IRelatedMetadata<ISeries, ISeries>, IEquatable<IRelatedMetadata<ISeries, ISeries>>, IEquatable<SVR_AniDB_Anime_Relation>
{
    public RType AbstractRelationType =>
        RelationType.ToLowerInvariant() switch
        {
            "parent story" => RType.MainStory,
            "side story" => RType.SideStory,
            "full story" => RType.FullStory,
            "alternative setting" => RType.AlternativeSetting,
            "alternative version" => RType.AlternativeVersion,
            "same setting" => RType.SameSetting,
            "character" => RType.SharedCharacters,
            _ => Enum.TryParse<RType>(RelationType, true, out var type)
                ? type
                : RType.Other
        };

    public bool Equals(IRelatedMetadata? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return AnimeID == other.BaseID &&
            RelatedAnimeID == other.RelatedID &&
            AbstractRelationType == other.RelationType;
    }

    public bool Equals(IRelatedMetadata<ISeries, ISeries>? other)
        => other is IRelatedMetadata otherMetadata && Equals(otherMetadata);

    public bool Equals(SVR_AniDB_Anime_Relation? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        return AnimeID == other.AnimeID &&
            RelatedAnimeID == other.RelatedAnimeID &&
            AbstractRelationType == other.AbstractRelationType;
    }

    public override bool Equals(object? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (other is IRelatedMetadata otherMetadata)
            return Equals(otherMetadata);

        if (other is SVR_AniDB_Anime_Relation otherRelation)
            return Equals(otherRelation);

        return false;

    }

    public override int GetHashCode()
        => HashCode.Combine(AnimeID, RelatedAnimeID, AbstractRelationType);

    #region IMetadata implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    #endregion

    #region IRelatedMetadata implementation

    int IRelatedMetadata.BaseID => AnimeID;

    int IRelatedMetadata.RelatedID => RelatedAnimeID;
    RType IRelatedMetadata.RelationType => AbstractRelationType;

    #endregion

    #region IRelatedMetadata<ISeries> implementation

    ISeries? IRelatedMetadata<ISeries, ISeries>.Base =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    ISeries? IRelatedMetadata<ISeries, ISeries>.Related =>
        RepoFactory.AniDB_Anime.GetByAnimeID(RelatedAnimeID);

    IRelatedMetadata<ISeries, ISeries> IRelatedMetadata<ISeries, ISeries>.Reversed => new SVR_AniDB_Anime_Relation
    {
        AnimeID = RelatedAnimeID,
        RelatedAnimeID = AnimeID,
        RelationType = ((IRelatedMetadata)this).RelationType.Reverse().ToString(),
    };

    IRelatedMetadata IRelatedMetadata.Reversed => new SVR_AniDB_Anime_Relation
    {
        AnimeID = RelatedAnimeID,
        RelatedAnimeID = AnimeID,
        RelationType = ((IRelatedMetadata)this).RelationType.Reverse().ToString(),
    };

    #endregion
}
