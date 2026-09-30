using System;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Related metadata.
/// </summary>
public interface IRelatedMetadata : IEquatable<IRelatedMetadata>
{
    /// <summary>
    ///   The entry the relation is read from.
    /// </summary>
    MetadataGuid BaseID { get; }

    /// <summary>
    ///   The entry related to, which need not be the same kind as the base:
    ///   a series can be related to a film.
    /// </summary>
    MetadataGuid RelatedID { get; }

    /// <summary>
    /// Base entity, if available.
    /// </summary>
    IMetadata? Base { get; }

    /// <summary>
    /// Related entity, if available.
    /// </summary>
    IMetadata? Related { get; }

    /// <summary>
    /// Relation type.
    /// </summary>
    RelationType RelationType { get; }

    /// <summary>
    /// Reverse relation.
    /// </summary>
    /// <returns>The reversed relation.</returns>
    IRelatedMetadata Reversed { get; }

    /// <summary>
    ///   The source of the relation.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    ///   Whether the relation has been verified to be correct. For now, only
    ///   relevant to AniDB relations.
    /// </summary>
    bool Verified { get; }
}

/// <summary>
/// Related metadata with entity.
/// </summary>
/// <typeparam name="TBaseMetadata">Base entity type.</typeparam>
/// <typeparam name="TRelatedMetadata">Related entity type.</typeparam>
public interface IRelatedMetadata<TBaseMetadata, TRelatedMetadata> : IRelatedMetadata, IEquatable<IRelatedMetadata<TBaseMetadata, TRelatedMetadata>> where TBaseMetadata : IMetadata where TRelatedMetadata : IMetadata
{
    /// <summary>
    /// Base entity, if available.
    /// </summary>
    new TBaseMetadata? Base { get; }

    /// <summary>
    /// Related entity, if available.
    /// </summary>
    new TRelatedMetadata? Related { get; }

    /// <summary>
    /// Reverse relation.
    /// </summary>
    /// <returns>The reversed relation.</returns>
    new IRelatedMetadata<TRelatedMetadata, TBaseMetadata> Reversed { get; }
}
