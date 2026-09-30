using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   Reads the orderings of a series and an episode for the models, which
///   are not built through dependency injection. Without the service, as in
///   unit tests, only the default ordering is there.
/// </summary>
internal static class OrderingLookup
{
    /// <summary>
    ///   The ordering service, when the server has one.
    /// </summary>
    internal static MetadataOrderingService? Service
        => ISystemService.HasStaticServices ? ISystemService.StaticServices.GetService<MetadataOrderingService>() : null;

    /// <summary>
    ///   Every ordering of a series, the default one first.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The orderings.</returns>
    internal static IReadOnlyList<IOrdering> For(ISeries series)
        => Service?.GetOrderings(series) ?? [new DefaultOrdering(series, null)];

    /// <summary>
    ///   The ordering chosen for a series, or its default one.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The ordering.</returns>
    internal static IOrdering PreferredFor(ISeries series)
        => Service?.GetPreferredOrdering(series) ?? new DefaultOrdering(series, null);

    /// <summary>
    ///   Every place an episode has in its series' orderings.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The places, its place in the default ordering first.</returns>
    internal static IReadOnlyList<IEpisodeOrderingInformation> For(IEpisode episode)
        => Service?.GetEpisodeOrderings(episode) ?? [new DefaultEpisodeOrdering(episode, episode.Series, null)];

    /// <summary>
    ///   Whether an ordering is the one chosen for a series. Without the
    ///   service nothing is chosen.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <param name="orderingID">One of its orderings, not the default one.</param>
    /// <returns><c>true</c> if it is the chosen one.</returns>
    internal static bool IsChosen(MetadataGuid seriesID, MetadataGuid orderingID)
        => Service?.IsChosen(seriesID, orderingID) ?? false;

    /// <summary>
    ///   Whether a user hid an episode. Without the service none is hidden.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns><c>true</c> if it is hidden.</returns>
    internal static bool IsHidden(MetadataGuid episodeID)
        => Service?.IsEpisodeHidden(episodeID) ?? false;

    /// <summary>
    ///   The episode's first place in the ordering chosen for its series.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The place, or <c>null</c> when that ordering leaves it out.</returns>
    internal static IEpisodeOrderingInformation? PreferredFor(IEpisode episode)
        => For(episode).FirstOrDefault(place => place.IsPreferred);
}
