using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.DataModels;

public interface IMetadata
{
    DataSourceEnum Source { get; }
}

public interface IMetadata<TId> : IMetadata where TId : struct
{
    TId ID { get; }
}
