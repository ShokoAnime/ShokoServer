using System;
using System.Collections.Generic;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Plugin.Models;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Contains information about an <see cref="IAiringScheduleProvider"/>.
/// </summary>
public class AiringScheduleProviderInfo
{
    /// <summary>
    ///   The unique ID of the provider.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   The version of the airing schedule provider.
    /// </summary>
    public required Version Version { get; init; }

    /// <summary>
    ///   The display name of the airing schedule provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   Describes what the airing schedule provider is for.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   The <see cref="IAiringScheduleProvider"/> that this info is for.
    /// </summary>
    public required IAiringScheduleProvider Provider { get; init; }

    /// <summary>
    ///   Information about the configuration that the airing schedule provider
    ///   uses.
    /// </summary>
    public required ConfigurationInfo? ConfigurationInfo { get; init; }

    /// <summary>
    ///   Information about the plugin that the airing schedule provider belongs
    ///   to.
    /// </summary>
    public required LocalPluginInfo PluginInfo { get; init; }

    /// <summary>
    ///   The source order of the provider. It decides whose airing wins when
    ///   two providers report the same channel for an episode, and breaks ties
    ///   during selection. It is not a ranking of results, and never hides
    ///   another provider's airings.
    /// </summary>
    public required int Priority { get; set; }

    /// <summary>
    ///   The enabled kinds, a subset of
    ///   <see cref="IAiringScheduleProvider.AvailableKinds"/>. Tracks of a
    ///   disabled kind are still stored, but hidden from reads.
    /// </summary>
    public required HashSet<AiringKind> EnabledKinds { get; set; }

    /// <summary>
    ///   How long after a sweep finishes before the next one starts, for a
    ///   provider that implements
    ///   <see cref="ISweepingAiringScheduleProvider"/>. Meaningless for one
    ///   that does not, and left at the default there.
    /// </summary>
    /// <remarks>
    ///   This is the user's, the way
    ///   <see cref="EnabledKinds"/> is: it is seeded from the provider's own
    ///   <see cref="ISweepingAiringScheduleProvider.SuggestedSweepInterval"/>,
    ///   or from the server's default when the provider suggests nothing, and
    ///   whatever it is set to afterwards is what the server uses. Values below
    ///   fifteen minutes are clamped on load, since a sweep is a walk of a whole
    ///   source rather than a poll.
    /// </remarks>
    public required TimeSpan SweepInterval { get; set; }

    /// <summary>
    ///   Whether or not the provider is enabled for automatic usage. A provider
    ///   with no enabled kinds is disabled.
    /// </summary>
    public bool Enabled => EnabledKinds.Count > 0;
}
