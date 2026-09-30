namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   A plugin's resolver for the entries of some sources and kinds, such as
///   a kind the plugin registered, or its own source's series read from its
///   own database. <see cref="Services.IMetadataService.GetEntry(MetadataGuid)"/>
///   asks it before the metadata stores, for IDs in its <see cref="Scope"/>.
/// </summary>
/// <remarks>
///   Each pair is taken by one resolver, the first in plugin load order; a
///   pair on a core source (<c>shoko</c>, <c>user</c>, <c>generated</c>,
///   <c>anidb</c> or <c>tmdb</c>), or one another resolver already took, is
///   refused and logged, and the resolver keeps the rest.
/// </remarks>
public interface IMetadataResolver
{
    /// <summary>
    ///   The name of the resolver, typically matching the plugin name. Used in
    ///   the logs.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   The sources and kinds of the entries the resolver resolves. Read
    ///   once, at registration, so return a constant.
    /// </summary>
    MetadataEntityScope Scope { get; }

    /// <summary>
    ///   Resolves an entry from its ID, from what the plugin already holds.
    ///   Called on every lookup, so it should not reach out to a remote
    ///   service.
    /// </summary>
    /// <param name="id">
    ///   The ID of the entry, always of a pair the resolver took from its
    ///   <see cref="Scope"/>.
    /// </param>
    /// <returns>
    ///   The entry, or <see langword="null"/> to leave the ID to the metadata
    ///   stores.
    /// </returns>
    IMetadata? GetEntry(MetadataGuid id);
}
