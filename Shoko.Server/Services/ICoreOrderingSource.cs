using System.Collections.Generic;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   A source the core keeps whose own orderings of its series live in the
///   source's own tables rather than the stored ordering tables. The
///   ordering service reads them from here, next to the stored ones, and asks
///   it for the source's typed view of a default ordering.
/// </summary>
public interface ICoreOrderingSource
{
    /// <summary>
    ///   The source whose orderings these are.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    ///   The source's own orderings of one of its series, leaving out the
    ///   default ordering.
    /// </summary>
    /// <param name="series">The series, on <see cref="Source"/>.</param>
    /// <returns>The orderings.</returns>
    IReadOnlyList<IOrdering> GetOrderings(ISeries series);

    /// <summary>
    ///   Looks up one of the source's own orderings.
    /// </summary>
    /// <param name="orderingID">The ordering, on <see cref="Source"/>.</param>
    /// <returns>The ordering, or <c>null</c> when the source has none by that ID.</returns>
    IOrdering? GetOrdering(MetadataGuid orderingID);

    /// <summary>
    ///   The places an episode has in the source's own orderings.
    /// </summary>
    /// <param name="episode">The episode, on <see cref="Source"/>.</param>
    /// <returns>The places, leaving out its place in the default ordering.</returns>
    IReadOnlyList<IEpisodeOrderingInformation> GetEpisodeOrderings(IEpisode episode);

    /// <summary>
    ///   The source's own view of a series' default ordering, which must keep
    ///   the ID <see cref="MetadataOrderingService.DefaultOrderingID"/> gives.
    /// </summary>
    /// <param name="series">The series, on <see cref="Source"/>.</param>
    /// <param name="service">The ordering service, which knows the choice and the hidden episodes.</param>
    /// <returns>The default ordering, or <c>null</c> to use the generic one.</returns>
    IOrdering? GetDefaultOrdering(ISeries series, MetadataOrderingService service);

    /// <summary>
    ///   The source's own view of an episode's place in the default ordering
    ///   of its series.
    /// </summary>
    /// <param name="episode">The episode, on <see cref="Source"/>.</param>
    /// <param name="series">The episode's series, if it is available.</param>
    /// <param name="service">The ordering service, which knows the choice.</param>
    /// <returns>The place, or <c>null</c> to use the generic one.</returns>
    IEpisodeOrderingInformation? GetDefaultEpisodeOrdering(IEpisode episode, ISeries? series, MetadataOrderingService service);
}
