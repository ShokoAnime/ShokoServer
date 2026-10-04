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
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series, an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
    /// <returns>The orderings.</returns>
    internal static IReadOnlyList<IOrdering<TSeries, TEpisode>> For<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => Service?.GetOrderings<TSeries, TEpisode>(series) ?? [new DefaultOrdering<TSeries, TEpisode>(series, null)];

    /// <summary>
    ///   The ordering chosen for a series, or its default one.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series, an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
    /// <returns>The ordering.</returns>
    internal static IOrdering<TSeries, TEpisode> PreferredFor<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => Service?.GetPreferredOrdering<TSeries, TEpisode>(series) ?? new DefaultOrdering<TSeries, TEpisode>(series, null);

    /// <summary>
    ///   The default ordering of a series, made from its own seasons.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="series">The series, an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
    /// <returns>The default ordering.</returns>
    internal static IOrdering<TSeries, TEpisode> DefaultFor<TSeries, TEpisode>(TSeries series)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => Service?.GetDefaultOrdering<TSeries, TEpisode>(series) ?? new DefaultOrdering<TSeries, TEpisode>(series, null);

    /// <summary>
    ///   An episode's place in the default ordering of its series, holding
    ///   the episode itself.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="episode">The episode, an <see cref="IEpisode{TSeries,TEpisode}"/>.</param>
    /// <returns>The place.</returns>
    internal static IEpisodeOrderingInformation<TSeries, TEpisode> DefaultPlaceOf<TSeries, TEpisode>(TEpisode episode)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => Service?.GetDefaultEpisodeOrdering<TSeries, TEpisode>(episode) ??
            new DefaultEpisodeOrdering<TSeries, TEpisode>(episode, ((IEpisode<TSeries, TEpisode>)episode).Series, null);

    /// <summary>
    ///   Every place an episode has in its series' orderings.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="episode">The episode, an <see cref="IEpisode{TSeries,TEpisode}"/>.</param>
    /// <returns>The places, its place in the default ordering first.</returns>
    internal static IReadOnlyList<IEpisodeOrderingInformation<TSeries, TEpisode>> PlacesOf<TSeries, TEpisode>(TEpisode episode)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => Service?.GetEpisodeOrderings<TSeries, TEpisode>(episode) ??
            [new DefaultEpisodeOrdering<TSeries, TEpisode>(episode, ((IEpisode<TSeries, TEpisode>)episode).Series, null)];

    /// <summary>
    ///   The episode's first place in the ordering chosen for its series.
    /// </summary>
    /// <typeparam name="TSeries">The series' type.</typeparam>
    /// <typeparam name="TEpisode">The episodes' type.</typeparam>
    /// <param name="episode">The episode, an <see cref="IEpisode{TSeries,TEpisode}"/>.</param>
    /// <returns>The place, or <c>null</c> when that ordering leaves it out.</returns>
    internal static IEpisodeOrderingInformation<TSeries, TEpisode>? PreferredPlaceOf<TSeries, TEpisode>(TEpisode episode)
        where TSeries : class, ISeries
        where TEpisode : class, IEpisode
        => PlacesOf<TSeries, TEpisode>(episode).FirstOrDefault(place => place.IsPreferred);

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
}
