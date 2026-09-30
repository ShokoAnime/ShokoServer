using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Text;
using Shoko.Abstractions.Metadata.Text.Options;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps, chooses and gathers the titles and overviews of every entry, and
///   takes the writes of providers, plugins and users.
/// </summary>
/// <remarks>
///   Stored texts come from <see cref="MetadataTextStore"/>. Every model reads
///   its texts here, through <see cref="Models.TextAccess"/>, and what is
///   worked out is remembered in a <see cref="TextMemoTable"/> until a text,
///   row or link it was read from changes.
/// </remarks>
public class MetadataTextManager : IMetadataTextManager
{
    #region Fields

    /// <summary>
    ///   Bumped whenever the language settings change, which outdates every
    ///   choice worked out before.
    /// </summary>
    private static int _generation;

    /// <summary>
    ///   The entries whose text this thread is reading. A linked entry may
    ///   read the Shoko entry's text back while being read, and the second
    ///   pass answers with nothing rather than recursing.
    /// </summary>
    [ThreadStatic]
    private static HashSet<object>? _resolving;

    private readonly IMetadataService _metadataService;

    private readonly MetadataTextStore _textStore;

    private readonly ILogger<MetadataTextManager> _logger;

    private readonly AniDBTitleHelper? _titleHelper;

    /// <summary>
    ///   What was worked out per entry, and what it was worked out from.
    /// </summary>
    private readonly TextMemoTable _memos = new();

    #endregion

    #region Constructor

    /// <summary>
    ///   Creates the manager.
    /// </summary>
    /// <param name="metadataService">Resolves entries and what they are linked to.</param>
    /// <param name="textStore">Holds the stored texts.</param>
    /// <param name="logger">Logs a linked entry whose text could not be read.</param>
    /// <param name="titleHelper">Optional. The catalog of every anime AniDB has, which names a series whose anime is not stored.</param>
    public MetadataTextManager(IMetadataService metadataService, MetadataTextStore textStore, ILogger<MetadataTextManager> logger, AniDBTitleHelper? titleHelper = null)
    {
        _metadataService = metadataService;
        _textStore = textStore;
        _logger = logger;
        _titleHelper = titleHelper;
        if (textStore?.Cache is { } cache)
        {
            cache.Changed += OnTextsChanged;
            cache.Reloaded += (_, _) => _memos.ForgetAll();
        }
        _memos.Forgotten += OnForgotten;
    }

    #endregion

    #region Events

    /// <inheritdoc />
    public event EventHandler<TextEventArgs>? TextAdded;

    /// <inheritdoc />
    public event EventHandler<TextEventArgs>? TextUpdated;

    /// <inheritdoc />
    public event EventHandler<TextEventArgs>? TextRemoved;

    /// <inheritdoc />
    public event EventHandler<EntityTextsChangedEventArgs>? EntityTextsChanged;

    /// <summary>
    ///   Outdates every choice, after the language settings changed.
    /// </summary>
    internal static void OnLanguageSettingsChanged()
        => Interlocked.Increment(ref _generation);

    /// <summary>
    ///   The language settings' generation: every value worked out with an
    ///   older one is outdated.
    /// </summary>
    internal static int Generation
        => Volatile.Read(ref _generation);

    /// <summary>
    ///   Raised once per entry whose remembered texts were dropped, the entry
    ///   written itself and each entry worked out from it.
    /// </summary>
    internal event Action<MetadataGuid>? Forgotten;

    /// <summary>
    ///   Tells the listeners, and the series search, that an entry's texts
    ///   are to be read again.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    private void OnForgotten(MetadataGuid entityID)
    {
        if (entityID.Source == MetadataSource.Shoko && entityID.EntityType == MetadataEntityType.Series && entityID.TryGetNumericID<int>(out var seriesID))
            SeriesSearch.MarkStale(seriesID);
        Forgotten?.Invoke(entityID);
    }

    /// <summary>
    ///   Drops what was worked out for the entries a write changed, and tells
    ///   the listeners.
    /// </summary>
    /// <param name="sender">The text cache.</param>
    /// <param name="changes">What changed, per entry.</param>
    private void OnTextsChanged(object? sender, IReadOnlyList<TextEntityChange> changes)
    {
        foreach (var change in changes)
        {
            Forget(change.EntityID);
            EntityTextsChanged?.Invoke(this, new() { EntityID = change.EntityID, Kinds = change.Kinds, Sources = change.Sources });
        }
    }

    #endregion

    #region Reading

    /// <inheritdoc />
    public IReadOnlyList<ITitle> GetTitles(IWithTitles entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Titles;
    }

    /// <inheritdoc />
    public ITitle? GetPreferredTitle(IWithTitles entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.PreferredTitle;
    }

    /// <inheritdoc />
    public IReadOnlyList<IText> GetOverviews(IWithOverviews entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Overviews;
    }

    /// <inheritdoc />
    public IText? GetPreferredOverview(IWithOverviews entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.PreferredOverview;
    }

    /// <inheritdoc />
    public ITitle? ChoosePreferredTitle(IEnumerable<ITitle> titles, MetadataEntityType? entityType = null)
    {
        ArgumentNullException.ThrowIfNull(titles);

        var settings = ISettingsProvider.Instance.GetSettings();
        var episode = entityType == MetadataEntityType.Episode;
        return ChoosePreferredTitle(titles, entityType, episode ? settings.Language.EpisodeTitleSourceOrder : settings.Language.SeriesTitleSourceOrder);
    }

    /// <inheritdoc />
    public ITitle? ChoosePreferredTitle(IEnumerable<ITitle> titles, MetadataEntityType? entityType, IReadOnlyList<MetadataSource> sourceOrder)
    {
        ArgumentNullException.ThrowIfNull(titles);
        ArgumentNullException.ThrowIfNull(sourceOrder);

        if (titles is not IReadOnlyList<ITitle> candidates)
            candidates = [.. titles];
        if (candidates.Count is 0)
            return null;

        var episode = entityType == MetadataEntityType.Episode;
        return TextChooser.ChooseTitle(candidates, new(
            [.. (episode ? Languages.PreferredEpisodeNamingLanguages : Languages.PreferredNamingLanguages).Select(language => language.Language)],
            sourceOrder,
            ISettingsProvider.Instance.GetSettings().Language.UseSynonyms,
            episode
        ));
    }

    /// <inheritdoc />
    public IText? ChoosePreferredOverview(IEnumerable<IText> overviews)
    {
        ArgumentNullException.ThrowIfNull(overviews);

        if (overviews is not IReadOnlyList<IText> candidates)
            candidates = [.. overviews];
        if (candidates.Count is 0)
            return null;

        return TextChooser.ChooseOverview(
            candidates,
            [.. Languages.PreferredDescriptionNamingLanguages.Select(language => language.Language)],
            ISettingsProvider.Instance.GetSettings().Language.DescriptionSourceOrder,
            null
        );
    }

    #endregion

    #region Reading by Entry

    /// <inheritdoc />
    public IReadOnlyList<ITitle> GetTitles(MetadataGuid entityID, TextFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        options ??= new();
        if (OwnListOf(entityID) is IWithTitles own)
        {
            var titles = WithStored(own.Titles, _textStore.GetTitles(entityID, options.Source), entityID);
            return [.. WithOwnDefault(titles, (own as IInlineTextSource)?.InlineTitle, options.IncludeInlineDefault).Where(title => Matches(title, options))];
        }

        return StoredTitles(entityID, InlineOf(entityID), options);
    }

    /// <inheritdoc />
    public IReadOnlyList<IText> GetOverviews(MetadataGuid entityID, TextFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        options ??= new();
        if (options.TitleType is not null)
            return [];

        if (OwnListOf(entityID) is IWithOverviews own)
        {
            var overviews = WithStored(own.Overviews, _textStore.GetOverviews(entityID, options.Source), entityID);
            return [.. WithOwnDefault(overviews, (own as IInlineTextSource)?.InlineOverview, options.IncludeInlineDefault).Where(overview => Matches(overview, options))];
        }

        return StoredOverviews(entityID, InlineOf(entityID), options);
    }

    /// <inheritdoc />
    public ITitle? GetDefaultTitle(MetadataGuid entityID)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        if (OwnListOf(entityID) is IWithTitles own)
            return NonEmpty(own.DefaultTitle);

        return StoredDefaultTitle(entityID, InlineOf(entityID));
    }

    /// <inheritdoc />
    public IText? GetDefaultOverview(MetadataGuid entityID)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        if (OwnListOf(entityID) is IWithOverviews own)
            return NonEmpty(own.DefaultOverview);

        return StoredDefaultOverview(entityID, InlineOf(entityID));
    }

    /// <inheritdoc />
    public ITitle? GetPreferredTitle(MetadataGuid entityID)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        // The entry's own model chooses, and keeps its own cache of the
        // choice, so only a user's overall pick is looked up here.
        if (OwnListOf(entityID) is IWithTitles own)
            return OverallPick(entityID, TextKind.Title) as ITitle ?? own.PreferredTitle ?? NonEmpty(own.DefaultTitle) ?? Synthesized(entityID, own);

        var entry = ResolveQuietly(entityID);
        return ChooseStoredTitle(entityID, entry as IInlineTextSource)
            ?? StoredDefaultTitle(entityID, entry as IInlineTextSource)
            ?? Synthesized(entityID, entry as IWithTitles);
    }

    /// <inheritdoc />
    public IText? GetPreferredOverview(MetadataGuid entityID)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        if (OwnListOf(entityID) is IWithOverviews own)
            return OverallPick(entityID, TextKind.Overview) ?? own.PreferredOverview ?? NonEmpty(own.DefaultOverview);

        var inline = InlineOf(entityID);
        return ChooseStoredOverview(entityID, inline) ?? StoredDefaultOverview(entityID, inline);
    }

    /// <inheritdoc />
    public ITitle? GetTitleByID(int titleID)
        => _textStore.Cache.Find(TextKind.Title, titleID) is { } found ? (ITitle)_textStore.Cache.ToText(found.Entity, found.Row, found.Row.Value) : null;

    /// <inheritdoc />
    public IText? GetOverviewByID(int overviewID)
        => _textStore.Cache.Find(TextKind.Overview, overviewID) is { } found ? _textStore.Cache.ToText(found.Entity, found.Row, _textStore.Cache.ValueOf(found.Row)) : null;

    /// <inheritdoc />
    public IEnumerable<IText> GetAllTexts(TextKind kind, TextFilteringOptions? options = null)
    {
        options ??= new();
        foreach (var (entity, _) in _textStore.Cache.Enumerate())
        {
            if ((options.EntitySource is not null && entity.Source != options.EntitySource) ||
                (options.EntityType is not null && entity.EntityType != options.EntityType))
                continue;

            IEnumerable<IText> texts = kind is TextKind.Title ? _textStore.GetTitles(entity, options.Source) : _textStore.GetOverviews(entity, options.Source);
            foreach (var text in texts)
                if (Matches(text, options))
                    yield return text;
        }
    }

    /// <summary>
    ///   The entry behind an ID, when its own model still keeps and chooses
    ///   its texts, rather than the store.
    /// </summary>
    /// <remarks>
    ///   The texts of the core's sources are not in the store yet, so the
    ///   entry's own lists are read, with what was stored for it on top.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <returns>The entry, or <c>null</c> when the store keeps its texts or it cannot be found.</returns>
    private object? OwnListOf(MetadataGuid entityID)
    {
        if (!entityID.Source.IsCore)
            return null;

        try
        {
            return _metadataService.GetEntry(entityID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Looking up {Entry} for its texts threw and was skipped.", entityID);
            return null;
        }
    }

    /// <summary>
    ///   An entry's own list, then the texts stored for it that the list does
    ///   not hold already.
    /// </summary>
    /// <param name="own">The list the entry's model gives.</param>
    /// <param name="stored">The texts stored for the entry.</param>
    /// <param name="entityID">The entry.</param>
    /// <returns>The texts, each once.</returns>
    private static IEnumerable<T> WithStored<T>(IReadOnlyList<T>? own, IReadOnlyList<T> stored, MetadataGuid entityID) where T : class, IText
    {
        own ??= [];
        var held = own.Where(text => text.ID is not null && text.EntityID == entityID).Select(text => text.ID!.Value).ToHashSet();
        return own.Concat(stored.Where(text => text.ID is not { } id || !held.Contains(id)));
    }

    /// <summary>
    ///   An entry's own list with the defaults kept on rows left out, or with
    ///   the entry's own default first where the list leaves it out.
    /// </summary>
    /// <remarks>
    ///   TMDB leaves its English title out of the list when TMDB did not list
    ///   it, yet it is still a text to choose from. A Shoko entry's list also
    ///   holds the defaults on the rows of the entries it is linked to.
    /// </remarks>
    /// <param name="texts">The entry's own list, with what was stored for it.</param>
    /// <param name="inline">The default on the entry's own row, or <c>null</c> when it has none.</param>
    /// <param name="include">Whether to list the defaults kept on rows.</param>
    /// <returns>The texts.</returns>
    private static IEnumerable<T> WithOwnDefault<T>(IEnumerable<T> texts, T? inline, bool include) where T : class, IText
    {
        if (!include)
            return texts.Where(text => !text.IsInlineDefault);

        if (inline is null)
            return texts;

        var listed = texts.ToList();
        return listed.Any(text => IText.Equals(text, inline)) ? listed : listed.Prepend(inline);
    }

    /// <summary>
    ///   A text, unless it has no value.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or <c>null</c> when it is missing or empty.</returns>
    private static T? NonEmpty<T>(T? text) where T : class, IText
        => string.IsNullOrEmpty(text?.Value) ? null : text;

    /// <summary>
    ///   A made-up title for an episode with no title of its own at all.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="entry">The entry, when it was found.</param>
    /// <returns>The title, or <c>null</c> when the entry is not an episode or has titles.</returns>
    private static ITitle? Synthesized(MetadataGuid entityID, IWithTitles? entry)
    {
        if (entityID.EntityType != MetadataEntityType.Episode || entry is not IEpisode episode || episode.Titles is { Count: > 0 })
            return null;

        var languages = Languages.PreferredEpisodeNamingLanguages.Select(language => language.Language).Where(language => language is not TitleLanguage.Main);
        return GenericEpisodeTitles.Synthesize(episode.Type, episode.EpisodeNumber, languages.Append(TitleLanguage.English));
    }

    /// <summary>
    ///   The entry behind an ID, when it keeps a default of its own.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <returns>The entry, or <c>null</c>.</returns>
    private IInlineTextSource? InlineOf(MetadataGuid entityID)
        => ResolveQuietly(entityID) as IInlineTextSource;

    /// <summary>
    ///   The entry behind an ID, logging and answering nothing when the lookup
    ///   throws.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <returns>The entry, or <c>null</c> when it is gone or its lookup threw.</returns>
    private IMetadata? ResolveQuietly(MetadataGuid entityID)
    {
        try
        {
            return _metadataService.GetEntry(entityID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Looking up {Entry} for its texts threw and was skipped.", entityID);
            return null;
        }
    }

    /// <summary>
    ///   Whether a text passes the filters.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="options">The filters.</param>
    /// <returns><c>true</c> when it passes.</returns>
    private static bool Matches(IText text, TextFilteringOptions options)
        => (options.Source is null || text.Source == options.Source) &&
           (options.Language is null || text.Language == options.Language) &&
           (options.TitleType is null || (text is ITitle title && title.Type == options.TitleType)) &&
           (options.Preference is null || text.Preference == options.Preference) &&
           (options.IsEnabled is null || text.IsEnabled == options.IsEnabled);

    /// <summary>
    ///   A choice worked out once per entry, kind and settings.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="choose">Works the choice out.</param>
    /// <returns>The choice.</returns>
    private IText? Remember(MetadataGuid entityID, TextKind kind, Func<IText?> choose)
        => _memos.Get(entityID, kind is TextKind.Title ? TextMemoSlot.StoredTitle : TextMemoSlot.StoredOverview, Generation, choose);

    /// <summary>
    ///   A value worked out once per entry and settings, and worked out again
    ///   once anything it read changes.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="entityID">The entry.</param>
    /// <param name="slot">Which of the entry's values.</param>
    /// <param name="compute">Works the value out.</param>
    /// <param name="reentrant">Works out the value to answer with while it is being worked out on this thread.</param>
    /// <returns>The value.</returns>
    internal T Remember<T>(MetadataGuid entityID, TextMemoSlot slot, Func<T> compute, Func<T> reentrant)
        => _memos.Get(entityID, slot, Generation, compute, reentrant);

    /// <summary>
    ///   A value worked out before, as <see cref="Remember"/> hands it back,
    ///   so a caller skips building what working it out would take.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="entityID">The entry.</param>
    /// <param name="slot">Which of the entry's values.</param>
    /// <param name="value">The value, when it was found.</param>
    /// <returns><c>true</c> when the value was worked out before with the current settings.</returns>
    internal bool TryRemembered<T>(MetadataGuid entityID, TextMemoSlot slot, out T value)
        => _memos.TryGet(entityID, slot, Generation, out value);

    /// <summary>
    ///   Records that the value being worked out reads an entry, so it is
    ///   worked out again once the entry changes.
    /// </summary>
    /// <param name="entityID">The entry read.</param>
    internal void Record(MetadataGuid entityID)
        => _memos.Record(entityID);

    /// <summary>
    ///   Forgets what a filter matches an entry by, as when a series moves to
    ///   another group.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    internal void InvalidateFilters(MetadataGuid entityID)
        => _memos.ForgetFilters(entityID);

    /// <summary>
    ///   Forgets every value worked out for every entry.
    /// </summary>
    internal void ForgetAll()
        => _memos.ForgetAll();

    #endregion

    #region Reading by Model

    /// <summary>
    ///   What a model lists: every enabled text, the default on its row first.
    /// </summary>
    private static readonly TextFilteringOptions _listing = new();

    /// <summary>
    ///   The titles a stored entry lists, for its model.
    /// </summary>
    /// <param name="entry">The entry, which may keep a default title on its row.</param>
    /// <returns>The default on its row, then the enabled stored titles, its own source's first.</returns>
    internal IReadOnlyList<ITitle> ListTitles(IMetadata entry)
        => StoredTitles(entry.ID, entry as IInlineTextSource, _listing);

    /// <summary>
    ///   The overviews a stored entry lists, for its model.
    /// </summary>
    /// <param name="entry">The entry, which may keep a default overview on its row.</param>
    /// <returns>The default on its row, then the enabled stored overviews, its own source's first.</returns>
    internal IReadOnlyList<IText> ListOverviews(IMetadata entry)
        => StoredOverviews(entry.ID, entry as IInlineTextSource, _listing);

    /// <summary>
    ///   The other names a person has, for its model.
    /// </summary>
    /// <param name="entry">The creator or character.</param>
    /// <returns>The enabled titles its own source stored, in the order given, without the name on its row.</returns>
    internal IReadOnlyList<ITitle> AlternativeNamesOf(IMetadata entry)
        => OwnTitlesOf(entry);

    /// <summary>
    ///   The titles an entry's own source stored for it, for its model.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The enabled titles its own source stored, in the order given.</returns>
    internal IReadOnlyList<ITitle> OwnTitlesOf(IMetadata entry)
    {
        var entityID = entry.ID;
        _memos.Record(entityID);
        return [.. _textStore.GetTitles(entityID, entityID.Source).Where(title => Matches(title, _listing))];
    }

    /// <summary>
    ///   The title a stored entry's own source calls it by, for its model.
    /// </summary>
    /// <param name="entry">The entry, which may keep a default title on its row.</param>
    /// <returns>The default on its row, else its source's main title, else its first, or <c>null</c> when it has none.</returns>
    internal ITitle? DefaultTitleFor(IMetadata entry)
        => StoredDefaultTitle(entry.ID, entry as IInlineTextSource);

    /// <summary>
    ///   The generic title made up for a stored episode with no title at all,
    ///   for its model.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The title, or <c>null</c> when the entry is not an episode or has titles.</returns>
    internal ITitle? SynthesizedTitleFor(IMetadata entry)
        => Synthesized(entry.ID, entry as IWithTitles);

    /// <summary>
    ///   The overview a stored entry's own source gives it, for its model.
    /// </summary>
    /// <param name="entry">The entry, which may keep a default overview on its row.</param>
    /// <returns>The default on its row, else its source's first overview, or <c>null</c> when it has none.</returns>
    internal IText? DefaultOverviewFor(IMetadata entry)
        => StoredDefaultOverview(entry.ID, entry as IInlineTextSource);

    /// <summary>
    ///   The title the user's picks and language settings choose for a stored
    ///   entry, for its model.
    /// </summary>
    /// <param name="entry">The entry, which may keep a default title on its row.</param>
    /// <returns>The title, or <c>null</c> when none is picked or in a preferred language.</returns>
    internal ITitle? PreferredTitleFor(IMetadata entry)
        => ChooseStoredTitle(entry.ID, entry as IInlineTextSource);

    /// <summary>
    ///   The overview the user's picks and language settings choose for a
    ///   stored entry, for its model.
    /// </summary>
    /// <param name="entry">The entry, which may keep a default overview on its row.</param>
    /// <returns>The overview, or <c>null</c> when none is picked or in a preferred language.</returns>
    internal IText? PreferredOverviewFor(IMetadata entry)
        => ChooseStoredOverview(entry.ID, entry as IInlineTextSource);

    /// <summary>
    ///   The titles of an entry whose texts the store keeps.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="inline">The entry, when it keeps a default title on its row.</param>
    /// <param name="options">The filters.</param>
    /// <returns>The stored titles, its own source's first, with the default on its row where the entry lists it.</returns>
    private List<ITitle> StoredTitles(MetadataGuid entityID, IInlineTextSource? inline, TextFilteringOptions options)
    {
        _memos.Record(entityID);
        var stored = OwnFirst(entityID, _textStore.GetTitles(entityID, options.Source)).ToList();
        var title = options.IncludeInlineDefault ? inline?.InlineTitle : null;
        return [.. WithInline(entityID, stored, title, inline?.InlineTitlePlacement ?? InlineTextPlacement.First).Where(text => Matches(text, options))];
    }

    /// <summary>
    ///   The overviews of an entry whose texts the store keeps.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="inline">The entry, when it keeps a default overview on its row.</param>
    /// <param name="options">The filters.</param>
    /// <returns>The stored overviews, its own source's first, with the default on its row where the entry lists it.</returns>
    private List<IText> StoredOverviews(MetadataGuid entityID, IInlineTextSource? inline, TextFilteringOptions options)
    {
        _memos.Record(entityID);
        if (options.TitleType is not null)
            return [];

        var stored = OwnFirst(entityID, _textStore.GetOverviews(entityID, options.Source)).ToList();
        var overview = options.IncludeInlineDefault ? inline?.InlineOverview : null;
        return [.. WithInline(entityID, stored, overview, inline?.InlineOverviewPlacement ?? InlineTextPlacement.First).Where(text => Matches(text, options))];
    }

    /// <summary>
    ///   An entry's stored texts with the default kept on its row put where
    ///   the entry lists it.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="stored">The stored texts, its own source's first, each source's in order.</param>
    /// <param name="inline">The default on its row, or <c>null</c> when it has none.</param>
    /// <param name="placement">Where the entry lists the default.</param>
    /// <returns>The texts.</returns>
    private static List<T> WithInline<T>(MetadataGuid entityID, List<T> stored, T? inline, InlineTextPlacement placement) where T : class, IText
    {
        if (inline is null)
            return stored;

        switch (placement)
        {
            case InlineTextPlacement.First:
                stored.Insert(0, inline);
                break;
            case InlineTextPlacement.InGap:
                stored.Insert(GapOf(entityID, stored), inline);
                break;
        }

        return stored;
    }

    /// <summary>
    ///   Where the default a source listed among its stored texts sits: the
    ///   first position its stored texts skip.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="stored">The stored texts, its own source's first, each source's in order.</param>
    /// <returns>The index to put the default at.</returns>
    private static int GapOf<T>(MetadataGuid entityID, IReadOnlyList<T> stored) where T : IText
    {
        var index = 0;
        while (index < stored.Count && stored[index].Source == entityID.Source && stored[index].Ordering == index)
            index++;
        return index;
    }

    /// <summary>
    ///   The default title of an entry whose texts the store keeps.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="inline">The entry, when it keeps a default title on its row.</param>
    /// <returns>The default on its row, else its source's enabled main title, else its first, or <c>null</c>.</returns>
    private ITitle? StoredDefaultTitle(MetadataGuid entityID, IInlineTextSource? inline)
    {
        _memos.Record(entityID);
        if (inline?.InlineTitle is { } title)
            return title;

        var stored = _textStore.GetTitles(entityID, entityID.Source).Where(stored => stored.IsEnabled).ToList();
        return stored.FirstOrDefault(stored => stored.Type is TitleType.Main) ?? stored.FirstOrDefault();
    }

    /// <summary>
    ///   The default overview of an entry whose texts the store keeps.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="inline">The entry, when it keeps a default overview on its row.</param>
    /// <returns>The default on its row, else its source's first enabled overview, or <c>null</c>.</returns>
    private IText? StoredDefaultOverview(MetadataGuid entityID, IInlineTextSource? inline)
    {
        _memos.Record(entityID);
        return inline?.InlineOverview ?? _textStore.GetOverviews(entityID, entityID.Source).FirstOrDefault(stored => stored.IsEnabled);
    }

    /// <summary>
    ///   Chooses the title of an entry whose texts the store keeps, once per
    ///   entry and settings.
    /// </summary>
    /// <remarks>
    ///   By <see cref="TextChooser.ChooseStoredTitle"/>, with the user's
    ///   sources after the configured ones. The default on the entry's row
    ///   answers <c>x-main</c> even when its source did not list it, and no
    ///   other language unless it did.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <param name="inline">The entry, when it keeps a default title on its row.</param>
    /// <returns>The title, or <c>null</c> when none is picked or in a preferred language.</returns>
    private ITitle? ChooseStoredTitle(MetadataGuid entityID, IInlineTextSource? inline)
        => _memos.TryGet<IText?>(entityID, TextMemoSlot.StoredTitle, Generation, out var remembered) ? (ITitle?)remembered : (ITitle?)Remember(entityID, TextKind.Title, () =>
        {
            var candidates = StoredTitles(entityID, inline, _listing);
            var main = inline?.InlineTitle;
            if (candidates.Count is 0 && main is null)
                return null;

            var settings = ISettingsProvider.Instance.GetSettings();
            var episode = entityID.EntityType == MetadataEntityType.Episode;
            return TextChooser.ChooseStoredTitle(candidates, entityID.Source, new(
                [.. (episode ? Languages.PreferredEpisodeNamingLanguages : Languages.PreferredNamingLanguages).Select(language => language.Language)],
                [.. (episode ? settings.Language.EpisodeTitleSourceOrder : settings.Language.SeriesTitleSourceOrder).Append(MetadataSource.User).Distinct()],
                settings.Language.UseSynonyms,
                episode,
                Main: main
            ));
        });

    /// <summary>
    ///   Chooses the overview of an entry whose texts the store keeps, once
    ///   per entry and settings.
    /// </summary>
    /// <remarks>
    ///   By <see cref="TextChooser.ChooseStoredOverview"/>, with the user's
    ///   sources after the configured ones.
    /// </remarks>
    /// <param name="entityID">The entry.</param>
    /// <param name="inline">The entry, when it keeps a default overview on its row.</param>
    /// <returns>The overview, or <c>null</c> when none is picked or in a preferred language.</returns>
    private IText? ChooseStoredOverview(MetadataGuid entityID, IInlineTextSource? inline)
        => _memos.TryGet<IText?>(entityID, TextMemoSlot.StoredOverview, Generation, out var remembered) ? remembered : Remember(entityID, TextKind.Overview, () =>
        {
            var candidates = StoredOverviews(entityID, inline, _listing);
            if (candidates.Count is 0)
                return null;

            return TextChooser.ChooseStoredOverview(
                candidates,
                entityID.Source,
                [.. Languages.PreferredDescriptionNamingLanguages.Select(language => language.Language)],
                [.. ISettingsProvider.Instance.GetSettings().Language.DescriptionSourceOrder.Append(MetadataSource.User).Distinct()]
            );
        });

    /// <summary>
    ///   An entry's stored texts with its own source's first, each source's
    ///   in the order it gave them.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="texts">The stored texts, by source.</param>
    /// <returns>The texts, reordered.</returns>
    private static IEnumerable<T> OwnFirst<T>(MetadataGuid entityID, IReadOnlyList<T> texts) where T : IText
        => texts.OrderBy(text => text.Source == entityID.Source ? 0 : 1);

    #endregion

    #region Source Writes

    /// <inheritdoc />
    public IReadOnlyList<ITitle> GetContributedTitles(IMetadata entry, MetadataSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var entityID = entry.ID;
        _memos.Record(entityID);
        return [.. _textStore.Cache.GetTitles(entityID, source, isEnabled: true).Where(title => IsContribution(entityID, title))];
    }

    /// <inheritdoc />
    public IReadOnlyList<IText> GetContributedOverviews(IMetadata entry, MetadataSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var entityID = entry.ID;
        _memos.Record(entityID);
        return [.. _textStore.Cache.GetOverviews(entityID, source, isEnabled: true).Where(overview => IsContribution(entityID, overview))];
    }

    /// <inheritdoc />
    public void SetTitles(IMetadata entry, MetadataSource source, IEnumerable<ITitle> titles)
    {
        ArgumentNullException.ThrowIfNull(entry);

        SetTitles(entry.ID, source, titles);
    }

    /// <inheritdoc />
    public void SetTitles(MetadataGuid entityID, MetadataSource source, IEnumerable<ITitle> titles)
    {
        ArgumentNullException.ThrowIfNull(entityID);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(titles);

        _textStore.SetTitles(entityID, source, titles);
    }

    /// <inheritdoc />
    public void SetOverviews(IMetadata entry, MetadataSource source, IEnumerable<IText> overviews)
    {
        ArgumentNullException.ThrowIfNull(entry);

        SetOverviews(entry.ID, source, overviews);
    }

    /// <inheritdoc />
    public void SetOverviews(MetadataGuid entityID, MetadataSource source, IEnumerable<IText> overviews)
    {
        ArgumentNullException.ThrowIfNull(entityID);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(overviews);

        _textStore.SetOverviews(entityID, source, overviews);
    }

    /// <inheritdoc />
    public int RemoveTexts(MetadataGuid entityID, MetadataSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        return _textStore.RemoveEntry(entityID, source);
    }

    /// <summary>
    ///   Removes every stored text of the entries a filter picks, as when the
    ///   entries themselves are gone.
    /// </summary>
    /// <param name="entries">Which entries to clear.</param>
    /// <returns>How many entries lost text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <c>null</c>.</exception>
    internal int RemoveTexts(Func<MetadataGuid, bool> entries)
        => _textStore.RemoveEntries(entries).Count;

    /// <inheritdoc />
    public int RemoveContributions(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return _textStore.RemoveSource(source, entity => entity.Source != source).Count;
    }

    /// <summary>
    ///   Whether a text on an entry is another source's addition, rather than
    ///   the entry's own or a user's.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="text">The text.</param>
    /// <returns><c>true</c> for an addition.</returns>
    private static bool IsContribution(MetadataGuid entityID, IText text)
        => text.Source != entityID.Source && text.Source != MetadataSource.User;

    #endregion

    #region User Writes

    /// <inheritdoc />
    public IText AddText(MetadataGuid entityID, TextData data)
    {
        ArgumentNullException.ThrowIfNull(entityID);
        ArgumentNullException.ThrowIfNull(data);
        if (string.IsNullOrWhiteSpace(data.Value))
            throw new ArgumentException("A text must have a value.", nameof(data));

        var source = data.Source ?? MetadataSource.User;
        var languageCode = string.IsNullOrWhiteSpace(data.LanguageCode) ? "unk" : data.LanguageCode.Trim();
        var countryCode = string.IsNullOrWhiteSpace(data.CountryCode) ? null : data.CountryCode.Trim();
        var language = data.Language ?? (countryCode is null ? languageCode.GetTitleLanguage() : languageCode.GetTitleLanguage(countryCode));
        var probe = new TitleStub
        {
            Source = source,
            Language = language,
            LanguageCode = languageCode,
            CountryCode = countryCode,
            Value = data.Value,
            Type = data.TitleType,
        };
        var text = MetadataTextStore.Check(data.Kind is TextKind.Title ? probe : new TextStub { Source = source, Language = language, LanguageCode = languageCode, CountryCode = countryCode, Value = data.Value }, nameof(data)) with
        {
            ScriptCode = string.IsNullOrWhiteSpace(data.ScriptCode) ? null : data.ScriptCode.Trim(),
        };
        if (text.ScriptCode is { Length: > MetadataTextStore.MaxScriptCodeLength })
            throw new ArgumentException($"A script code must be at most {MetadataTextStore.MaxScriptCodeLength} characters, but got '{text.ScriptCode}'.", nameof(data));

        var row = _textStore.Change<MetadataTextRow>(() =>
        {
            var created = MetadataTextRow.Create(data.Kind);
            created.EntryID = entityID;
            created.Source = source;
            text.CopyTo(created);
            created.IsEnabled = data.IsEnabled;
            created.Preference = data.Preference;
            created.Ordering = _textStore.Cache.GetRows(entityID).Where(stored => stored.Kind == data.Kind && _textStore.Cache.SourceOf(stored) == source)
                .Select(stored => stored.Ordering + 1).DefaultIfEmpty(0).Max();
            return ([created, .. ClearedSiblings(entityID, created)], [], created);
        });

        var added = Stored(entityID, row.Kind, row.RowID)!;
        TextAdded?.Invoke(this, new() { Text = added });
        return added;
    }

    /// <inheritdoc />
    public IText UpdateText(IText text, TextUpdateData data)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(data);
        if (data.Value is not null && string.IsNullOrWhiteSpace(data.Value))
            throw new ArgumentException("A text must have a value.", nameof(data));

        return ChangeText(text, data);
    }

    /// <summary>
    ///   Changes a stored text, taking a blank value as it is.
    /// </summary>
    /// <param name="text">The stored text.</param>
    /// <param name="data">What to change.</param>
    /// <returns>The text as it is stored now.</returns>
    /// <exception cref="InvalidOperationException">A new value is given to a text a user did not add themselves.</exception>
    private IText ChangeText(IText text, TextUpdateData data)
    {
        var (entityID, kind, id) = Identify(text);
        var changed = _textStore.Change<bool>(() =>
        {
            var row = StoredRow(entityID, kind, id);
            var changed = false;
            if (data.Value is { } value && !string.Equals(row.Value, value, StringComparison.Ordinal))
            {
                if (row.Source != MetadataSource.User || row.ReferenceID is not null)
                    throw new InvalidOperationException("Only a text a user added themselves can be given a new value.");

                row.Value = value;
                changed = true;
            }

            if (data.IsEnabled is { } isEnabled && row.IsEnabled != isEnabled)
            {
                row.IsEnabled = isEnabled;
                changed = true;
            }

            if (data.Ordering is { } ordering && row.Ordering != ordering)
            {
                row.Ordering = ordering;
                changed = true;
            }

            var siblings = new List<MetadataTextRow>();
            if (data.Preference is { } preference && row.Preference != preference)
            {
                row.Preference = preference;
                siblings.AddRange(ClearedSiblings(entityID, row));
                changed = true;
            }

            if (!changed)
                return ([], [], false);

            return ([row, .. siblings], [], true);
        });

        var updated = Stored(entityID, kind, id)!;
        if (changed)
            TextUpdated?.Invoke(this, new() { Text = updated });
        return updated;
    }

    /// <inheritdoc />
    public IText EnableText(IText text, bool isEnabled)
        => UpdateText(text, new() { IsEnabled = isEnabled });

    /// <inheritdoc />
    public ITitle SetPreferredTitle(MetadataGuid entityID, ITitle title, bool forLanguageOnly = false)
        => (ITitle)SetPreferred(entityID, TextKind.Title, title, forLanguageOnly, false);

    /// <inheritdoc />
    public IText SetPreferredOverview(MetadataGuid entityID, IText overview, bool forLanguageOnly = false)
        => SetPreferred(entityID, TextKind.Overview, overview, forLanguageOnly, false);

    /// <inheritdoc />
    public bool UnsetPreferredText(IText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.ID is null || text.EntityID is null)
            return false;

        var (entityID, kind, id) = Identify(text);
        var (had, removed, result) = _textStore.Change<(bool, bool, IText?)>(() =>
        {
            if (FindRow(entityID, kind, id) is not { } row || row.Preference is TextPreference.None)
                return ([], [], (false, false, null));

            if (row.ReferenceID is not null)
                return ([], [row], (true, true, Stored(entityID, kind, id)));

            row.Preference = TextPreference.None;
            return ([row], [], (true, false, null));
        });

        if (!had)
            return false;

        if (removed)
            TextRemoved?.Invoke(this, new() { Text = result! });
        else
            TextUpdated?.Invoke(this, new() { Text = Stored(entityID, kind, id)! });
        return true;
    }

    /// <inheritdoc />
    public int UnsetAllPreferredTexts(MetadataGuid entityID, TextKind? kind = null)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        var (updated, removed) = _textStore.Change<(List<(TextKind Kind, int ID)>, List<IText>)>(() =>
        {
            var saving = new List<MetadataTextRow>();
            var deleting = new List<MetadataTextRow>();
            foreach (var row in _textStore.Cache.GetRows(entityID))
            {
                if (row.Preference is TextPreference.None || (kind is not null && row.Kind != kind))
                    continue;

                var copy = _textStore.Cache.ToRow(entityID, row);
                if (copy.ReferenceID is not null)
                {
                    deleting.Add(copy);
                    continue;
                }

                copy.Preference = TextPreference.None;
                saving.Add(copy);
            }

            var removedTexts = deleting.Select(row => Stored(entityID, row.Kind, row.RowID)).OfType<IText>().ToList();
            return (saving, deleting, (saving.Select(row => (row.Kind, row.RowID)).ToList(), removedTexts));
        });

        foreach (var text in removed)
            TextRemoved?.Invoke(this, new() { Text = text });
        foreach (var (textKind, id) in updated)
            if (Stored(entityID, textKind, id) is { } text)
                TextUpdated?.Invoke(this, new() { Text = text });
        return updated.Count + removed.Count;
    }

    /// <inheritdoc />
    public bool RemoveText(IText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.ID is null || text.EntityID is null)
            return false;

        var (entityID, kind, id) = Identify(text);
        var removed = _textStore.Change<IText?>(() =>
        {
            if (FindRow(entityID, kind, id) is not { } row)
                return ([], [], null);

            return ([], [row], Stored(entityID, kind, id));
        });
        if (removed is null)
            return false;

        TextRemoved?.Invoke(this, new() { Text = removed });
        return true;
    }

    /// <summary>
    ///   Puts a preference on a stored text of the entry, or on a new pick of
    ///   any other text.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="text">The text to prefer.</param>
    /// <param name="forLanguageOnly">Whether the preference is for the text's language only.</param>
    /// <param name="allowBlank">Whether a blank value is taken as it is, as for an overview a user cleared on purpose.</param>
    /// <returns>The stored text carrying the preference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> or <paramref name="text"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">The text's value is blank and <paramref name="allowBlank"/> is <c>false</c>.</exception>
    private IText SetPreferred(MetadataGuid entityID, TextKind kind, IText text, bool forLanguageOnly, bool allowBlank)
    {
        ArgumentNullException.ThrowIfNull(entityID);
        ArgumentNullException.ThrowIfNull(text);
        if (!allowBlank && string.IsNullOrWhiteSpace(text.Value))
            throw new ArgumentException("A preferred text must have a value.", nameof(text));

        var preference = forLanguageOnly ? TextPreference.Language : TextPreference.Overall;
        var checkedText = MetadataTextStore.Check(text, nameof(text));

        // A stored text of the other kind, such as a title given as an
        // overview, is taken by its value alone, as its ID names another row.
        var sameKind = (text is ITitle) == (kind is TextKind.Title);
        if (!sameKind)
            checkedText = checkedText with { TitleType = TitleType.None };
        var (row, added) = _textStore.Change<(MetadataTextRow, bool)>(() =>
        {
            // A stored text of the entry itself carries the preference.
            if (sameKind && text.ID is { } ownID && text.EntityID == entityID && FindRow(entityID, kind, ownID) is { } own)
            {
                own.Preference = preference;
                own.IsEnabled = true;
                return ([own, .. ClearedSiblings(entityID, own)], [], (own, false));
            }

            // Any other text becomes a pick, or updates the pick already made.
            var referenceID = sameKind && text.ID is { } id && text.EntityID is not null ? id : (int?)null;
            var pick = _textStore.Cache.GetRows(entityID)
                .Where(stored => stored.Kind == kind && _textStore.Cache.SourceOf(stored) == MetadataSource.User)
                .Select(stored => _textStore.Cache.ToRow(entityID, stored))
                .FirstOrDefault(stored => referenceID is not null ? stored.ReferenceID == referenceID : stored.ReferenceID is null && checkedText.SameText(stored));
            var isNew = pick is null;
            pick ??= MetadataTextRow.Create(kind);
            pick.EntryID = entityID;
            pick.Source = MetadataSource.User;
            checkedText.CopyTo(pick);
            pick.ReferenceID = referenceID;
            pick.Preference = preference;
            pick.IsEnabled = true;
            if (isNew)
                pick.Ordering = _textStore.Cache.GetRows(entityID).Where(stored => stored.Kind == kind && _textStore.Cache.SourceOf(stored) == MetadataSource.User)
                    .Select(stored => stored.Ordering + 1).DefaultIfEmpty(0).Max();
            return ([pick, .. ClearedSiblings(entityID, pick)], [], (pick, isNew));
        });

        var stored = Stored(entityID, kind, row.RowID)!;
        if (added)
            TextAdded?.Invoke(this, new() { Text = stored });
        else
            TextUpdated?.Invoke(this, new() { Text = stored });
        return stored;
    }

    /// <summary>
    ///   The entry's other texts that lose their preference to a row taking
    ///   the same one.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="row">The row taking the preference.</param>
    /// <returns>The other rows, with their preference cleared.</returns>
    private List<MetadataTextRow> ClearedSiblings(MetadataGuid entityID, MetadataTextRow row)
    {
        if (row.Preference is TextPreference.None)
            return [];

        var cleared = new List<MetadataTextRow>();
        foreach (var sibling in _textStore.Cache.GetRows(entityID))
        {
            if (sibling.Kind != row.Kind || sibling.ID == row.RowID || sibling.Preference != row.Preference)
                continue;

            var copy = _textStore.Cache.ToRow(entityID, sibling);
            if (row.Preference is TextPreference.Language && copy.Language != row.Language)
                continue;

            copy.Preference = TextPreference.None;
            cleared.Add(copy);
        }

        return cleared;
    }

    /// <summary>
    ///   The entry, kind and ID of a stored text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Where the text is stored.</returns>
    /// <exception cref="ArgumentException">The text is not stored.</exception>
    private static (MetadataGuid EntityID, TextKind Kind, int ID) Identify(IText text)
        => text is { ID: { } id, EntityID: { } entityID }
            ? (entityID, text is ITitle ? TextKind.Title : TextKind.Overview, id)
            : throw new ArgumentException("The text is not a stored one.", nameof(text));

    /// <summary>
    ///   A detached copy of a stored row.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="id">The row's ID.</param>
    /// <returns>The row, or <c>null</c> when the entry has no such text.</returns>
    private MetadataTextRow? FindRow(MetadataGuid entityID, TextKind kind, int id)
    {
        foreach (var row in _textStore.Cache.GetRows(entityID))
            if (row.ID == id && row.Kind == kind)
                return _textStore.Cache.ToRow(entityID, row);

        return null;
    }

    /// <summary>
    ///   A detached copy of a stored row that must be there.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="id">The row's ID.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentException">The entry has no such text.</exception>
    private MetadataTextRow StoredRow(MetadataGuid entityID, TextKind kind, int id)
        => FindRow(entityID, kind, id) ?? throw new ArgumentException($"{entityID} has no stored {kind.ToString().ToLowerInvariant()} {id}.", "text");

    /// <summary>
    ///   A stored text as it is handed out.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="id">The text's ID.</param>
    /// <returns>The text, or <c>null</c> when the entry has no such text.</returns>
    private IText? Stored(MetadataGuid entityID, TextKind kind, int id)
    {
        foreach (var row in _textStore.Cache.GetRows(entityID))
            if (row.ID == id && row.Kind == kind)
                return _textStore.Cache.ToText(entityID, row, _textStore.Cache.ValueOf(row));

        return null;
    }

    #endregion

    #region Maintenance

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> GetOrphanedEntries(MetadataSource? entitySource = null)
        => [
            .. _textStore.Cache.Enumerate()
                .Select(entry => entry.Entity)
                .Where(entity => (entitySource is null || entity.Source == entitySource) && Resolve(entity) is null),
        ];

    /// <inheritdoc />
    public int PurgeOrphanedTexts(MetadataSource? entitySource = null)
        => GetOrphanedEntries(entitySource).Sum(entity => _textStore.RemoveEntry(entity));

    /// <inheritdoc />
    public void Invalidate(MetadataGuid entityID)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        Forget(entityID);
    }

    /// <summary>
    ///   Looks an entry up, treating one whose lookup throws as found, so a
    ///   broken lookup never costs an entry its texts.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <returns>The entry, a placeholder when the lookup threw, or <c>null</c> when it is gone.</returns>
    private object? Resolve(MetadataGuid entityID)
    {
        try
        {
            return _metadataService.GetEntry(entityID);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Looking up {Entry} threw; its texts are kept.", entityID);
            return entityID;
        }
    }

    /// <summary>
    ///   Drops what was worked out for an entry, and for every entry worked
    ///   out from it.
    /// </summary>
    /// <param name="entityID">The entry whose text changed.</param>
    private void Forget(MetadataGuid entityID)
        => _memos.Forget(entityID);

    #endregion

    #region Custom Texts

    /// <summary>
    ///   The title a user named a Shoko entry by, over every other.
    /// </summary>
    /// <param name="entityID">The Shoko entry.</param>
    /// <returns>The title preferred overall, or <c>null</c> when the entry has none.</returns>
    internal ITitle? CustomTitleOf(MetadataGuid entityID)
        => OverallPick(entityID, TextKind.Title) as ITitle;

    /// <summary>
    ///   The overview a user described a Shoko entry with, over every other.
    /// </summary>
    /// <param name="entityID">The Shoko entry.</param>
    /// <returns>The overview preferred overall, or <c>null</c> when the entry has none.</returns>
    internal IText? CustomOverviewOf(MetadataGuid entityID)
        => OverallPick(entityID, TextKind.Overview);

    /// <summary>
    ///   Names a Shoko entry, or hands its name back to the linked entries.
    /// </summary>
    /// <remarks>
    ///   The name is stored as the entry's <c>user</c> title, preferred over
    ///   every other; a name given before is changed in place.
    /// </remarks>
    /// <param name="entityID">The Shoko entry.</param>
    /// <param name="value">The name, or <c>null</c> or blank to remove it.</param>
    /// <returns><c>true</c> when the name changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    internal bool SetCustomTitle(MetadataGuid entityID, string? value)
        => SetCustom(entityID, TextKind.Title, value, false);

    /// <summary>
    ///   Describes a Shoko entry, or hands its overview back to the linked
    ///   entries.
    /// </summary>
    /// <remarks>
    ///   The overview is stored as the entry's <c>user</c> overview, preferred
    ///   over every other; one given before is changed in place. A blank one
    ///   is kept too, so the entry shows no overview at all.
    /// </remarks>
    /// <param name="entityID">The Shoko entry.</param>
    /// <param name="value">The overview, or <c>null</c> to remove it.</param>
    /// <returns><c>true</c> when the overview changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    internal bool SetCustomOverview(MetadataGuid entityID, string? value)
        => SetCustom(entityID, TextKind.Overview, value, true);

    /// <summary>
    ///   Sets or removes the text of a kind a user gave a Shoko entry over
    ///   every other.
    /// </summary>
    /// <param name="entityID">The Shoko entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="value">The text, or <c>null</c> to remove it.</param>
    /// <param name="keepBlank">Whether a blank text is kept rather than removing the one given before.</param>
    /// <returns><c>true</c> when the text changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entityID"/> is <c>null</c>.</exception>
    private bool SetCustom(MetadataGuid entityID, TextKind kind, string? value, bool keepBlank)
    {
        ArgumentNullException.ThrowIfNull(entityID);

        var current = OverallPick(entityID, kind);
        var typed = current is { Source: var source, ReferenceID: null } && source == MetadataSource.User;
        if (value is null || (!keepBlank && string.IsNullOrWhiteSpace(value)))
        {
            if (current is null)
                return false;

            if (typed)
                RemoveText(current);
            else
                UnsetPreferredText(current);
            return true;
        }

        if (current is not null && string.Equals(current.Value, value, StringComparison.Ordinal))
            return false;

        if (typed)
        {
            ChangeText(current!, new() { Value = value });
            return true;
        }

        // Another text preferred overall, such as a pick of another entry's
        // title, gives way to the one typed.
        if (current is not null)
            UnsetPreferredText(current);
        SetPreferred(entityID, kind, kind is TextKind.Title ? CustomTitle(value) : CustomOverview(value), false, true);
        return true;
    }

    /// <summary>
    ///   A title a user typed, in no known language.
    /// </summary>
    /// <param name="value">The title.</param>
    /// <returns>The title.</returns>
    private static TitleStub CustomTitle(string value)
        => new() { Source = MetadataSource.User, Language = TitleLanguage.Unknown, LanguageCode = "unk", Value = value, Type = TitleType.Main };

    /// <summary>
    ///   An overview a user typed, in no known language.
    /// </summary>
    /// <param name="value">The overview.</param>
    /// <returns>The overview.</returns>
    private static TextStub CustomOverview(string value)
        => new() { Source = MetadataSource.User, Language = TitleLanguage.Unknown, LanguageCode = "unk", Value = value };

    /// <summary>
    ///   The enabled text of a kind an entry prefers over every other.
    /// </summary>
    /// <param name="entityID">The entry.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <returns>The text, or <c>null</c> when there is none.</returns>
    private IText? OverallPick(MetadataGuid entityID, TextKind kind)
    {
        if (_textStore is null)
            return null;

        _memos.Record(entityID);
        foreach (var row in _textStore.Cache.GetRows(entityID))
            if (row.Kind == kind && row.IsEnabled && row.Preference is TextPreference.Overall)
                return _textStore.Cache.ToText(entityID, row, _textStore.Cache.ValueOf(row));

        return null;
    }

    #endregion

    #region Series

    /// <summary>
    ///   The walk behind <see cref="AnimeSeries.PreferredTitle"/>.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The preferred title, or the series' default one.</returns>
    internal ITitle? PreferredTitleOf(AnimeSeries series)
        => _memos.TryGet<ITitle?>(ref series.TextMemo, TextMemoSlot.PreferredTitle, Generation, out var remembered) ? remembered : WorkPreferredTitleOf(series);

    /// <summary>
    ///   Works out, and remembers, what <see cref="PreferredTitleOf(AnimeSeries)"/> hands back.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The value.</returns>
    private ITitle? WorkPreferredTitleOf(AnimeSeries series)
        => _memos.Get<ITitle?>(((IMetadata)series).ID, TextMemoSlot.PreferredTitle, Generation, () => ChoosePreferredTitleOf(series), () => DefaultTitleOf(series), ref series.TextMemo);

    /// <summary>
    ///   Works out the series' preferred title.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The preferred title, or the series' default one.</returns>
    private ITitle? ChoosePreferredTitleOf(AnimeSeries series)
    {
        if (OverallPick(((IMetadata)series).ID, TextKind.Title) is ITitle pick)
            return pick;

        return Guarded(series, () =>
        {
            var settings = ISettingsProvider.Instance.GetSettings();
            var anime = AnimeOf(series);
            List<ITitle>? anidbTitles = null;
            var perSource = new Dictionary<MetadataSource, IReadOnlyList<ITitle>>();
            foreach (var language in Languages.PreferredNamingLanguages)
                foreach (var source in settings.Language.SeriesTitleSourceOrder)
                {
                    var title = source switch
                    {
                        _ when source == MetadataSource.AniDB => language.Language is TitleLanguage.Main
                            ? anime?.DefaultTitle
                            : PickAnidb(anidbTitles ??= [.. anime?.Titles ?? []], language.Language, settings.Language.UseSynonyms),
                        _ => Pick(Cached(perSource, source, () => SeriesTitlesFrom(series, source)), language.Language, settings.Language.UseSynonyms),
                    };
                    if (title is not null)
                        return title;
                }

            return DefaultTitleOf(series);
        }, DefaultTitleOf(series));
    }

    /// <summary>
    ///   The title behind the series' <see cref="IWithTitles.DefaultTitle"/>.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The name a user gave it, else its AniDB anime's default title, else a stand-in naming the anime.</returns>
    internal ITitle DefaultTitleOf(AnimeSeries series)
        => _memos.TryGet<ITitle>(ref series.TextMemo, TextMemoSlot.DefaultTitle, Generation, out var remembered) ? remembered : WorkDefaultTitleOf(series);

    /// <summary>
    ///   Works out, and remembers, what <see cref="DefaultTitleOf(AnimeSeries)"/> hands back.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The value.</returns>
    private ITitle WorkDefaultTitleOf(AnimeSeries series)
        => _memos.Get<ITitle>(((IMetadata)series).ID, TextMemoSlot.DefaultTitle, Generation, () =>
        {
            if (CustomTitleOf(((IMetadata)series).ID) is { } custom)
                return custom;

            if (AnimeOf(series) is { } anime)
                return anime.DefaultTitle;

            if (_titleHelper?.SearchAnimeID(series.AniDB_ID) is { } titleResponse && titleResponse.Titles.FirstOrDefault(title => title.TitleType == TitleType.Main) is { } defaultTitle)
                return defaultTitle;

            return PlaceholderTitleOf(series);
        }, () => PlaceholderTitleOf(series), ref series.TextMemo);

    /// <summary>
    ///   The stand-in title of a series whose anime is not known at all.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The title, naming the AniDB anime.</returns>
    private static TitleStub PlaceholderTitleOf(AnimeSeries series)
        => new()
        {
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = $"<AniDB Anime {series.AniDB_ID}>",
            Source = MetadataSource.Shoko,
        };

    /// <summary>
    ///   The AniDB anime of a series, recorded as read so a change to it works
    ///   the series' texts out again.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The anime, or <c>null</c> when it is not stored.</returns>
    private AniDB_Anime? AnimeOf(AnimeSeries series)
    {
        _memos.Record(AnidbAnimeID(series.AniDB_ID));
        return series.AniDB_Anime;
    }

    /// <summary>
    ///   The list behind the series' <see cref="IWithTitles.Titles"/>.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>Every title: the user's, AniDB's, the linked entries' and the contributed ones.</returns>
    internal IReadOnlyList<ITitle> TitlesOf(AnimeSeries series)
        => _memos.TryGet<IReadOnlyList<ITitle>>(ref series.TextMemo, TextMemoSlot.Titles, Generation, out var remembered) ? remembered : WorkTitlesOf(series);

    /// <summary>
    ///   Works out, and remembers, what <see cref="TitlesOf(AnimeSeries)"/> hands back.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The value.</returns>
    private IReadOnlyList<ITitle> WorkTitlesOf(AnimeSeries series)
        => _memos.Get<IReadOnlyList<ITitle>>(((IMetadata)series).ID, TextMemoSlot.Titles, Generation, () =>
        {
            var titles = new List<ITitle>();
            var custom = CustomTitleOf(((IMetadata)series).ID);
            if (custom is not null)
                titles.Add(custom);

            titles.AddRange(DemoteMain(((IWithTitles?)AnimeOf(series))?.Titles ?? [], custom is not null, TitleType.Official));
            Guarded(series, () =>
            {
                titles.AddRange(GetContributedTitles(series).Where(title => title.ID is null || title.ID != custom?.ID));
                titles.AddRange(LinkedSeriesOf(series).Where(entry => entry.Source != MetadataSource.AniDB).SelectMany(Titles));
                return true;
            }, false);

            return titles;
        }, () => [], ref series.TextMemo);

    /// <summary>
    ///   The entries a series is linked to: its AniDB anime, then the series
    ///   every source links it to, as <see cref="AnimeSeries.LinkedSeries"/>
    ///   lists them.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The linked series.</returns>
    private List<ISeries> LinkedSeriesOf(AnimeSeries series)
    {
        var linked = new List<ISeries>();
        if (AnimeOf(series) is { } anime)
            linked.Add(anime);
        linked.AddRange(Links(AnidbAnimeID(series.AniDB_ID), _metadataService.GetSeriesCrossReferences(series.AniDB_ID)).Select(link => link.Provider).OfType<ISeries>());
        return linked;
    }

    /// <summary>
    ///   The overview behind the series' <see cref="IWithOverviews.DefaultOverview"/>.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>Its AniDB anime's description, or <c>null</c> when it has none.</returns>
    internal IText? DefaultOverviewOf(AnimeSeries series)
        => _memos.TryGet<IText?>(ref series.TextMemo, TextMemoSlot.DefaultOverview, Generation, out var remembered) ? remembered : WorkDefaultOverviewOf(series);

    /// <summary>
    ///   Works out, and remembers, what <see cref="DefaultOverviewOf(AnimeSeries)"/> hands back.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The value.</returns>
    private IText? WorkDefaultOverviewOf(AnimeSeries series)
        => _memos.Get<IText?>(((IMetadata)series).ID, TextMemoSlot.DefaultOverview, Generation, () => AnidbDescription(AnimeOf(series)?.Description, TitleLanguage.English), () => null, ref series.TextMemo);

    /// <summary>
    ///   The walk behind <see cref="AnimeSeries.PreferredOverview"/>.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The preferred description, or <c>null</c> when none speaks a preferred language.</returns>
    internal IText? PreferredDescriptionOf(AnimeSeries series)
        => _memos.TryGet<IText?>(ref series.TextMemo, TextMemoSlot.PreferredOverview, Generation, out var remembered) ? remembered : WorkPreferredDescriptionOf(series);

    /// <summary>
    ///   Works out, and remembers, what <see cref="PreferredDescriptionOf(AnimeSeries)"/> hands back.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The value.</returns>
    private IText? WorkPreferredDescriptionOf(AnimeSeries series)
        => _memos.Get<IText?>(((IMetadata)series).ID, TextMemoSlot.PreferredOverview, Generation, () => ChoosePreferredDescriptionOf(series), () => null, ref series.TextMemo);

    /// <summary>
    ///   Works out the series' preferred overview.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The preferred description, or <c>null</c> when none speaks a preferred language.</returns>
    private IText? ChoosePreferredDescriptionOf(AnimeSeries series)
        => OverallPick(((IMetadata)series).ID, TextKind.Overview) ?? Guarded(series, () =>
        {
            var anidbDescription = AnimeOf(series)?.Description;
            var perSource = new Dictionary<MetadataSource, IReadOnlyList<IText>>();
            foreach (var language in Languages.PreferredDescriptionNamingLanguages)
                foreach (var source in ISettingsProvider.Instance.GetSettings().Language.DescriptionSourceOrder)
                {
                    var description = source switch
                    {
                        _ when source == MetadataSource.AniDB => AnidbDescription(anidbDescription, language.Language),
                        _ => Pick(Cached(perSource, source, () => SeriesDescriptionsFrom(series, source)), language.Language, useSynonyms: false),
                    };
                    if (description is not null)
                        return description;
                }

            return null;
        }, null);

    /// <summary>
    ///   The list behind the series' <see cref="IWithOverviews.Overviews"/>.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>Every description: the linked entries' and the contributed ones.</returns>
    internal IReadOnlyList<IText> DescriptionsOf(AnimeSeries series)
        => Guarded<IReadOnlyList<IText>>(series, () =>
        [
            .. GetContributedOverviews(series),
            .. LinkedSeriesOf(series).SelectMany(Descriptions),
        ], []);

    /// <summary>
    ///   A source's titles for a series: what it contributed, then those of
    ///   the entry that speaks for the anime on it.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <returns>The titles, without any claiming the main slot.</returns>
    private IReadOnlyList<ITitle> SeriesTitlesFrom(AnimeSeries series, MetadataSource source)
        => [.. GetContributedTitles(series, source), .. WithoutMain(Titles(SeriesTitleEntry(series.AniDB_ID, AnimeTypeOf(series), source, () => EpisodesOf(series.AniDB_ID))))];

    /// <summary>
    ///   A source's descriptions for a series: what it contributed, then
    ///   those of the entry that speaks for the anime on it.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="source">The source.</param>
    /// <returns>The descriptions.</returns>
    private IReadOnlyList<IText> SeriesDescriptionsFrom(AnimeSeries series, MetadataSource source)
        => [.. GetContributedOverviews(series, source), .. Descriptions(SeriesDescriptionEntry(series.AniDB_ID, AnimeTypeOf(series), source, () => EpisodesOf(series.AniDB_ID)))];

    /// <summary>
    ///   The entry whose titles speak for a whole anime on a source: its one
    ///   stored series, or the film or collection its films name.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="animeType">The anime's type, which decides which side is asked first.</param>
    /// <param name="source">The source.</param>
    /// <param name="episodes">Reads every AniDB episode of the anime, with its type and English title.</param>
    /// <returns>The entry, or <c>null</c> when neither side says anything.</returns>
    internal IMetadata? SeriesTitleEntry(int anidbAnimeID, AnimeType animeType, MetadataSource source, Func<IEnumerable<(int ID, EpisodeType Type, string? Title)>> episodes)
    {
        var (show, film) = SpeakingSide(anidbAnimeID, animeType, source, episodes);
        return show ?? film;
    }

    /// <summary>
    ///   The entry whose descriptions speak for a whole anime on a source,
    ///   read off the same side as its titles.
    /// </summary>
    /// <remarks>
    ///   On the series side, that is the part of the one series the anime
    ///   covers, and nothing when no episode of it is linked; the films are
    ///   not asked then, so titles and descriptions never come from two
    ///   different entries' sides.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="animeType">The anime's type, which decides which side is asked first.</param>
    /// <param name="source">The source.</param>
    /// <param name="episodes">Reads every AniDB episode of the anime, with its type and English title.</param>
    /// <returns>The entry, or <c>null</c> when the speaking side says nothing.</returns>
    internal IMetadata? SeriesDescriptionEntry(int anidbAnimeID, AnimeType animeType, MetadataSource source, Func<IEnumerable<(int ID, EpisodeType Type, string? Title)>> episodes)
    {
        var (show, film) = SpeakingSide(anidbAnimeID, animeType, source, episodes);
        return show is null ? film : ShowDescriptionEntry(anidbAnimeID, show, source);
    }

    /// <summary>
    ///   Which side speaks for a whole anime on a source: its one stored
    ///   series, or the film or collection its films name.
    /// </summary>
    /// <remarks>
    ///   Movies, web releases and TV specials ask their films first, and
    ///   every other type its series first. The side asked second speaks only
    ///   when the first has no entry. The films speak by the rules in
    ///   <see cref="MovieTextRules.ChooseSeriesEntry"/>.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="animeType">The anime's type.</param>
    /// <param name="source">The source.</param>
    /// <param name="episodes">Reads every AniDB episode of the anime, with its type and English title.</param>
    /// <returns>The one series, or the film or collection; at most one of them is set.</returns>
    private (IMetadata? Show, IMetadata? Film) SpeakingSide(int anidbAnimeID, AnimeType animeType, MetadataSource source,
        Func<IEnumerable<(int ID, EpisodeType Type, string? Title)>> episodes)
    {
        IMetadata? Show() => OnlyEntry(Links(AnidbAnimeID(anidbAnimeID), _metadataService.GetSeriesCrossReferences(anidbAnimeID, source)));
        IMetadata? Film() => FilmEntry(anidbAnimeID, source, episodes());

        if (MovieTextRules.MoviesFirst(animeType))
            return Film() is { } film ? (null, film) : (Show(), null);

        return Show() is { } show ? (show, null) : (null, Film());
    }

    /// <summary>
    ///   The part of an anime's one linked series that describes it on a
    ///   source.
    /// </summary>
    /// <remarks>
    ///   A series with no linked episodes says nothing. An anime covering only
    ///   specials reads its one linked episode; one starting past the first
    ///   season reads that season rather than the whole series.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="show">The one stored series the anime is linked to.</param>
    /// <param name="source">The source.</param>
    /// <returns>The entry, or <c>null</c> when the series says nothing.</returns>
    private IMetadata? ShowDescriptionEntry(int anidbAnimeID, IMetadata show, MetadataSource source)
    {
        // A series linked with none of its episodes is a link nothing has
        // confirmed, so it is not trusted to describe the anime.
        var seasons = Links(AnidbAnimeID(anidbAnimeID), _metadataService.GetSeasonCrossReferences(anidbAnimeID, source));
        if (seasons.Count is 0)
            return null;

        if (seasons.All(season => season.SeasonNumber is 0))
            return OnlyEntry(Links(AnidbAnimeID(anidbAnimeID), _metadataService.GetEpisodeCrossReferencesForSeries(anidbAnimeID, source)));

        var first = seasons.Where(season => season.SeasonNumber is not 0).MinBy(season => season.SeasonNumber)!;
        return first.SeasonNumber is 1 ? show : first.Provider;
    }

    /// <summary>
    ///   The AniDB type of a series' anime.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The type, or <see cref="AnimeType.Unknown"/> when the anime is missing.</returns>
    private AnimeType AnimeTypeOf(AnimeSeries series)
        => AnimeOf(series)?.AnimeType ?? AnimeType.Unknown;

    /// <summary>
    ///   Every AniDB episode of an anime, with its type and English title.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The episodes.</returns>
    private static IEnumerable<(int ID, EpisodeType Type, string? Title)> EpisodesOf(int anidbAnimeID)
        => RepoFactory.AniDB_Episode.GetByAnimeID(anidbAnimeID).Select(episode => (episode.EpisodeID, episode.EpisodeType, (string?)episode.EnglishTitle));

    /// <summary>
    ///   The stored film or collection that speaks for a whole anime on a
    ///   source, given the anime's episodes.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="source">The source.</param>
    /// <param name="episodes">Every AniDB episode of the anime, with its type and English title.</param>
    /// <returns>The film or collection, or <c>null</c> when the source's films say nothing for the anime.</returns>
    internal IMetadata? FilmEntry(int anidbAnimeID, MetadataSource source, IEnumerable<(int ID, EpisodeType Type, string? Title)> episodes)
    {
        var links = Links(AnidbAnimeID(anidbAnimeID), _metadataService.GetMovieCrossReferencesForSeries(anidbAnimeID, source));
        if (links.Count is 0)
            return null;

        var entries = new Dictionary<MetadataGuid, IMetadata>();
        var filmsByEpisode = new Dictionary<int, List<MetadataGuid>>();
        foreach (var link in links)
        {
            if (link.ProviderID is not { } id || link.Provider is not { } film)
                continue;

            entries.TryAdd(id, film);
            if (!filmsByEpisode.TryGetValue(link.AnidbEpisodeID, out var films))
                filmsByEpisode[link.AnidbEpisodeID] = films = [];
            films.Add(id);
        }

        var chosen = MovieTextRules.ChooseSeriesEntry(
            [.. episodes.Select(episode => new MovieTextRules.EpisodeFilms(episode.ID, episode.Type, episode.Title, filmsByEpisode.GetValueOrDefault(episode.ID) ?? []))],
            film =>
            [
                .. _metadataService.GetCollectionsWith(film).Select(collection =>
                {
                    _memos.Record(collection.ID);
                    entries.TryAdd(collection.ID, collection);
                    return collection.ID;
                }),
            ]
        );
        return chosen is null ? null : entries.GetValueOrDefault(chosen);
    }

    #endregion

    #region Groups

    /// <summary>
    ///   The name behind <see cref="AnimeGroup.GroupName"/>: the one a user
    ///   gave the group, else its main series' title.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when no user named the group and it has no series to take a
    ///   name from.
    /// </exception>
    /// <returns>The name.</returns>
    internal string GroupNameOf(AnimeGroup group)
        => _memos.TryGet<string>(ref group.TextMemo, TextMemoSlot.Name, Generation, out var remembered) ? remembered : WorkGroupNameOf(group);

    /// <summary>
    ///   Works out, and remembers, what <see cref="GroupNameOf(AnimeGroup)"/> hands back.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when no user named the group and it has no series to take a
    ///   name from, or when working the name out needs the name itself.
    /// </exception>
    /// <returns>The value.</returns>
    private string WorkGroupNameOf(AnimeGroup group)
        => _memos.Get<string>(((IMetadata)group).ID, TextMemoSlot.Name, Generation, () =>
        {
            if (CustomTitleOf(((IMetadata)group).ID) is { Value: var custom } && !string.IsNullOrWhiteSpace(custom))
                return custom;

            return RequireMainSeriesOf(group).Title;
        }, () => throw new InvalidOperationException($"Group {group.AnimeGroupID}'s name depends on itself."), ref group.TextMemo);

    /// <summary>
    ///   A group's main series, for a text the group cannot go without.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the group has no series.
    /// </exception>
    /// <returns>The main series.</returns>
    private AnimeSeries RequireMainSeriesOf(AnimeGroup group)
        => MainSeriesOf(group) ?? throw new InvalidOperationException($"Group {group.AnimeGroupID} has no series to take its name from.");

    /// <summary>
    ///   The overview behind <see cref="AnimeGroup.Description"/>: the one a
    ///   user gave the group, else its main series' preferred overview.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The overview, or an empty one when there is none.</returns>
    internal string GroupOverviewOf(AnimeGroup group)
        => _memos.TryGet<string>(ref group.TextMemo, TextMemoSlot.Overview, Generation, out var remembered) ? remembered : WorkGroupOverviewOf(group);

    /// <summary>
    ///   Works out, and remembers, what <see cref="GroupOverviewOf(AnimeGroup)"/> hands back.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The value.</returns>
    private string WorkGroupOverviewOf(AnimeGroup group)
        => _memos.Get<string>(((IMetadata)group).ID, TextMemoSlot.Overview, Generation, () =>
        {
            if (CustomOverviewOf(((IMetadata)group).ID) is { } custom)
                return custom.Value;

            return MainSeriesOf(group)?.PreferredOverview?.Value ?? string.Empty;
        }, () => string.Empty, ref group.TextMemo);

    /// <summary>
    ///   A group's main series, recording every group and series it was chosen
    ///   among, so a change to any of them works the group's texts out again.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The main series, or <c>null</c> when the group has no series.</returns>
    private AnimeSeries? MainSeriesOf(AnimeGroup group)
    {
        RecordShapeOf(group);
        return group.MainSeries;
    }

    /// <summary>
    ///   Records a group, the groups below it and every series in them, with
    ///   their AniDB anime, which decide which series is the group's main one.
    /// </summary>
    /// <param name="group">The group.</param>
    internal void RecordShapeOf(AnimeGroup group)
    {
        if (!_memos.IsRecording)
            return;

        var pending = new Stack<AnimeGroup>([group]);
        while (pending.TryPop(out var current))
        {
            _memos.Record(((IMetadata)current).ID);
            foreach (var series in current.Series)
            {
                _memos.Record(((IMetadata)series).ID);
                _memos.Record(AnidbAnimeID(series.AniDB_ID));
            }

            foreach (var child in current.Children)
                pending.Push(child);
        }
    }

    #endregion

    #region Seasons

    /// <summary>
    ///   The list behind a season's <see cref="IWithTitles.Titles"/>, ahead
    ///   of the series' own.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <returns>What was contributed to the season, then what its linked seasons are called.</returns>
    internal IReadOnlyList<ITitle> SeasonTitlesOf(AnimeSeason season)
        => Guarded<IReadOnlyList<ITitle>>(season, () =>
        [
            .. GetContributedTitles(season),
            .. OnePerSource(SeasonLinks(season)).SelectMany(entry => WithoutMain(Titles(entry))),
        ], []);

    /// <summary>
    ///   The list behind a season's <see cref="IWithOverviews.Overviews"/>,
    ///   ahead of the series' own.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <returns>What was contributed to the season, then what its linked seasons say.</returns>
    internal IReadOnlyList<IText> SeasonDescriptionsOf(AnimeSeason season)
        => Guarded<IReadOnlyList<IText>>(season, () =>
        [
            .. GetContributedOverviews(season),
            .. OnePerSource(SeasonLinks(season)).SelectMany(Descriptions),
        ], []);

    private IReadOnlyList<IMetadataCrossReference> SeasonLinks(AnimeSeason season)
        => _metadataService is MetadataService service
            ? Links(AnidbAnimeID(((IShokoSeason)season).Series is IShokoSeries series ? series.AnidbAnimeID : 0), service.GetSeasonCrossReferences(season))
            : [];

    #endregion

    #region Episodes

    /// <summary>
    ///   The walk behind the episode's <see cref="IWithTitles.PreferredTitle"/>.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The preferred title, or the episode's default one.</returns>
    internal ITitle? PreferredTitleOf(AnimeEpisode episode)
        => _memos.TryGet<ITitle?>(ref episode.TextMemo, TextMemoSlot.PreferredTitle, Generation, out var remembered) ? remembered : WorkPreferredTitleOf(episode);

    /// <summary>
    ///   Works out, and remembers, what <see cref="PreferredTitleOf(AnimeEpisode)"/> hands back.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The value.</returns>
    private ITitle? WorkPreferredTitleOf(AnimeEpisode episode)
        => _memos.Get<ITitle?>(((IMetadata)episode).ID, TextMemoSlot.PreferredTitle, Generation, () => ChoosePreferredTitleOf(episode), () => episode.DefaultTitle, ref episode.TextMemo);

    /// <summary>
    ///   Works out the episode's preferred title.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The preferred title, or the episode's default one.</returns>
    private ITitle? ChoosePreferredTitleOf(AnimeEpisode episode)
    {
        if (OverallPick(((IMetadata)episode).ID, TextKind.Title) is ITitle pick)
            return pick;

        return Guarded(episode, () =>
        {
            _memos.Record(AnidbEpisodeID(episode.AniDB_EpisodeID));
            var settings = ISettingsProvider.Instance.GetSettings();
            List<ITitle>? anidbTitles = null;
            var perSource = new Dictionary<MetadataSource, IReadOnlyList<ITitle>>();
            foreach (var language in Languages.PreferredEpisodeNamingLanguages)
                foreach (var source in settings.Language.EpisodeTitleSourceOrder)
                {
                    var title = source switch
                    {
                        // AniDB has no main episode title, so x-main reads its
                        // English one.
                        _ when source == MetadataSource.AniDB => PickAnidb(anidbTitles ??= [.. episode.AniDB_Episode?.GetTitles() ?? []],
                            language.Language is TitleLanguage.Main ? TitleLanguage.English : language.Language, settings.Language.UseSynonyms),
                        _ => Pick(Cached(perSource, source, () => EpisodeTitlesFrom(episode, source)), language.Language, settings.Language.UseSynonyms),
                    };
                    if (title is not null)
                        return title;
                }

            return episode.DefaultTitle;
        }, episode.DefaultTitle);
    }

    /// <summary>
    ///   The list behind the episode's <see cref="IWithTitles.Titles"/>.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>Every title: the user's, AniDB's, the linked entries' and the contributed ones.</returns>
    internal IReadOnlyList<ITitle> TitlesOf(AnimeEpisode episode)
    {
        var titles = new List<ITitle>();
        var custom = CustomTitleOf(((IMetadata)episode).ID);
        if (custom is not null)
            titles.Add(custom);

        titles.AddRange(DemoteMain(((IShokoEpisode)episode).AnidbEpisode.Titles, custom is not null, TitleType.None));
        Guarded(episode, () =>
        {
            titles.AddRange(GetContributedTitles(episode).Where(title => title.ID is null || title.ID != custom?.ID));
            titles.AddRange(LinkedEpisodesOf(episode).Where(entry => entry.Source != MetadataSource.AniDB).SelectMany(Titles));
            return true;
        }, false);

        return titles;
    }

    /// <summary>
    ///   The overview behind the episode's <see cref="IWithOverviews.DefaultOverview"/>.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>Its AniDB episode's description, or <c>null</c> when it has none.</returns>
    internal IText? DefaultOverviewOf(AnimeEpisode episode)
        => _memos.TryGet<IText?>(ref episode.TextMemo, TextMemoSlot.DefaultOverview, Generation, out var remembered) ? remembered : WorkDefaultOverviewOf(episode);

    /// <summary>
    ///   Works out, and remembers, what <see cref="DefaultOverviewOf(AnimeEpisode)"/> hands back.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The value.</returns>
    private IText? WorkDefaultOverviewOf(AnimeEpisode episode)
        => _memos.Get<IText?>(((IMetadata)episode).ID, TextMemoSlot.DefaultOverview, Generation, () =>
        {
            _memos.Record(AnidbEpisodeID(episode.AniDB_EpisodeID));
            return AnidbDescription(episode.AniDB_Episode?.Description, TitleLanguage.English);
        }, () => null, ref episode.TextMemo);

    /// <summary>
    ///   The walk behind the episode's <see cref="IWithOverviews.PreferredOverview"/>.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The preferred description, or <c>null</c> when none speaks a preferred language.</returns>
    internal IText? PreferredDescriptionOf(AnimeEpisode episode)
        => _memos.TryGet<IText?>(ref episode.TextMemo, TextMemoSlot.PreferredOverview, Generation, out var remembered) ? remembered : WorkPreferredDescriptionOf(episode);

    /// <summary>
    ///   Works out, and remembers, what <see cref="PreferredDescriptionOf(AnimeEpisode)"/> hands back.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The value.</returns>
    private IText? WorkPreferredDescriptionOf(AnimeEpisode episode)
        => _memos.Get<IText?>(((IMetadata)episode).ID, TextMemoSlot.PreferredOverview, Generation, () => ChoosePreferredDescriptionOf(episode), () => null, ref episode.TextMemo);

    /// <summary>
    ///   Works out the episode's preferred overview.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The preferred description, or <c>null</c> when none speaks a preferred language.</returns>
    private IText? ChoosePreferredDescriptionOf(AnimeEpisode episode)
        => OverallPick(((IMetadata)episode).ID, TextKind.Overview) ?? Guarded(episode, () =>
        {
            _memos.Record(AnidbEpisodeID(episode.AniDB_EpisodeID));
            var anidbDescription = episode.AniDB_Episode?.Description;
            var perSource = new Dictionary<MetadataSource, IReadOnlyList<IText>>();
            foreach (var language in Languages.PreferredDescriptionNamingLanguages)
                foreach (var source in ISettingsProvider.Instance.GetSettings().Language.DescriptionSourceOrder)
                {
                    var description = source switch
                    {
                        _ when source == MetadataSource.AniDB => AnidbDescription(anidbDescription, language.Language),
                        _ => Pick(Cached(perSource, source, () => EpisodeDescriptionsFrom(episode, source)), language.Language, useSynonyms: false),
                    };
                    if (description is not null)
                        return description;
                }

            return null;
        }, null);

    /// <summary>
    ///   The list behind the episode's <see cref="IWithOverviews.Overviews"/>.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>Every description: the linked entries' and the contributed ones.</returns>
    internal IReadOnlyList<IText> DescriptionsOf(AnimeEpisode episode)
        => Guarded<IReadOnlyList<IText>>(episode, () =>
        [
            .. GetContributedOverviews(episode),
            .. LinkedEpisodesOf(episode).SelectMany(Descriptions),
        ], []);

    /// <summary>
    ///   The entries an episode is linked to: its AniDB episode, then the
    ///   episodes every source links it to, as
    ///   <see cref="AnimeEpisode.LinkedEpisodes"/> lists them.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <returns>The linked episodes.</returns>
    private List<IEpisode> LinkedEpisodesOf(AnimeEpisode episode)
    {
        var linked = new List<IEpisode>();
        _memos.Record(AnidbEpisodeID(episode.AniDB_EpisodeID));
        if (episode.AniDB_Episode is { } anidbEpisode)
            linked.Add(anidbEpisode);
        linked.AddRange(Links(AnidbEpisodeID(episode.AniDB_EpisodeID), _metadataService.GetEpisodeCrossReferences(episode.AniDB_EpisodeID)).Select(link => link.Provider).OfType<IEpisode>());
        return linked;
    }

    /// <summary>
    ///   A source's titles for an episode: what it contributed, then those of
    ///   the entry that speaks for it, labelled when that is a shared film.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source.</param>
    /// <returns>The titles, without any claiming the main slot.</returns>
    private IReadOnlyList<ITitle> EpisodeTitlesFrom(AnimeEpisode episode, MetadataSource source)
    {
        var (entry, placeholder) = EpisodeEntry(episode, source);
        return [.. GetContributedTitles(episode, source), .. Labelled(WithoutMain(Titles(entry)), placeholder)];
    }

    /// <summary>
    ///   A source's descriptions for an episode, read off the same entry as
    ///   its titles.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source.</param>
    /// <returns>The descriptions.</returns>
    private IReadOnlyList<IText> EpisodeDescriptionsFrom(AnimeEpisode episode, MetadataSource source)
        => [.. GetContributedOverviews(episode, source), .. Descriptions(EpisodeEntry(episode, source).Entry)];

    /// <summary>
    ///   The entry that speaks for an episode on a source, as
    ///   <see cref="EpisodeEntry(int, int, string?, MetadataSource)"/> finds it.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="source">The source.</param>
    /// <returns>The entry, or <c>null</c> when none speaks, and the stand-in to label its titles with.</returns>
    private (IMetadata? Entry, MovieTextRules.Placeholder? Placeholder) EpisodeEntry(AnimeEpisode episode, MetadataSource source)
        => EpisodeEntry(episode.AniDB_Episode?.AnimeID ?? 0, episode.AniDB_EpisodeID, AnidbEnglishTitle(episode.AniDB_EpisodeID), source);

    /// <summary>
    ///   The entry that speaks for an episode on a source: the first stored
    ///   episode it links to, since a part of a longer episode is still that
    ///   episode, or failing any, the one stored film it stands for.
    /// </summary>
    /// <remarks>
    ///   A film other episodes of the anime are linked to as well speaks only
    ///   for an episode AniDB gave a stand-in name, such as <c>Part 1 of 2</c>,
    ///   and the stand-in's label is kept after the film's title. An episode
    ///   with a real name of its own keeps AniDB's text.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime the episode is in.</param>
    /// <param name="anidbEpisodeID">The AniDB episode.</param>
    /// <param name="anidbTitle">AniDB's English title for the episode, or <c>null</c> when it has none.</param>
    /// <param name="source">The source.</param>
    /// <returns>The entry, or <c>null</c> when none speaks, and the stand-in to label its titles with.</returns>
    internal (IMetadata? Entry, MovieTextRules.Placeholder? Placeholder) EpisodeEntry(int anidbAnimeID, int anidbEpisodeID, string? anidbTitle, MetadataSource source)
    {
        var episodeID = AnidbEpisodeID(anidbEpisodeID);
        if (Stored(Links(episodeID, _metadataService.GetEpisodeCrossReferences(anidbEpisodeID, source))).FirstOrDefault() is { } linked)
            return (linked, null);

        if (OnlyEntry(Links(episodeID, _metadataService.GetMovieCrossReferences(anidbEpisodeID, source))) is not { } film)
            return (null, null);

        var shared = Links(AnidbAnimeID(anidbAnimeID), _metadataService.GetMovieCrossReferencesForSeries(anidbAnimeID, source))
            .Any(link => link.AnidbEpisodeID != anidbEpisodeID && link.ProviderID == film.ID);
        if (!shared)
            return (film, null);

        return MovieTextRules.ParsePlaceholder(anidbTitle) is { } placeholder ? (film, placeholder) : (null, null);
    }

    /// <summary>
    ///   A shared film's titles with an episode's stand-in label kept after
    ///   each, e.g. <c>Vampire Hunter D (Part 1 of 2)</c>.
    /// </summary>
    /// <param name="titles">The film's titles.</param>
    /// <param name="placeholder">The episode's stand-in name, or <c>null</c> to leave the titles as they are.</param>
    /// <returns>The titles for the episode.</returns>
    internal static IReadOnlyList<ITitle> Labelled(IReadOnlyList<ITitle> titles, MovieTextRules.Placeholder? placeholder)
        => placeholder is null
            ? titles
            :
            [
                .. titles.Select(title => new TitleStub
                {
                    Source = title.Source,
                    Language = title.Language,
                    LanguageCode = title.LanguageCode,
                    CountryCode = title.CountryCode,
                    Value = MovieTextRules.LabelFilmTitle(title.Value, placeholder),
                    Type = title.Type,
                }),
            ];

    /// <summary>
    ///   AniDB's English title for an episode, which is where it names an
    ///   episode with a stand-in such as <c>Part 1 of 2</c>: its first English
    ///   title, else AniDB's generic one, <c>Episode {prefix}{number}</c>.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode.</param>
    /// <returns>The title, or <c>null</c> when the episode is not stored.</returns>
    private static string? AnidbEnglishTitle(int anidbEpisodeID)
        => RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID)?.EnglishTitle;

    #endregion

    #region Helpers

    /// <summary>
    ///   Whether a text is in a language, by its language or by its codes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="language">The language wanted.</param>
    /// <returns>Whether it is in that language.</returns>
    internal static bool Speaks(IText text, TitleLanguage language)
        => TextChooser.Speaks(text, language);

    /// <summary>
    ///   One source's text in a language, a title by the ranked rule and an
    ///   overview its first.
    /// </summary>
    /// <param name="texts">The source's text.</param>
    /// <param name="language">The language wanted.</param>
    /// <param name="useSynonyms">Whether a title other than a main, official or untyped one may answer.</param>
    /// <returns>The text, or <c>null</c> when none is in that language.</returns>
    internal static T? Pick<T>(IReadOnlyList<T> texts, TitleLanguage language, bool useSynonyms) where T : class, IText
    {
        if (language is TitleLanguage.Main || texts.Count is 0)
            return null;

        var matching = texts.Where(text => Speaks(text, language)).ToList();
        if (matching.Count is 0 || matching[0] is not ITitle)
            return matching.FirstOrDefault();
        return TextChooser.Ranked(matching.Cast<ITitle>(), useSynonyms) as T;
    }

    /// <summary>
    ///   Drops the titles a source marked as the main one. On a Shoko entry
    ///   <c>x-main</c> is AniDB's alone.
    /// </summary>
    /// <param name="titles">The titles as the source gave them.</param>
    /// <returns>The titles, without any claiming the main slot.</returns>
    internal static IReadOnlyList<ITitle> WithoutMain(IReadOnlyList<ITitle> titles)
        => titles.Count is 0 ? titles : [.. titles.Where(title => title.Language is not TitleLanguage.Main)];

    private static ITitle? PickAnidb(List<ITitle> titles, TitleLanguage language, bool useSynonyms)
        => TextChooser.Ranked(titles.Where(title => title.Language == language), useSynonyms);

    private static IText? AnidbDescription(string? description, TitleLanguage language)
        => language is TitleLanguage.English && !string.IsNullOrEmpty(description)
            ? new TextStub { Language = TitleLanguage.English, LanguageCode = "en", Value = description, Source = MetadataSource.AniDB }
            : null;

    /// <summary>
    ///   AniDB's titles, with its main one stepped down when the user's
    ///   title has taken its place.
    /// </summary>
    private static IEnumerable<ITitle> DemoteMain(IEnumerable<ITitle> titles, bool hasCustom, TitleType demotedTo)
    {
        foreach (var title in titles)
            yield return hasCustom && title.Type is TitleType.Main
                ? new TitleStub
                {
                    Language = title.Language,
                    LanguageCode = title.LanguageCode,
                    Value = title.Value,
                    CountryCode = title.CountryCode,
                    Source = title.Source,
                    Type = demotedTo,
                }
                : title;
    }

    private static IReadOnlyList<T> Cached<T>(Dictionary<MetadataSource, IReadOnlyList<T>> cache, MetadataSource source, Func<IReadOnlyList<T>> read)
    {
        if (!cache.TryGetValue(source, out var texts))
            cache[source] = texts = read();

        return texts;
    }

    /// <summary>
    ///   The stored entries the links point at, in link order. A link to an
    ///   entry that isn't stored has no text to read, so it is left out.
    /// </summary>
    private static IEnumerable<IMetadata> Stored(IEnumerable<IMetadataCrossReference> links)
        => links.Where(link => link.ProviderID is not null).Select(link => link.Provider).OfType<IMetadata>();

    /// <summary>
    ///   The only stored entry the links point at. Two entries is two answers,
    ///   and neither is the one; two links to the same entry, such as several
    ///   episodes of one film, are still one.
    /// </summary>
    internal static IMetadata? OnlyEntry(IEnumerable<IMetadataCrossReference> links)
        => Stored(links).DistinctBy(entry => entry.ID).Take(2).ToList() is [var only] ? only : null;

    private static IEnumerable<IMetadata> OnePerSource(IEnumerable<IMetadataCrossReference> links)
        => links.GroupBy(link => link.Source).Select(group => OnlyEntry(group)).OfType<IMetadata>();

    /// <summary>
    ///   The ID of an AniDB anime, which stands for its row and its links.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The ID.</returns>
    internal static MetadataGuid AnidbAnimeID(int animeID)
        => new(MetadataSource.AniDB, MetadataEntityType.Series, animeID.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    ///   The ID of an AniDB episode, which stands for its row and its links.
    /// </summary>
    /// <param name="episodeID">The AniDB episode ID.</param>
    /// <returns>The ID.</returns>
    internal static MetadataGuid AnidbEpisodeID(int episodeID)
        => new(MetadataSource.AniDB, MetadataEntityType.Episode, episodeID.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    ///   Records links as read: the AniDB entry they hang off, so a change to
    ///   its links works the value out again, and every entry they point at,
    ///   stored yet or not.
    /// </summary>
    /// <typeparam name="T">The kind of link list.</typeparam>
    /// <param name="owner">The AniDB anime or episode the links belong to.</param>
    /// <param name="links">The links.</param>
    /// <returns>The links.</returns>
    private T Links<T>(MetadataGuid owner, T links) where T : IEnumerable<IMetadataCrossReference>
    {
        if (!_memos.IsRecording)
            return links;

        _memos.Record(owner);
        foreach (var link in links)
            if (link.ProviderID is { } providerID)
                _memos.Record(providerID);
        return links;
    }

    private IReadOnlyList<ITitle> Titles(IMetadata? entry)
        => Read(entry, withTitles => (withTitles as IWithTitles)?.Titles);

    private IReadOnlyList<IText> Descriptions(IMetadata? entry)
        => Read(entry, withOverviews => (withOverviews as IWithOverviews)?.Overviews);

    /// <summary>
    ///   Reads text off a linked entry, where a throwing entry costs only its
    ///   own text rather than the Shoko entry's.
    /// </summary>
    private IReadOnlyList<T> Read<T>(IMetadata? entry, Func<IMetadata, IReadOnlyList<T>?> read) where T : IText
    {
        if (entry is null)
            return [];

        _memos.Record(entry.ID);
        try
        {
            return [.. (read(entry) ?? []).Where(text => text.Source == entry.Source)];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reading text off {Entry} threw and was skipped.", entry.ID);
            return [];
        }
    }

    /// <summary>
    ///   Runs a read unless this thread is already reading the same entry.
    /// </summary>
    private static T Guarded<T>(object entry, Func<T> read, T fallback)
    {
        _resolving ??= new(ReferenceEqualityComparer.Instance);
        if (!_resolving.Add(entry))
            return fallback;

        try
        {
            return read();
        }
        finally
        {
            _resolving.Remove(entry);
        }
    }

    #endregion
}
