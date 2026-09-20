using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Repositories;

using AbstractRelationType = Shoko.Abstractions.Metadata.Enums.RelationType;

#nullable enable
namespace Shoko.Server.Models.Anilist;

/// <summary>
/// AniList relation between an anime and another media entry. AniList also
/// relates anime to manga and novels, so the related entry is only exposed as
/// a series when it is an anime. Mirrors <see cref="AniDB.AniDB_Anime_Relation"/>.
/// </summary>
public class Anilist_Anime_Relation : IRelatedMetadata<ISeries, ISeries>, IEquatable<IRelatedMetadata<ISeries, ISeries>>, IEquatable<Anilist_Anime_Relation>
{
    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int Anilist_Anime_RelationID { get; set; }

    /// <summary>
    /// AniList Anime ID.
    /// </summary>
    public int AnilistAnimeID { get; set; }

    /// <summary>
    /// AniList Media ID of the related entry. Only an anime when
    /// <see cref="RelatedIsAnime"/> is set.
    /// </summary>
    public int RelatedAnilistID { get; set; }

    /// <summary>
    /// Whether the related entry is an anime, as opposed to a manga or novel.
    /// </summary>
    public bool RelatedIsAnime { get; set; }

    /// <summary>
    /// The raw AniList relation type, e.g. <c>PREQUEL</c> or <c>SIDE_STORY</c>.
    /// </summary>
    public string RelationType { get; set; } = string.Empty;

    #endregion

    #region Computed Properties

    /// <summary>
    /// The abstract relation type.
    /// </summary>
    public AbstractRelationType AbstractRelationType => ParseRelationType(RelationType);

    /// <summary>
    /// Map an AniList <c>MediaRelation</c> to an abstract <see cref="AbstractRelationType"/>.
    /// </summary>
    public static AbstractRelationType ParseRelationType(string? relationType) => relationType?.ToUpperInvariant() switch
    {
        "PREQUEL" => AbstractRelationType.Prequel,
        "SEQUEL" => AbstractRelationType.Sequel,
        "PARENT" => AbstractRelationType.MainStory,
        "SIDE_STORY" => AbstractRelationType.SideStory,
        "SPIN_OFF" => AbstractRelationType.SideStory,
        "SUMMARY" => AbstractRelationType.Summary,
        "COMPILATION" => AbstractRelationType.Summary,
        "ALTERNATIVE" => AbstractRelationType.AlternativeVersion,
        "CHARACTER" => AbstractRelationType.SharedCharacters,
        "SAME_UNIVERSE" => AbstractRelationType.SameSetting,
        _ => AbstractRelationType.Other,
    };

    /// <summary>
    /// The reversed relation.
    /// </summary>
    public Anilist_Anime_Relation Reversed => new()
    {
        AnilistAnimeID = RelatedAnilistID,
        RelatedAnilistID = AnilistAnimeID,
        RelatedIsAnime = true,
        RelationType = ReverseRawRelationType(RelationType),
    };

    private static string ReverseRawRelationType(string relationType) => relationType.ToUpperInvariant() switch
    {
        "PREQUEL" => "SEQUEL",
        "SEQUEL" => "PREQUEL",
        "PARENT" => "SIDE_STORY",
        "SIDE_STORY" => "PARENT",
        "SPIN_OFF" => "PARENT",
        "SUMMARY" => "CONTAINS",
        "CONTAINS" => "SUMMARY",
        "ADAPTATION" => "SOURCE",
        "SOURCE" => "ADAPTATION",
        var other => other,
    };

    #endregion

    #region Constructors

    /// <summary>
    /// Default constructor for NHibernate.
    /// </summary>
    public Anilist_Anime_Relation() { }

    /// <summary>
    /// Creates a new relation.
    /// </summary>
    public Anilist_Anime_Relation(int anilistAnimeId, int relatedAnilistId, bool relatedIsAnime, string relationType)
    {
        AnilistAnimeID = anilistAnimeId;
        RelatedAnilistID = relatedAnilistId;
        RelatedIsAnime = relatedIsAnime;
        RelationType = relationType;
    }

    #endregion

    #region Navigation Properties

    /// <summary>
    /// Gets the anime the relation belongs to.
    /// </summary>
    public Anilist_Anime? Anime => RepoFactory.Anilist_Anime.GetByAnilistAnimeID(AnilistAnimeID);

    /// <summary>
    /// Gets the related anime, if the related entry is an anime and is available locally.
    /// </summary>
    public Anilist_Anime? RelatedAnime => RelatedIsAnime ? RepoFactory.Anilist_Anime.GetByAnilistAnimeID(RelatedAnilistID) : null;

    #endregion

    #region Equality

    public bool Equals(IRelatedMetadata? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return AnilistAnimeID == other.BaseID && RelatedAnilistID == other.RelatedID && AbstractRelationType == other.RelationType;
    }

    public bool Equals(IRelatedMetadata<ISeries, ISeries>? other)
        => other is IRelatedMetadata otherMetadata && Equals(otherMetadata);

    public bool Equals(Anilist_Anime_Relation? other)
        => other is not null && AnilistAnimeID == other.AnilistAnimeID && RelatedAnilistID == other.RelatedAnilistID && AbstractRelationType == other.AbstractRelationType;

    public override bool Equals(object? other)
        => other is Anilist_Anime_Relation relation ? Equals(relation) : other is IRelatedMetadata metadata && Equals(metadata);

    public override int GetHashCode()
        => HashCode.Combine(AnilistAnimeID, RelatedAnilistID, AbstractRelationType);

    #endregion

    #region IRelatedMetadata Implementation

    int IRelatedMetadata.BaseID => AnilistAnimeID;

    int IRelatedMetadata.RelatedID => RelatedAnilistID;

    IMetadata<int>? IRelatedMetadata.Base => Anime;

    IMetadata<int>? IRelatedMetadata.Related => RelatedAnime;

    AbstractRelationType IRelatedMetadata.RelationType => AbstractRelationType;

    DataSource IRelatedMetadata.Source => DataSource.AniList;

    bool IRelatedMetadata.Verified => true;

    IRelatedMetadata IRelatedMetadata.Reversed => Reversed;

    #endregion

    #region IRelatedMetadata<ISeries> Implementation

    ISeries? IRelatedMetadata<ISeries, ISeries>.Base => Anime;

    ISeries? IRelatedMetadata<ISeries, ISeries>.Related => RelatedAnime;

    IRelatedMetadata<ISeries, ISeries> IRelatedMetadata<ISeries, ISeries>.Reversed => Reversed;

    #endregion
}
