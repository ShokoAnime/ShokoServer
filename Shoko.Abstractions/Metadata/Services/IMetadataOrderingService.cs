using System;
using System.Collections.Generic;
using System.Threading;
using Shoko.Abstractions.Metadata.Storage;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Keeps the orderings of every source's series, which ordering is chosen
///   for each series, and which episodes users hid.
/// </summary>
/// <remarks>
///   Every series has an unstored default ordering from its own seasons: its
///   ID is the series' ID for a core source (<c>anidb://ordering/1</c>), or
///   <c>default/</c> plus the series' ID for any other
///   (<c>tmdb://ordering/default/1</c>). Global orderings are
///   saved whole by a plugin under its source; local ones are users', under
///   <c>user</c>. Purging a series removes every ordering of it.
/// </remarks>
public interface IMetadataOrderingService
{
    #region Reading

    /// <summary>
    ///   Every ordering of a series: the default one first, then the stored
    ///   ones, global before local, oldest first.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The orderings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is <c>null</c>.</exception>
    IReadOnlyList<IOrdering> GetOrderings(ISeries series);

    /// <summary>
    ///   Every ordering of a series, looked up by its identifier.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The orderings, or none when the series is not available.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seriesID"/> is <c>null</c>.</exception>
    IReadOnlyList<IOrdering> GetOrderings(MetadataGuid seriesID);

    /// <summary>
    ///   Looks up an ordering: a stored one, or a default one whose series is
    ///   available.
    /// </summary>
    /// <param name="orderingID">The ordering, e.g. <c>user://ordering/1f0c…</c>.</param>
    /// <returns>The ordering, or <c>null</c> when there is none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="orderingID"/> is <c>null</c>.</exception>
    IOrdering? GetOrdering(MetadataGuid orderingID);

    /// <summary>
    ///   Every ordering stored under a source: a plugin's global orderings, or
    ///   the users' own under <c>user</c>.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The orderings, oldest first.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    IReadOnlyList<IOrdering> GetStoredOrderings(MetadataSource source);

    /// <summary>
    ///   The default ordering of a series, made from its own seasons.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The default ordering.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is <c>null</c>.</exception>
    IOrdering GetDefaultOrdering(ISeries series);

    /// <summary>
    ///   Every place an episode has in its series' orderings, its place in the
    ///   default ordering first.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The places.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="episode"/> is <c>null</c>.</exception>
    IReadOnlyList<IEpisodeOrderingInformation> GetEpisodeOrderings(IEpisode episode);

    #endregion

    #region Global Orderings

    /// <summary>
    ///   Stores a global ordering whole under its own source, replacing what
    ///   was stored for it, groups and networks and all.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    /// <returns>The stored ordering.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ordering"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ordering is on a source the core keeps itself, an ID names the
    ///   wrong kind or source, an ID is reserved or taken by another ordering,
    ///   the type is kept by the core, the series is not available, a group
    ///   holds an episode that is not the series', more than one group is
    ///   special, or a network is not stored or is on another source.
    /// </exception>
    IOrdering SaveOrdering(MetadataOrderingData ordering);

    /// <summary>
    ///   Removes a global ordering with its groups and networks, and the
    ///   choice of it for its series.
    /// </summary>
    /// <param name="orderingID">The ordering.</param>
    /// <returns><c>true</c> if it was stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="orderingID"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name an ordering, or is on a source the core keeps.
    /// </exception>
    bool RemoveOrdering(MetadataGuid orderingID);

    /// <summary>
    ///   Removes every global ordering of a source, with its groups, networks
    ///   and the choices of them. The users' own orderings are kept.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="progress">Told how far the removal is, from 0 to 100.</param>
    /// <param name="cancellationToken">Stops the removal between two orderings.</param>
    /// <returns>How many orderings were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The source keeps no global orderings.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    int RemoveOrderings(MetadataSource source, IProgress<decimal>? progress = null, CancellationToken cancellationToken = default);

    #endregion

    #region Local Orderings

    /// <summary>
    ///   Makes a user's ordering of a series, under the <c>user</c> source,
    ///   linked to its networks, with a stub for each network not stored yet.
    /// </summary>
    /// <param name="ordering">The ordering.</param>
    /// <returns>The new ordering, with the IDs it was given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ordering"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   A group names an ID, the series is not available, a group holds an
    ///   episode that is not the series', more than one group is special, or
    ///   a network names another kind or a source this server does not know.
    /// </exception>
    IOrdering CreateLocalOrdering(MetadataLocalOrderingData ordering);

    /// <summary>
    ///   Replaces a user's ordering whole. A group naming one of the
    ///   ordering's groups keeps its ID; the others get new ones. The
    ///   networks are replaced when given, with a stub for each one not
    ///   stored yet, and kept when left out.
    /// </summary>
    /// <param name="orderingID">The ordering, under the <c>user</c> source.</param>
    /// <param name="ordering">What it is to be.</param>
    /// <returns>The updated ordering, or <c>null</c> when there is none to update.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="orderingID"/> or <paramref name="ordering"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name a user's ordering, the series is another one, a
    ///   group names an ID the ordering does not have, a group holds an
    ///   episode that is not the series', more than one group is special, or
    ///   a network names another kind or a source this server does not know.
    /// </exception>
    IOrdering? UpdateLocalOrdering(MetadataGuid orderingID, MetadataLocalOrderingData ordering);

    /// <summary>
    ///   Removes a user's ordering with its groups, and the choice of it for
    ///   its series.
    /// </summary>
    /// <param name="orderingID">The ordering, under the <c>user</c> source.</param>
    /// <returns><c>true</c> if it was stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="orderingID"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The ID does not name a user's ordering.</exception>
    bool DeleteLocalOrdering(MetadataGuid orderingID);

    #endregion

    #region Preferred Ordering

    /// <summary>
    ///   The ordering chosen for a series, or its default ordering when none
    ///   is, or the chosen one is gone.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The ordering.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="series"/> is <c>null</c>.</exception>
    IOrdering GetPreferredOrdering(ISeries series);

    /// <summary>
    ///   Chooses an ordering for a series, on any source, kept on the
    ///   series' own row.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <param name="orderingID">One of the series' orderings, or <c>null</c> for its default one.</param>
    /// <returns><c>true</c> if the choice changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="seriesID"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name a series, the series is not available or only a
    ///   resolver serves it, or the ordering is not one of its orderings.
    /// </exception>
    bool SetPreferredOrdering(MetadataGuid seriesID, MetadataGuid? orderingID);

    #endregion

    #region Hidden Episodes

    /// <summary>
    ///   Whether a user hid an episode, of any source.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns><c>true</c> if it is hidden.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="episodeID"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The ID does not name an episode.</exception>
    bool IsEpisodeHidden(MetadataGuid episodeID);

    /// <summary>
    ///   Hides or shows an episode, of any source, on the episode's own row.
    ///   A Shoko episode's flag is set with the series and group stats. Only
    ///   an available episode can be hidden; any can be shown.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <param name="hidden">Whether to hide it.</param>
    /// <returns><c>true</c> if the state changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="episodeID"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The ID does not name an episode, or it is hidden while the episode
    ///   is not available or only a resolver serves it, or it names a Shoko
    ///   episode that does not exist.
    /// </exception>
    bool SetEpisodeHidden(MetadataGuid episodeID, bool hidden);

    #endregion
}
