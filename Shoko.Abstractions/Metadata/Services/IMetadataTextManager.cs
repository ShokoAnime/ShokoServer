using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Text;
using Shoko.Abstractions.Metadata.Text.Options;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
///   Where the titles and overviews of every entry are kept, chosen and
///   gathered, for every source, and where users and plugins change them.
/// </summary>
/// <remarks>
///   Every stored text belongs to one entry, named by its
///   <see cref="MetadataGuid"/>, and to the source that wrote it. A provider
///   writes its own entries' texts, a plugin may add its own to any entry, and
///   the <c>user</c> source holds what a person picked or typed. A source's own
///   default, kept on the entry's row, is read alongside. The rules are in the
///   services README.
/// </remarks>
public interface IMetadataTextManager
{
    #region Events

    /// <summary>
    ///   Dispatched when a text is added through <see cref="AddText"/> or a
    ///   pick made with <see cref="SetPreferredTitle"/> or
    ///   <see cref="SetPreferredOverview"/>.
    /// </summary>
    event EventHandler<TextEventArgs>? TextAdded;

    /// <summary>
    ///   Dispatched when a text is changed through <see cref="UpdateText"/>,
    ///   <see cref="EnableText"/> or a change of preference.
    /// </summary>
    event EventHandler<TextEventArgs>? TextUpdated;

    /// <summary>
    ///   Dispatched when a text is removed through <see cref="RemoveText"/>,
    ///   or a pick goes because its preference was unset.
    /// </summary>
    event EventHandler<TextEventArgs>? TextRemoved;

    /// <summary>
    ///   Dispatched once per entry and write when the entry's stored texts
    ///   changed, whoever wrote them, a provider's refresh included.
    /// </summary>
    event EventHandler<EntityTextsChangedEventArgs>? EntityTextsChanged;

    #endregion

    #region Reading

    /// <summary>
    ///   Every title an entry has.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The titles, as the entry lists them, the user's override first when there is one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<ITitle> GetTitles(IWithTitles entry);

    /// <summary>
    ///   The title the user would want for an entry, from the configured
    ///   language and source orders.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The title, or <c>null</c> when none qualifies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    ITitle? GetPreferredTitle(IWithTitles entry);

    /// <summary>
    ///   Every overview an entry has.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The overviews, as the entry lists them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<IText> GetOverviews(IWithOverviews entry);

    /// <summary>
    ///   The overview the user would want for an entry, from the
    ///   configured language and source orders.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The overview, or <c>null</c> when none qualifies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IText? GetPreferredOverview(IWithOverviews entry);

    /// <summary>
    ///   Picks the title a user would want out of any set of candidates, the
    ///   same way the core picks for its own entries.
    /// </summary>
    /// <remarks>
    ///   A candidate a user prefers overall wins outright, and one preferred
    ///   for its language wins once the language order reaches it. For
    ///   episodes a real title in any preferred language beats a generic one
    ///   such as <c>Episode 5</c>. Disabled candidates, and those from a
    ///   source the user did not rank, are skipped.
    /// </remarks>
    /// <param name="titles">The candidates, in any order.</param>
    /// <param name="entityType">
    ///   Which ordering to walk. Episodes have their own; anything else,
    ///   <c>null</c> included, is ranked as a series.
    /// </param>
    /// <returns>The preferred title, or <c>null</c> when none qualifies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="titles"/> is <c>null</c>.</exception>
    ITitle? ChoosePreferredTitle(IEnumerable<ITitle> titles, MetadataEntityType? entityType = null);

    /// <summary>
    ///   Picks the title a user would want out of any set of candidates, ranking
    ///   the sources in the order given rather than the configured one.
    /// </summary>
    /// <param name="titles">The candidates, in any order.</param>
    /// <param name="entityType">
    ///   Which language order to walk. Episodes have their own; anything else,
    ///   <c>null</c> included, is ranked as a series.
    /// </param>
    /// <param name="sourceOrder">
    ///   The sources to read, best first. Candidates from any other source are
    ///   only chosen through a user's preference.
    /// </param>
    /// <returns>The preferred title, or <c>null</c> when none qualifies.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="titles"/> or <paramref name="sourceOrder"/> is <c>null</c>.
    /// </exception>
    ITitle? ChoosePreferredTitle(IEnumerable<ITitle> titles, MetadataEntityType? entityType, IReadOnlyList<MetadataSource> sourceOrder);

    /// <summary>
    ///   Picks the overview a user would want out of any set of
    ///   candidates, the same way the core picks for its own entries.
    /// </summary>
    /// <remarks>
    ///   A candidate a user prefers overall wins outright, and one preferred
    ///   for its language wins once the language order reaches it. Disabled
    ///   candidates, and those from a source the user did not rank, are
    ///   skipped.
    /// </remarks>
    /// <param name="overviews">The candidates, in any order.</param>
    /// <returns>The preferred overview, or <c>null</c> when none qualifies.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="overviews"/> is <c>null</c>.</exception>
    IText? ChoosePreferredOverview(IEnumerable<IText> overviews);

    /// <summary>
    ///   The languages the settings rank for one kind of text, best first,
    ///   for a provider choosing which of its translations to store.
    /// </summary>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="entityType">
    ///   Which title order to read. Episodes have their own; anything else,
    ///   <c>null</c> included, reads the series order. Ignored for overviews.
    /// </param>
    /// <returns>
    ///   The languages, <see cref="TitleLanguage.Main"/> included where the
    ///   settings place it.
    /// </returns>
    IReadOnlyList<TitleLanguage> GetLanguageOrder(TextKind kind, MetadataEntityType? entityType = null);

    #endregion

    #region Reading by Entry

    /// <summary>
    ///   The titles kept for an entry: the source's own default from the
    ///   entry's row, then every stored title, whoever wrote it.
    /// </summary>
    /// <param name="entityID">The entry, which may be any source's.</param>
    /// <param name="options">Optional. Which titles to list; enabled ones with the default when left out.</param>
    /// <returns>The titles, the stored ones by source and then in the order each source gave them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    IReadOnlyList<ITitle> GetTitles(MetadataGuid entityID, TextFilteringOptions? options = null);

    /// <summary>
    ///   The overviews kept for an entry: the source's own default from the
    ///   entry's row, then every stored overview, whoever wrote it.
    /// </summary>
    /// <param name="entityID">The entry, which may be any source's.</param>
    /// <param name="options">Optional. Which overviews to list; enabled ones with the default when left out.</param>
    /// <returns>The overviews, the stored ones by source and then in the order each source gave them.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    IReadOnlyList<IText> GetOverviews(MetadataGuid entityID, TextFilteringOptions? options = null);

    /// <summary>
    ///   The title the entry's own source calls it by.
    /// </summary>
    /// <remarks>
    ///   A plugin source's entry with no default on its row and no main title
    ///   gets a synthesized one, never stored: an episode's or season's generic
    ///   name, or its source, kind and ID, such as <c>TMDB Series 46195</c>.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <returns>
    ///   The default on the entry's row, else the source's first stored main
    ///   title, else the synthesized one, or <c>null</c> when there is none.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    ITitle? GetDefaultTitle(MetadataGuid entityID);

    /// <summary>
    ///   The overview the entry's own source gives it.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <returns>
    ///   The default on the entry's row, else the source's first stored
    ///   overview, or <c>null</c> when there is none.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    IText? GetDefaultOverview(MetadataGuid entityID);

    /// <summary>
    ///   The title the user would want for an entry, chosen from the titles
    ///   kept for it.
    /// </summary>
    /// <remarks>
    ///   A user's pick wins. Then every real title in a preferred language,
    ///   the entry's own source read whatever the source order says. With
    ///   none, the default is used, which may be a synthesized one such as
    ///   <c>Episode 5</c>, <c>Season 2</c> or <c>Specials</c>.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <returns>The title, or <c>null</c> when the entry has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    ITitle? GetPreferredTitle(MetadataGuid entityID);

    /// <summary>
    ///   The overview the user would want for an entry, chosen from the
    ///   overviews kept for it.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <returns>The overview, the default one when none is in a preferred language, or <c>null</c> when there is none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    IText? GetPreferredOverview(MetadataGuid entityID);

    /// <summary>
    ///   Looks up a stored title by its ID.
    /// </summary>
    /// <param name="titleID">The title's <see cref="IText.ID"/>.</param>
    /// <returns>The title, or <c>null</c> when none has that ID.</returns>
    ITitle? GetTitleByID(int titleID);

    /// <summary>
    ///   Looks up a stored overview by its ID.
    /// </summary>
    /// <param name="overviewID">The overview's <see cref="IText.ID"/>.</param>
    /// <returns>The overview, or <c>null</c> when none has that ID.</returns>
    IText? GetOverviewByID(int overviewID);

    /// <summary>
    ///   Every stored text of one kind, on every entry.
    /// </summary>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="options">Optional. Which texts to list; the default is never included here.</param>
    /// <returns>The texts, entry by entry.</returns>
    IEnumerable<IText> GetAllTexts(TextKind kind, TextFilteringOptions? options = null);

    #endregion

    #region Source Writes

    /// <summary>
    ///   The titles other sources added to an entry.
    /// </summary>
    /// <param name="entry">The entry, which may be any source's.</param>
    /// <param name="source">Optional. One source, or every source when left out.</param>
    /// <returns>The titles from sources other than the entry's own and <c>user</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<ITitle> GetContributedTitles(IMetadata entry, MetadataSource? source = null);

    /// <summary>
    ///   The overviews other sources added to an entry.
    /// </summary>
    /// <param name="entry">The entry, which may be any source's.</param>
    /// <param name="source">Optional. One source, or every source when left out.</param>
    /// <returns>The overviews from sources other than the entry's own and <c>user</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    IReadOnlyList<IText> GetContributedOverviews(IMetadata entry, MetadataSource? source = null);

    /// <summary>
    ///   Sets a source's titles on an entry, replacing the ones it gave before.
    /// </summary>
    /// <remarks>
    ///   A title that stays keeps its ID and whatever a user set on it, and a
    ///   title whose value changed in place keeps them too. On a Shoko entry
    ///   the titles are read ahead of the source's linked entry.
    /// </remarks>
    /// <param name="entry">The entry, which may be any source's.</param>
    /// <param name="source">The source the titles are from: the entry's own, or any other.</param>
    /// <param name="titles">
    ///   The titles, in order. Empty removes the source's titles. Generic
    ///   titles carrying the episode's or season's own number, such as
    ///   <c>Episode 5</c> on episode 5 or <c>Staffel 2</c> on season 2, are
    ///   left out.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/>, <paramref name="source"/> or
    ///   <paramref name="titles"/> is or holds <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    void SetTitles(IMetadata entry, MetadataSource source, IEnumerable<ITitle> titles);

    /// <summary>
    ///   Sets a source's titles on an entry by its ID, replacing the ones it
    ///   gave before.
    /// </summary>
    /// <param name="entityID">The entry, which may be any source's.</param>
    /// <param name="source">The source the titles are from: the entry's own, or any other.</param>
    /// <param name="titles">
    ///   The titles, in order. Empty removes the source's titles. Generic
    ///   titles carrying the episode's or season's own number are left out.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entityID"/>, <paramref name="source"/> or
    ///   <paramref name="titles"/> is or holds <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    void SetTitles(MetadataGuid entityID, MetadataSource source, IEnumerable<ITitle> titles);

    /// <summary>
    ///   Sets a source's overviews on an entry, replacing the ones it gave
    ///   before.
    /// </summary>
    /// <remarks>
    ///   An overview that stays keeps its ID and whatever a user set on it, and
    ///   one whose value changed in place keeps them too. On a Shoko entry the
    ///   overviews are read ahead of the source's linked entry.
    /// </remarks>
    /// <param name="entry">The entry, which may be any source's.</param>
    /// <param name="source">The source the overviews are from: the entry's own, or any other.</param>
    /// <param name="overviews">The overviews, in order. Empty removes the source's overviews.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/>, <paramref name="source"/> or
    ///   <paramref name="overviews"/> is or holds <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    void SetOverviews(IMetadata entry, MetadataSource source, IEnumerable<IText> overviews);

    /// <summary>
    ///   Sets a source's overviews on an entry by its ID, replacing the ones
    ///   it gave before.
    /// </summary>
    /// <param name="entityID">The entry, which may be any source's.</param>
    /// <param name="source">The source the overviews are from: the entry's own, or any other.</param>
    /// <param name="overviews">The overviews, in order. Empty removes the source's overviews.</param>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entityID"/>, <paramref name="source"/> or
    ///   <paramref name="overviews"/> is or holds <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    void SetOverviews(MetadataGuid entityID, MetadataSource source, IEnumerable<IText> overviews);

    /// <summary>
    ///   Removes the stored texts of an entry.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="source">Optional. Only this source's texts, or every source's when left out.</param>
    /// <returns>How many texts were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    int RemoveTexts(MetadataGuid entityID, MetadataSource? source = null);

    /// <summary>
    ///   Removes everything a source added to other sources' entries, on every
    ///   entry. Its own entries keep their texts.
    /// </summary>
    /// <param name="source">The source to forget.</param>
    /// <returns>How many entries lost text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <c>null</c>.</exception>
    int RemoveContributions(MetadataSource source);

    #endregion

    #region User Writes

    /// <summary>
    ///   Stores a new text on an entry, from a user unless another source is
    ///   named.
    /// </summary>
    /// <param name="entityID">The entry, which may be any source's.</param>
    /// <param name="data">The text.</param>
    /// <returns>The stored text, with its ID.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> or <paramref name="data"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The value is blank, a language, country or script code is too long,
    ///   or the title is a generic one carrying the episode's or season's own
    ///   number, which is synthesized rather than stored.
    /// </exception>
    IText AddText(MetadataGuid entityID, TextData data);

    /// <summary>
    ///   Changes a stored text in part.
    /// </summary>
    /// <param name="text">The stored text.</param>
    /// <param name="data">What to change; anything left <c>null</c> stays.</param>
    /// <returns>The text as it is now.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="data"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="text"/> is not stored, or a new value is blank or a
    ///   generic title carrying the episode's or season's own number.
    /// </exception>
    /// <exception cref="InvalidOperationException">A new value was given for a text that is not the user's own.</exception>
    IText UpdateText(IText text, TextUpdateData data);

    /// <summary>
    ///   Enables or disables a stored text. A disabled text is neither listed
    ///   by default nor chosen, and stays disabled across refreshes.
    /// </summary>
    /// <param name="text">The stored text.</param>
    /// <param name="isEnabled">Whether it may be listed and chosen.</param>
    /// <returns>The text as it is now.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="text"/> is not stored.</exception>
    IText EnableText(IText text, bool isEnabled);

    /// <summary>
    ///   Makes a title the one an entry is called by, overall or for the
    ///   title's own language.
    /// </summary>
    /// <remarks>
    ///   A stored title of the entry itself is flagged, and enabled. Any other
    ///   title, such as the entry's own default, another entry's title or a
    ///   new value, is stored on the entry as a <c>user</c> pick holding a copy
    ///   of it, which follows the picked text while it is stored.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <param name="title">The title to prefer.</param>
    /// <param name="forLanguageOnly">
    ///   <c>true</c> to prefer it only once the language order reaches its
    ///   language; <c>false</c> to prefer it over everything.
    /// </param>
    /// <returns>The stored title that now carries the preference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> or <paramref name="title"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   The title's value is blank, or a copy would store a generic title
    ///   carrying the episode's or season's own number.
    /// </exception>
    ITitle SetPreferredTitle(MetadataGuid entityID, ITitle title, bool forLanguageOnly = false);

    /// <summary>
    ///   Makes an overview the one an entry is described by, overall or for
    ///   the overview's own language.
    /// </summary>
    /// <remarks>
    ///   A stored overview of the entry itself is flagged, and enabled. Any
    ///   other overview is stored on the entry as a <c>user</c> pick holding a
    ///   copy of it, which follows the picked text while it is stored.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <param name="overview">The overview to prefer.</param>
    /// <param name="forLanguageOnly">
    ///   <c>true</c> to prefer it only once the language order reaches its
    ///   language; <c>false</c> to prefer it over everything.
    /// </param>
    /// <returns>The stored overview that now carries the preference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> or <paramref name="overview"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The overview's value is blank.</exception>
    IText SetPreferredOverview(MetadataGuid entityID, IText overview, bool forLanguageOnly = false);

    /// <summary>
    ///   Takes a user's preference off a stored text. A pick of another text
    ///   has no other use, so it is removed.
    /// </summary>
    /// <param name="text">The stored text.</param>
    /// <returns><c>true</c> when the text carried a preference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    bool UnsetPreferredText(IText text);

    /// <summary>
    ///   Takes every preference off an entry's stored texts, removing the
    ///   picks of other texts.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Optional. Only titles or only overviews; both when left out.</param>
    /// <returns>How many texts carried a preference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    int UnsetAllPreferredTexts(MetadataGuid entityID, TextKind? kind = null);

    /// <summary>
    ///   Removes a stored text, and every pick of it.
    /// </summary>
    /// <param name="text">The stored text.</param>
    /// <returns><c>true</c> when it was stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <c>null</c>.</exception>
    bool RemoveText(IText text);

    #endregion

    #region Maintenance

    /// <summary>
    ///   The entries that have stored texts but can no longer be found.
    /// </summary>
    /// <remarks>
    ///   An entry only a plugin's resolver finds counts as missing while that
    ///   plugin is not loaded.
    /// </remarks>
    /// <param name="entitySource">Optional. Only entries of this source.</param>
    /// <returns>The missing entries.</returns>
    IReadOnlyList<MetadataGuid> GetOrphanedEntries(MetadataSource? entitySource = null);

    /// <summary>
    ///   Removes the texts of every entry that can no longer be found.
    /// </summary>
    /// <param name="entitySource">Optional. Only entries of this source.</param>
    /// <returns>How many texts were removed.</returns>
    int PurgeOrphanedTexts(MetadataSource? entitySource = null);

    /// <summary>
    ///   Drops what was worked out for an entry, and for every entry worked
    ///   out from it, after its own row changed outside the manager, such as
    ///   the default kept on it.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    void Invalidate(MetadataGuid entityID);

    #endregion
}
