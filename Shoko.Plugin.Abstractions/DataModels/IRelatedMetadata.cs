
#nullable enable
namespace Shoko.Plugin.Abstractions.DataModels;

public interface IRelatedMetadata
{
    int RelatedID { get; }

    RelationType RelationType { get; }
}

public interface IRelatedMetadata<TMetadata> : IMetadata, IRelatedMetadata where TMetadata : IMetadata<int>
{
    TMetadata? Related { get; }
}
