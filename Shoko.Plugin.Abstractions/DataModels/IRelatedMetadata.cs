
namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Related metadata.
/// </summary>
public interface IRelatedMetadata
{
    /// <summary>
    /// Related entity id.
    /// </summary>
    int RelatedID { get; }

    /// <summary>
    /// Relation type.
    /// </summary>
    RelationType RelationType { get; }
}

/// <summary>
/// Related metadata with entity.
/// </summary>
/// <typeparam name="TMetadata">Related entity type.</typeparam>
public interface IRelatedMetadata<TMetadata> : IMetadata, IRelatedMetadata where TMetadata : IMetadata<int>
{
    /// <summary>
    /// Related entity, if available.
    /// </summary>
    TMetadata? Related { get; }
}
