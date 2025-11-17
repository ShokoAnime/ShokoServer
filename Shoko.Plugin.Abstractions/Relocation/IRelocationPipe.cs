using System;

namespace Shoko.Plugin.Abstractions.Relocation;

/// <summary>
/// 
/// </summary>
public interface IRelocationPipe
{
    /// <summary>
    /// The provider ID for this pipe.
    /// </summary>
    Guid ProviderID { get; }

    /// <summary>
    ///   The packed configuration for this pipe, if the
    ///   <see cref="IRelocationProvider"/> for the <see cref="ProviderID"/>
    ///   supports configuration.
    /// </summary>
    byte[]? Configuration { get; }
}

/// <summary>
/// 
/// </summary>
public interface IStoredRelocationPipe : IRelocationPipe
{
    /// <summary>
    /// The ID of the pipe.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    /// The friendly name of the pipe, for display.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Indicates if this pipe is the default pipe.
    /// </summary>
    bool IsDefault { get; }
}
