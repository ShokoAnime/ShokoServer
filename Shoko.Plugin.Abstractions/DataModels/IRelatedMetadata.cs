
using System;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Related metadata.
/// </summary>
public interface IRelatedMetadata : IEquatable<IRelatedMetadata>
{
    /// <summary>
    /// Base entity id.
    /// </summary>
    int BaseID { get; }

    /// <summary>
    /// Related entity id.
    /// </summary>
    int RelatedID { get; }

    /// <summary>
    /// Relation type.
    /// </summary>
    RelationType RelationType { get; }

    /// <summary>
    /// Reverse relation.
    /// </summary>
    /// <returns>The reversed relation.</returns>
    IRelatedMetadata Reversed { get; }
}

/// <summary>
/// Related metadata with entity.
/// </summary>
/// <typeparam name="TMetadata">Related entity type.</typeparam>
public interface IRelatedMetadata<TMetadata> : IMetadata, IRelatedMetadata, IEquatable<IRelatedMetadata<TMetadata>> where TMetadata : IMetadata<int>
{
    /// <summary>
    /// Base entity, if available.
    /// </summary>
    TMetadata? Base { get; }

    /// <summary>
    /// Related entity, if available.
    /// </summary>
    TMetadata? Related { get; }

    /// <summary>
    /// Reverse relation.
    /// </summary>
    /// <returns>The reversed relation.</returns>
    new IRelatedMetadata<TMetadata> Reversed { get; }
}
