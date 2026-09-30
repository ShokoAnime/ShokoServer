using System;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   A stored relation read as the kinds of entry asked for, either the way
///   the source stated it or reversed from the other end.
/// </summary>
/// <typeparam name="TBase">The kind of entry the relation is read from.</typeparam>
/// <typeparam name="TRelated">The kind of entry related to.</typeparam>
/// <param name="source">The source both entries belong to.</param>
/// <param name="baseID">The entry the relation is read from.</param>
/// <param name="relatedID">The entry related to.</param>
/// <param name="relationType">What the related entry is to the base one.</param>
public sealed class MetadataRelation<TBase, TRelated>(
    MetadataSource source,
    MetadataGuid baseID,
    MetadataGuid relatedID,
    RelationType relationType
) : IRelatedMetadata<TBase, TRelated>
    where TBase : IMetadata
    where TRelated : IMetadata
{
    #region Constructors

    /// <summary>
    ///   Reads a stored relation the way the source stated it.
    /// </summary>
    /// <param name="row">The stored relation.</param>
    public MetadataRelation(Metadata_Relation row)
        : this(row.Source, new(row.Source, row.BaseType, row.BaseID), new(row.Source, row.RelatedType, row.RelatedID), row.RelationType) { }

    #endregion

    #region IRelatedMetadata Implementation

    /// <inheritdoc />
    public MetadataSource Source => source;

    /// <inheritdoc />
    public MetadataGuid BaseID => baseID;

    /// <inheritdoc />
    public MetadataGuid RelatedID => relatedID;

    /// <inheritdoc />
    public RelationType RelationType => relationType;

    /// <summary>
    ///   Always <c>true</c>: the source stated the relation itself.
    /// </summary>
    public bool Verified => true;

    /// <inheritdoc />
    public TBase? Base => MetadataEntries.Resolve(baseID) is TBase entry ? entry : default;

    /// <inheritdoc />
    public TRelated? Related => MetadataEntries.Resolve(relatedID) is TRelated entry ? entry : default;

    /// <inheritdoc />
    public IRelatedMetadata<TRelated, TBase> Reversed
        => new MetadataRelation<TRelated, TBase>(source, relatedID, baseID, relationType.Reverse());

    IMetadata? IRelatedMetadata.Base => MetadataEntries.Resolve(baseID);

    IMetadata? IRelatedMetadata.Related => MetadataEntries.Resolve(relatedID);

    IRelatedMetadata IRelatedMetadata.Reversed => Reversed;

    #endregion

    #region Equality

    /// <inheritdoc />
    public bool Equals(IRelatedMetadata? other)
        => other is not null &&
            other.Source == source &&
            other.BaseID == baseID &&
            other.RelatedID == relatedID &&
            other.RelationType == relationType;

    /// <inheritdoc />
    public bool Equals(IRelatedMetadata<TBase, TRelated>? other)
        => Equals((IRelatedMetadata?)other);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is IRelatedMetadata other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(source, baseID, relatedID, relationType);

    #endregion
}
