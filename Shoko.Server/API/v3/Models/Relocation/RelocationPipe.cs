using System;
using Shoko.Plugin.Abstractions.Relocation;

#nullable enable
namespace Shoko.Server.API.v3.Models.Relocation;

/// <summary>
///   A relocation pipe in the APIv3.
/// </summary>
/// <param name="pipe">The pipe info.</param>
/// <param name="provider">The provider info.</param>
public class RelocationPipe(RelocationPipeInfo pipe, RelocationProviderInfo? provider)
{
    /// <summary>
    ///   The ID of the pipe.
    /// </summary>
    public Guid ID { get; init; } = pipe.ID;

    /// <summary>
    ///   The provider ID for this pipe.
    /// </summary>
    public Guid ProviderID { get; init; } = pipe.ProviderID;

    /// <summary>
    ///   The friendly name of the pipe, for display.
    /// </summary>
    public string Name { get; init; } = pipe.Name;

    /// <summary>
    ///   Indicates if this pipe is the default pipe.
    /// </summary>
    public bool IsDefault { get; init; } = pipe.IsDefault;

    /// <summary>
    /// Indicates that this pipe is currently usable.
    /// </summary>
    public bool IsUsable { get; init; } = provider is not null;

    /// <summary>
    ///   Indicates that the pipe has a configuration attached to it.
    /// </summary>
    public bool HasConfiguration { get; init; } = provider is null ? pipe.Configuration is { Length: > 0 } : provider.ConfigurationInfo is not null;
}
