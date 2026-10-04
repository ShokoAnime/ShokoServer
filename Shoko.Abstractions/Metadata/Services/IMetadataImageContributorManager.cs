using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Plugin;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Registers the image contributors and holds which sources and kinds an
///   admin left each one on for.
/// </summary>
/// <remarks>
///   A change takes effect at once, without a restart: the next image refresh
///   of an entry asks the contributors enabled for it then, and the links a
///   contributor added on a pair turned off are removed.
/// </remarks>
public interface IMetadataImageContributorManager
{
    #region Contributors

    /// <summary>
    ///   The image contributors registered with the service, in plugin load
    ///   order.
    /// </summary>
    IReadOnlyList<MetadataImageContributorInfo> ImageContributors { get; }

    /// <summary>
    ///   Looks up a contributor by its stable ID.
    /// </summary>
    /// <param name="contributorID">The contributor's ID.</param>
    /// <returns>
    ///   The info, or <c>null</c> if nothing goes by that ID.
    /// </returns>
    MetadataImageContributorInfo? GetImageContributorInfo(Guid contributorID);

    /// <summary>
    ///   Looks up a contributor's info from the contributor itself.
    /// </summary>
    /// <param name="contributor">The contributor.</param>
    /// <returns>The info.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="contributor"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when the contributor was never registered.
    /// </exception>
    MetadataImageContributorInfo GetImageContributorInfo(IMetadataImageContributor contributor);

    /// <summary>
    ///   The contributors a plugin registered.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <returns>One <see cref="MetadataImageContributorInfo"/> per contributor.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="plugin"/> is <c>null</c>.
    /// </exception>
    IReadOnlyList<MetadataImageContributorInfo> GetImageContributorInfo(IPlugin plugin);

    /// <summary>
    ///   The contributors enabled for an entity's source and kind.
    /// </summary>
    /// <param name="entityID">The entity.</param>
    /// <returns>The contributors, in plugin load order.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="entityID"/> is <c>null</c>.
    /// </exception>
    IReadOnlyList<MetadataImageContributorInfo> GetImageContributorsFor(MetadataGuid entityID);

    #endregion

    #region Settings

    /// <summary>
    ///   Turns a contributor on for exactly the given pairs, and off for every
    ///   other pair it can add images for.
    /// </summary>
    /// <remarks>
    ///   Saved with the other metadata settings. The links the contributor
    ///   added to entities of a pair turned off are removed in a queued job.
    /// </remarks>
    /// <param name="contributorID">The contributor's ID.</param>
    /// <param name="enabled">
    ///   The pairs to leave on, each one of its
    ///   <see cref="MetadataImageContributorInfo.AvailableScope"/>; an empty
    ///   scope turns it off.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="enabled"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   Thrown when no contributor goes by <paramref name="contributorID"/>,
    ///   or a pair is not one it can add images for.
    /// </exception>
    void SetImageContributorEnabled(Guid contributorID, MetadataEntityScope enabled);

    #endregion
}
