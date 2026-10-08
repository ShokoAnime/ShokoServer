using System;
using System.Collections.Generic;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   Contains information about an <see cref="ISuspensionProvider"/>.
/// </summary>
public sealed record SuspensionProviderInfo
{
    /// <summary>
    ///   The unique ID of the provider.
    /// </summary>
    /// <remarks>
    ///   Derived from the provider's type and its plugin, so it survives a
    ///   rename and a reinstall.
    /// </remarks>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The <see cref="ISuspensionProvider"/> that this info is for.
    /// </summary>
    public required ISuspensionProvider Provider { get; init; }

    /// <summary>
    ///   Information about the plugin that the provider belongs to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }

    /// <summary>
    ///   The display name of the provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   Describes what is suspended, or <c>null</c>.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    ///   The types of the providers whose queued jobs wait while this
    ///   provider is suspended, read once at registration.
    /// </summary>
    public required IReadOnlyList<Type> HeldProviderTypes { get; init; }
}
