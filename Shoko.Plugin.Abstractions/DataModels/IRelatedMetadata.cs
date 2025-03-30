
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
/// <typeparam name="TBaseMetadata">Base entity type.</typeparam>
/// <typeparam name="TRelatedMetadata">Related entity type.</typeparam>
public interface IRelatedMetadata<TBaseMetadata, TRelatedMetadata> : IMetadata, IRelatedMetadata, IEquatable<IRelatedMetadata<TBaseMetadata, TRelatedMetadata>> where TBaseMetadata : IMetadata<int> where TRelatedMetadata : IMetadata<int>
{
    /// <summary>
    /// Base entity, if available.
    /// </summary>
    TBaseMetadata? Base { get; }

    /// <summary>
    /// Related entity, if available.
    /// </summary>
    TBaseMetadata? Related { get; }

    /// <summary>
    /// Reverse relation.
    /// </summary>
    /// <returns>The reversed relation.</returns>
    new IRelatedMetadata<TRelatedMetadata, TBaseMetadata> Reversed { get; }
}
