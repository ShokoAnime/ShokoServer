using System;
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   Keeps every source's characters and creators, and who is credited on
///   which entry, so a provider does not have to.
/// </summary>
/// <remarks>
///   A creator or character is keyed by its own <see cref="MetadataGuid"/>,
///   a credit by the entry it is on. Only plugin sources can be written. No
///   write removes a person: one nothing credits any more is purged by
///   <see cref="RemoveOrphaned"/> once orphaned long enough.
/// </remarks>
public interface IMetadataPeopleStore
{
    #region Reading

    /// <summary>
    ///   Looks up a creator.
    /// </summary>
    /// <param name="id">The creator, e.g. <c>anilist://creator/95</c>.</param>
    /// <returns>The creator, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    ICreator? GetCreator(MetadataGuid id);

    /// <summary>
    ///   Looks up a character.
    /// </summary>
    /// <param name="id">The character, e.g. <c>anilist://character/40</c>.</param>
    /// <returns>The character, or <c>null</c> when it is not stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="id"/> is <c>null</c>.</exception>
    ICharacter? GetCharacter(MetadataGuid id);

    /// <summary>
    ///   The cast credited on an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The credits, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<ICast> GetCast(MetadataGuid entry);

    /// <summary>
    ///   The crew credited on an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The credits, in the order they were given.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<ICrew> GetCrew(MetadataGuid entry);

    #endregion

    #region Writing

    /// <summary>
    ///   Adds creators, or updates the ones already stored, with their
    ///   alternative names.
    /// </summary>
    /// <param name="creators">The creators.</param>
    /// <exception cref="ArgumentNullException"><paramref name="creators"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   A creator's ID does not name a creator or is on a source the core
    ///   keeps itself, or a language code is longer than 32 characters.
    /// </exception>
    void SaveCreators(IEnumerable<MetadataCreatorData> creators);

    /// <summary>
    ///   Adds characters, or updates the ones already stored, with their
    ///   alternative names.
    /// </summary>
    /// <param name="characters">The characters.</param>
    /// <exception cref="ArgumentNullException"><paramref name="characters"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   A character's ID does not name a character or is on a source the core
    ///   keeps itself, or a language code is longer than 32 characters.
    /// </exception>
    void SaveCharacters(IEnumerable<MetadataCharacterData> characters);

    /// <summary>
    ///   Makes an entry's cast exactly the credits given, in that order. A
    ///   credit is known by its character, creator and language: one already
    ///   stored is updated, a new one added, and a missing one removed.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="cast">The cast credits. Empty removes the whole cast.</param>
    /// <returns>How many credits were added, changed or removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> or <paramref name="cast"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The entry is on a source the core keeps itself, a credit names a
    ///   creator or character that is not stored or is on another source, or a
    ///   language code is longer than 32 characters.
    /// </exception>
    int SetCast(MetadataGuid entry, IEnumerable<MetadataCastData> cast);

    /// <summary>
    ///   Makes an entry's crew exactly the credits given, in that order. A
    ///   credit is known by its creator and job: one already stored is
    ///   updated, a new one added, and a missing one removed.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="crew">The crew credits. Empty removes the whole crew.</param>
    /// <returns>How many credits were added, changed or removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> or <paramref name="crew"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The entry is on a source the core keeps itself, a credit names a
    ///   creator that is not stored or is on another source, or a language
    ///   code is longer than 32 characters.
    /// </exception>
    int SetCrew(MetadataGuid entry, IEnumerable<MetadataCrewData> crew);

    /// <summary>
    ///   Removes an entry's whole cast.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many credits were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on a source the core keeps itself.</exception>
    int RemoveCast(MetadataGuid entry);

    /// <summary>
    ///   Removes an entry's whole crew.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>How many credits were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The entry is on a source the core keeps itself.</exception>
    int RemoveCrew(MetadataGuid entry);

    /// <summary>
    ///   Removes a source's creators and characters that nothing has credited
    ///   since before a cutoff, with their alternative names, but not their
    ///   image links.
    /// </summary>
    /// <remarks>
    ///   A person is stamped when it loses its last credit (a character, its
    ///   last cast credit) and unstamped when credited again; one found
    ///   uncredited without a stamp is stamped now rather than removed. The
    ///   core runs this daily and also unlinks the images, so you rarely need it.
    /// </remarks>
    /// <param name="source">The source.</param>
    /// <param name="orphanedBefore">
    ///   Remove only the people orphaned before this time. Saving an
    ///   uncredited person stamps it anew, so keep this a day or more in the
    ///   past to spare one saved for a credit still to come.
    /// </param>
    /// <returns>The creators and characters removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The source is one the core keeps itself.</exception>
    IReadOnlyList<MetadataGuid> RemoveOrphaned(MetadataSource source, DateTime orphanedBefore);

    #endregion
}
