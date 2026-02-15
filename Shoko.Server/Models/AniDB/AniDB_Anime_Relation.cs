using System;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Repositories;

using AbstractRelationType = Shoko.Abstractions.Enums.RelationType;

#nullable enable
namespace Shoko.Server.Models.AniDB;

public class AniDB_Anime_Relation : IRelatedMetadata<ISeries, ISeries>, IEquatable<IRelatedMetadata<ISeries, ISeries>>, IEquatable<AniDB_Anime_Relation>
{
    #region Database Columns

    public int AniDB_Anime_RelationID { get; set; }

    public int AnimeID { get; set; }

    public string RelationType { get; set; } = string.Empty;

    public int RelatedAnimeID { get; set; }

    #endregion

    public AbstractRelationType AbstractRelationType =>
        RelationType.ToLowerInvariant() switch
        {
            "parent story" => AbstractRelationType.MainStory,
            "side story" => AbstractRelationType.SideStory,
            "full story" => AbstractRelationType.FullStory,
            "alternative setting" => AbstractRelationType.AlternativeSetting,
            "alternative version" => AbstractRelationType.AlternativeVersion,
            "same setting" => AbstractRelationType.SameSetting,
            "character" => AbstractRelationType.SharedCharacters,
            _ => Enum.TryParse<AbstractRelationType>(RelationType, true, out var type)
                ? type
                : AbstractRelationType.Other
        };

    public AniDB_Anime_Relation Reversed => new()
    {
        AnimeID = RelatedAnimeID,
        RelatedAnimeID = AnimeID,
        RelationType = ((IRelatedMetadata)this).RelationType.Reverse().ToString(),
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

    public bool Equals(AniDB_Anime_Relation? other)
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

        if (other is AniDB_Anime_Relation otherRelation)
            return Equals(otherRelation);

        return false;

    }

    public override int GetHashCode()
        => HashCode.Combine(AnimeID, RelatedAnimeID, AbstractRelationType);

    #region IMetadata implementation

    DataSource IMetadata.Source => DataSource.AniDB;

    #endregion

    #region IRelatedMetadata implementation

    int IRelatedMetadata.BaseID => AnimeID;

    int IRelatedMetadata.RelatedID => RelatedAnimeID;
    AbstractRelationType IRelatedMetadata.RelationType => AbstractRelationType;

    #endregion

    #region IRelatedMetadata<ISeries> implementation

    ISeries? IRelatedMetadata<ISeries, ISeries>.Base =>
        RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

    ISeries? IRelatedMetadata<ISeries, ISeries>.Related =>
        RepoFactory.AniDB_Anime.GetByAnimeID(RelatedAnimeID);

    IRelatedMetadata<ISeries, ISeries> IRelatedMetadata<ISeries, ISeries>.Reversed => new AniDB_Anime_Relation
    {
        AnimeID = RelatedAnimeID,
        RelatedAnimeID = AnimeID,
        RelationType = ((IRelatedMetadata)this).RelationType.Reverse().ToString(),
    };

    IRelatedMetadata IRelatedMetadata.Reversed => Reversed;

    #endregion
}
