using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Base metadata interface.
/// </summary>
public interface IMetadata
{
    /// <summary>
    /// The source of the metadata.
    /// </summary>
    DataSource Source { get; }
}

/// <summary>
/// Base metadata interface with an ID.
/// </summary>
public interface IMetadata<TId> : IMetadata
{
    /// <summary>
    /// The ID of the metadata.
    /// </summary>
    TId ID { get; }
}
