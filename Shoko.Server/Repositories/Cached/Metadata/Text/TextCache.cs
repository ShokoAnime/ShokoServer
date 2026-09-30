using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#pragma warning disable CS0618
namespace Shoko.Server.Repositories.Cached.Metadata.Text;

/// <summary>
///   Every stored title and overview, held in memory: per entry one sorted
///   array of compact rows, replaced whole on every write so readers never
///   lock. Title values are pooled at load; overview values are read from the
///   database when asked for and kept in a bounded cache.
/// </summary>
/// <remarks>
///   Loaded with one plain query per table at startup, with no NHibernate
///   objects. Writes go through <see cref="MetadataRowWriter"/> and reach the
///   cache through <see cref="Apply"/> once they have committed.
/// </remarks>
public sealed class TextCache : ICachedRepository
{
    #region Fields

    /// <summary>
    ///   How many overview characters are kept by default, about 64 MB.
    /// </summary>
    internal const long DefaultOverviewCapacity = 32_000_000;

    /// <summary>
    ///   How many overview IDs one query asks for.
    /// </summary>
    private const int OverviewBatchSize = 500;

    private readonly DatabaseFactory? _databaseFactory;

    private readonly ILogger _logger;

    private readonly Lock _writeLock = new();

    private readonly Lock _indexLock = new();

    private MetadataSource[] _sources = [];

    private readonly Dictionary<MetadataSource, byte> _sourceIndexes = [];

    private MetadataEntityType[] _entityTypes = [null!];

    private readonly Dictionary<MetadataEntityType, byte> _entityTypeIndexes = [];

    private TextLocale[] _locales = [];

    private readonly Dictionary<TextLocale, ushort> _localeIndexes = [];

    private State _state = new();

    private readonly OverviewBodyCache _overviews;

    #endregion

    #region Constructors

    /// <summary>
    ///   The cache of the running server, loaded from its database.
    /// </summary>
    /// <param name="databaseFactory">The database factory.</param>
    /// <param name="logger">Logs what was loaded.</param>
    public TextCache(DatabaseFactory databaseFactory, ILogger<TextCache> logger)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _overviews = new(DefaultOverviewCapacity);
    }

    /// <summary>
    ///   A cache with no database behind it, which keeps every overview value
    ///   it is given, for tests and tools.
    /// </summary>
    internal TextCache()
    {
        _logger = NullLogger<TextCache>.Instance;
        _overviews = new(null);
    }

    #endregion

    #region State

    /// <summary>
    ///   Everything a load builds, swapped in whole.
    /// </summary>
    private sealed class State
    {
        internal readonly TextEntityTable Entries = new();

        /// <summary>
        ///   The picks of every stored text a user picked, by the picked text.
        /// </summary>
        internal readonly Dictionary<(TextKind Kind, int ID), List<(TextEntityKey Owner, int ID)>> Picks = [];
    }

    /// <summary>
    ///   Raised once a write has reached the cache, with what it changed.
    /// </summary>
    internal event EventHandler<IReadOnlyList<TextEntityChange>>? Changed;

    /// <summary>
    ///   Raised once the cache was loaded again from the database, which may
    ///   have changed any entry's texts.
    /// </summary>
    internal event EventHandler? Reloaded;

    /// <summary>
    ///   How many entries have stored texts.
    /// </summary>
    internal int EntryCount => Volatile.Read(ref _state).Entries.Enumerate().Count();

    /// <summary>
    ///   How many overview characters are held in memory.
    /// </summary>
    internal long OverviewCharactersHeld => _overviews.Size;

    #endregion

    #region Reading

    /// <summary>
    ///   Every stored text of an entry, titles first, then by source and in
    ///   the order each source gave them.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <returns>The rows; empty when the entry has none.</returns>
    internal TextRow[] GetRows(MetadataGuid entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return KeyOf(entity, create: false) is { } key ? Volatile.Read(ref _state).Entries.Get(key) ?? [] : [];
    }

    /// <summary>
    ///   The stored titles of an entry.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <param name="source">Optional. Only this source's titles.</param>
    /// <param name="isEnabled">Optional. Only enabled or only disabled titles; both when left out.</param>
    /// <returns>The titles, by source and in the order each source gave them.</returns>
    internal IReadOnlyList<ITitle> GetTitles(MetadataGuid entity, MetadataSource? source = null, bool? isEnabled = null)
    {
        if (!TrySourceFilter(source, out var sourceIndex))
            return [];

        var titles = new List<ITitle>();
        foreach (var row in GetRows(entity))
            if (row.Kind is TextKind.Title && Matches(row, sourceIndex, isEnabled))
                titles.Add((ITitle)ToText(entity, row, row.Value));

        return titles;
    }

    /// <summary>
    ///   The stored overviews of an entry, reading the values it lacks from
    ///   the database in one query.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <param name="source">Optional. Only this source's overviews.</param>
    /// <param name="isEnabled">Optional. Only enabled or only disabled overviews; both when left out.</param>
    /// <returns>The overviews, by source and in the order each source gave them.</returns>
    internal IReadOnlyList<IText> GetOverviews(MetadataGuid entity, MetadataSource? source = null, bool? isEnabled = null)
    {
        if (!TrySourceFilter(source, out var sourceIndex))
            return [];

        var rows = GetRows(entity).Where(row => row.Kind is TextKind.Overview && Matches(row, sourceIndex, isEnabled)).ToList();
        if (rows.Count is 0)
            return [];

        var values = OverviewValues(rows.Select(row => row.ID));
        return [.. rows.Select(row => ToText(entity, row, values.GetValueOrDefault(row.ID, string.Empty)))];
    }

    /// <summary>
    ///   The value of the title one source set for an entry as its overall
    ///   preferred title, such as the name the core gives an AniDB tag.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <param name="source">The source that set it.</param>
    /// <returns>The first such enabled title's value, or <c>null</c> when there is none.</returns>
    internal string? OverallTitleValue(MetadataGuid entity, MetadataSource source)
    {
        var rows = GetRows(entity);
        if (rows.Length is 0 || !TrySourceFilter(source, out var sourceIndex) || sourceIndex < 0)
            return null;

        foreach (var row in rows)
            if (row is { Kind: TextKind.Title, IsEnabled: true, Preference: TextPreference.Overall } && row.Source == sourceIndex)
                return row.Value;

        return null;
    }

    /// <summary>
    ///   The value of a stored text.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The title's value, or the overview's, read from the database when it is not held.</returns>
    internal string ValueOf(in TextRow row)
        => row.Kind is TextKind.Title ? row.Value ?? string.Empty : OverviewValues([row.ID]).GetValueOrDefault(row.ID, string.Empty);

    /// <summary>
    ///   The values of some overviews, reading those not held from the
    ///   database in batches.
    /// </summary>
    /// <param name="ids">The overviews' IDs.</param>
    /// <returns>The values found, by ID.</returns>
    internal IReadOnlyDictionary<int, string> OverviewValues(IEnumerable<int> ids)
    {
        var values = new Dictionary<int, string>();
        var missing = new List<int>();
        foreach (var id in ids)
        {
            if (values.ContainsKey(id))
                continue;
            if (_overviews.TryGet(id, out var value))
                values[id] = value;
            else
                missing.Add(id);
        }

        if (missing.Count is 0 || _databaseFactory is null)
            return values;

        // A write may have put a newer value while the read ran, and that one
        // wins over the value read.
        foreach (var (id, value) in LoadOverviewValues(missing))
            values[id] = _overviews.GetOrAdd(id, value);

        return values;
    }

    /// <summary>
    ///   Hands a row out as a detached text.
    /// </summary>
    /// <param name="entity">The entry the row belongs to.</param>
    /// <param name="row">The row.</param>
    /// <param name="value">The text's value.</param>
    /// <returns>A <see cref="StoredTitle"/> for a title, else a <see cref="StoredText"/>.</returns>
    internal StoredText ToText(MetadataGuid entity, in TextRow row, string? value)
    {
        var locale = LocaleOf(row);
        return row.Kind is TextKind.Title
            ? new StoredTitle
            {
                ID = row.ID,
                EntityID = entity,
                Source = SourceOf(row),
                Language = locale.Language,
                LanguageCode = locale.LanguageCode,
                CountryCode = locale.CountryCode,
                ScriptCode = locale.ScriptCode,
                Value = value ?? string.Empty,
                Type = row.TitleType,
                IsEnabled = row.IsEnabled,
                Preference = row.Preference,
                Ordering = row.Ordering,
                ReferenceID = row.ReferenceID is 0 ? null : row.ReferenceID,
            }
            : new StoredText
            {
                ID = row.ID,
                EntityID = entity,
                Source = SourceOf(row),
                Language = locale.Language,
                LanguageCode = locale.LanguageCode,
                CountryCode = locale.CountryCode,
                ScriptCode = locale.ScriptCode,
                Value = value ?? string.Empty,
                IsEnabled = row.IsEnabled,
                Preference = row.Preference,
                Ordering = row.Ordering,
                ReferenceID = row.ReferenceID is 0 ? null : row.ReferenceID,
            };
    }

    /// <summary>
    ///   A detached database row for a stored text, to change and write back.
    /// </summary>
    /// <param name="entity">The entry the row belongs to.</param>
    /// <param name="row">The row.</param>
    /// <param name="value">The row's value when the caller has it; read when left out.</param>
    /// <returns>The row, with its current value.</returns>
    internal MetadataTextRow ToRow(MetadataGuid entity, in TextRow row, string? value = null)
    {
        var locale = LocaleOf(row);
        var stored = MetadataTextRow.Create(row.Kind);
        stored.RowID = row.ID;
        stored.EntryID = entity;
        stored.Source = SourceOf(row);
        stored.Language = locale.Language;
        stored.LanguageCode = locale.LanguageCode;
        stored.CountryCode = locale.CountryCode;
        stored.ScriptCode = locale.ScriptCode;
        stored.TitleType = row.TitleType;
        stored.Value = value ?? ValueOf(row);
        stored.IsEnabled = row.IsEnabled;
        stored.Preference = row.Preference;
        stored.Ordering = row.Ordering;
        stored.ReferenceID = row.ReferenceID is 0 ? null : row.ReferenceID;
        return stored;
    }

    /// <summary>
    ///   The source that wrote a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The source.</returns>
    internal MetadataSource SourceOf(in TextRow row)
        => Volatile.Read(ref _sources)[row.Source];

    /// <summary>
    ///   The language and codes of a row.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The language and codes.</returns>
    internal TextLocale LocaleOf(in TextRow row)
        => Volatile.Read(ref _locales)[row.Locale];

    /// <summary>
    ///   Every entry with stored texts, and its rows.
    /// </summary>
    /// <returns>The entries, in no particular order.</returns>
    internal IEnumerable<(MetadataGuid Entity, TextRow[] Rows)> Enumerate()
    {
        foreach (var (key, rows) in Volatile.Read(ref _state).Entries.Enumerate())
            yield return (EntityOf(key), rows);
    }

    /// <summary>
    ///   Every stored text, titles and overviews, of every entry.
    /// </summary>
    /// <returns>The texts, entry by entry.</returns>
    internal IReadOnlyList<StoredText> GetAll()
        => [.. Enumerate().SelectMany(entry => entry.Rows.Select(row => ToText(entry.Entity, row, ValueOf(row))))];

    /// <summary>
    ///   Finds a stored text by its ID, looking through every entry.
    /// </summary>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="id">The text's ID.</param>
    /// <returns>The entry and the row, or <c>null</c> when no text has that ID.</returns>
    internal (MetadataGuid Entity, TextRow Row)? Find(TextKind kind, int id)
    {
        foreach (var (key, rows) in Volatile.Read(ref _state).Entries.Enumerate())
            foreach (var row in rows)
                if (row.ID == id && row.Kind == kind)
                    return (EntityOf(key), row);

        return null;
    }

    /// <summary>
    ///   The users' picks of a stored text.
    /// </summary>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="id">The picked text's ID.</param>
    /// <returns>The entries holding a pick of it, and the picks' IDs.</returns>
    internal IReadOnlyList<(MetadataGuid Entity, int ID)> PicksOf(TextKind kind, int id)
    {
        var state = Volatile.Read(ref _state);
        lock (_writeLock)
            return state.Picks.TryGetValue((kind, id), out var picks) ? [.. picks.Select(pick => (EntityOf(pick.Owner), pick.ID))] : [];
    }

    /// <summary>
    ///   The index a source filter matches rows by.
    /// </summary>
    /// <param name="source">The source, or <c>null</c> for any.</param>
    /// <param name="index">The source's index, or <c>-1</c> for any source.</param>
    /// <returns><c>false</c> when no stored text is from the source.</returns>
    private bool TrySourceFilter(MetadataSource? source, out int index)
    {
        index = -1;
        if (source is null)
            return true;

        lock (_indexLock)
        {
            if (!_sourceIndexes.TryGetValue(source, out var found))
                return false;

            index = found;
            return true;
        }
    }

    private static bool Matches(in TextRow row, int sourceIndex, bool? isEnabled)
        => (isEnabled is null || row.IsEnabled == isEnabled) && (sourceIndex < 0 || row.Source == sourceIndex);

    #endregion

    #region Writing

    /// <summary>
    ///   Brings the cache in line with a write that has committed.
    /// </summary>
    /// <param name="saved">The rows written, each with its ID.</param>
    /// <param name="deleted">The rows removed.</param>
    /// <returns>What changed, per entry.</returns>
    internal IReadOnlyList<TextEntityChange> Apply(IReadOnlyCollection<MetadataTextRow> saved, IReadOnlyCollection<MetadataTextRow> deleted)
    {
        if (saved.Count is 0 && deleted.Count is 0)
            return [];

        var changes = new Dictionary<MetadataGuid, (HashSet<TextKind> Kinds, HashSet<MetadataSource> Sources)>();
        lock (_writeLock)
        {
            var state = _state;
            foreach (var group in deleted.Select(row => (Row: row, Saving: false)).Concat(saved.Select(row => (Row: row, Saving: true))).GroupBy(item => item.Row.EntryID))
            {
                var key = KeyOf(group.Key, create: true)!.Value;
                var rows = new List<TextRow>(state.Entries.Get(key) ?? []);
                if (!changes.TryGetValue(group.Key, out var change))
                    changes[group.Key] = change = ([], []);

                foreach (var (row, saving) in group)
                {
                    var index = rows.FindIndex(existing => existing.ID == row.RowID && existing.Kind == row.Kind);
                    if (index >= 0)
                    {
                        Unpick(state, row.Kind, rows[index], key);
                        change.Sources.Add(SourceOf(rows[index]));
                        rows.RemoveAt(index);
                    }

                    change.Kinds.Add(row.Kind);
                    change.Sources.Add(row.Source);
                    if (!saving)
                    {
                        if (row.Kind is TextKind.Overview)
                            _overviews.Remove(row.RowID);
                        continue;
                    }

                    var textRow = ToTextRow(row, pool: null);
                    rows.Add(textRow);
                    Pick(state, row.Kind, textRow, key);
                    if (row.Kind is TextKind.Overview)
                        _overviews.Put(row.RowID, row.Value);
                }

                state.Entries.Set(key, Sorted(rows));
            }
        }

        return [.. changes.Select(pair => new TextEntityChange(pair.Key, pair.Value.Kinds, pair.Value.Sources))];
    }

    /// <summary>
    ///   Tells every listener what a write changed. Called by the writer once
    ///   it has let go of its own lock.
    /// </summary>
    /// <param name="changes">What changed, per entry.</param>
    internal void Notify(IReadOnlyList<TextEntityChange> changes)
    {
        if (changes.Count is not 0)
            Changed?.Invoke(this, changes);
    }

    private static void Pick(State state, TextKind kind, in TextRow row, TextEntityKey owner)
    {
        if (row.ReferenceID is 0)
            return;

        if (!state.Picks.TryGetValue((kind, row.ReferenceID), out var picks))
            state.Picks[(kind, row.ReferenceID)] = picks = [];
        picks.Add((owner, row.ID));
    }

    private static void Unpick(State state, TextKind kind, in TextRow row, TextEntityKey owner)
    {
        if (row.ReferenceID is 0 || !state.Picks.TryGetValue((kind, row.ReferenceID), out var picks))
            return;

        var id = row.ID;
        picks.RemoveAll(pick => pick.ID == id && pick.Owner == owner);
        if (picks.Count is 0)
            state.Picks.Remove((kind, row.ReferenceID));
    }

    /// <summary>
    ///   Sorts an entry's rows the way they are read: titles first, then by
    ///   source, then in the order each source gave them.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <returns>The sorted rows.</returns>
    private TextRow[] Sorted(List<TextRow> rows)
    {
        var sources = Volatile.Read(ref _sources);
        var sorted = rows.ToArray();
        Array.Sort(sorted, (a, b) =>
        {
            var byKind = a.Kind.CompareTo(b.Kind);
            if (byKind is not 0)
                return byKind;
            var bySource = a.Source == b.Source ? 0 : sources[a.Source].CompareTo(sources[b.Source]);
            if (bySource is not 0)
                return bySource;
            var byOrdering = a.Ordering.CompareTo(b.Ordering);
            return byOrdering is not 0 ? byOrdering : a.ID.CompareTo(b.ID);
        });
        return sorted;
    }

    /// <summary>
    ///   Turns a database row into the cache's compact row.
    /// </summary>
    /// <param name="row">The database row.</param>
    /// <param name="pool">Pools title values while loading, or <c>null</c>.</param>
    /// <returns>The compact row.</returns>
    private TextRow ToTextRow(MetadataTextRow row, Dictionary<string, string>? pool)
        => new(
            row.RowID,
            row.Kind,
            SourceIndex(row.Source),
            LocaleIndex(new(row.Language, row.LanguageCode, row.CountryCode, row.ScriptCode)),
            row.Kind is TextKind.Title ? row.TitleType : TitleType.None,
            row.IsEnabled,
            row.Preference,
            row.Ordering,
            row.ReferenceID ?? 0,
            row.Kind is TextKind.Title ? Pooled(pool, row.Value) : null
        );

    private static string Pooled(Dictionary<string, string>? pool, string value)
    {
        if (pool is null)
            return value;
        if (pool.TryGetValue(value, out var pooled))
            return pooled;

        pool[value] = value;
        return value;
    }

    #endregion

    #region Keys and Indexes

    /// <summary>
    ///   The key an entry is held under.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <param name="create">Whether to give its source and kind an index when they have none yet.</param>
    /// <returns>The key, or <c>null</c> when the entry cannot have texts yet.</returns>
    private TextEntityKey? KeyOf(MetadataGuid entity, bool create)
    {
        byte source;
        byte entityType;
        if (create)
        {
            source = SourceIndex(entity.Source);
            entityType = EntityTypeIndex(entity.EntityType);
        }
        else
        {
            lock (_indexLock)
            {
                if (!_sourceIndexes.TryGetValue(entity.Source, out source) || !_entityTypeIndexes.TryGetValue(entity.EntityType, out entityType))
                    return null;
            }
        }

        return TextEntityKey.Create(source, entityType, entity.ID);
    }

    /// <summary>
    ///   The entry a key stands for.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The entry.</returns>
    private MetadataGuid EntityOf(TextEntityKey key)
        => new(
            Volatile.Read(ref _sources)[key.Source],
            Volatile.Read(ref _entityTypes)[key.EntityType],
            key.Name ?? key.Number.ToString(CultureInfo.InvariantCulture)
        );

    private byte SourceIndex(MetadataSource source)
    {
        lock (_indexLock)
        {
            if (_sourceIndexes.TryGetValue(source, out var index))
                return index;
            if (_sources.Length > byte.MaxValue)
                throw new InvalidOperationException("The text cache holds texts from more sources than it can index.");

            index = (byte)_sources.Length;
            _sourceIndexes[source] = index;
            Volatile.Write(ref _sources, [.. _sources, source]);
            return index;
        }
    }

    private byte EntityTypeIndex(MetadataEntityType entityType)
    {
        lock (_indexLock)
        {
            if (_entityTypeIndexes.TryGetValue(entityType, out var index))
                return index;
            if (_entityTypes.Length > byte.MaxValue)
                throw new InvalidOperationException("The text cache holds texts of more kinds of entry than it can index.");

            index = (byte)_entityTypes.Length;
            _entityTypeIndexes[entityType] = index;
            Volatile.Write(ref _entityTypes, [.. _entityTypes, entityType]);
            return index;
        }
    }

    private ushort LocaleIndex(TextLocale locale)
    {
        lock (_indexLock)
        {
            if (_localeIndexes.TryGetValue(locale, out var index))
                return index;
            if (_locales.Length > ushort.MaxValue)
                throw new InvalidOperationException("The text cache holds texts in more languages than it can index.");

            index = (ushort)_locales.Length;
            _localeIndexes[locale] = index;
            Volatile.Write(ref _locales, [.. _locales, locale]);
            return index;
        }
    }

    #endregion

    #region Loading

    /// <inheritdoc />
    public void Populate(bool displayName = true, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested || _databaseFactory is null)
            return;

        using var session = _databaseFactory.SessionFactory.OpenStatelessSession();
        Load(session.Connection, displayName);
    }

    /// <inheritdoc />
    public void Populate(ISessionWrapper session, bool displayName = true, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        Load(session.Connection, displayName);
    }

    /// <summary>
    ///   Reads both tables into a fresh state, and swaps it in.
    /// </summary>
    /// <param name="connection">An open connection, or one to open.</param>
    /// <param name="displayName">Whether to say so on the startup screen.</param>
    internal void Load(IDbConnection connection, bool displayName = false)
    {
        if (displayName)
            ISystemService.StaticServices.GetRequiredService<SystemService>().StartupMessage = "Database Cache - Caching  - Titles and Overviews...";

        if (connection.State is not ConnectionState.Open)
            connection.Open();

        var stopwatch = Stopwatch.StartNew();
        var timeout = _databaseFactory is null ? 0 : ISettingsProvider.Instance.GetSettings().CachingDatabaseTimeout;
        var grouped = new Dictionary<TextEntityKey, List<TextRow>>();
        var pool = new Dictionary<string, string>(StringComparer.Ordinal);
        var titles = LoadTable(connection, TextKind.Title, timeout, grouped, pool);
        var overviews = LoadTable(connection, TextKind.Overview, timeout, grouped, pool);

        var state = new State();
        lock (_writeLock)
        {
            foreach (var (key, rows) in grouped)
            {
                var sorted = Sorted(rows);
                state.Entries.Set(key, sorted);
                foreach (var row in sorted)
                    Pick(state, row.Kind, row, key);
            }

            Volatile.Write(ref _state, state);
            _overviews.Clear();
        }

        Reloaded?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation(
            "Loaded {Titles} titles ({Distinct} distinct values) and {Overviews} overviews for {Entries} entries in {Elapsed} ms.",
            titles,
            pool.Count,
            overviews,
            grouped.Count,
            stopwatch.ElapsedMilliseconds
        );
    }

    /// <summary>
    ///   Streams one table into per-entry lists. Overview values are not read.
    /// </summary>
    /// <param name="connection">The open connection.</param>
    /// <param name="kind">Which table.</param>
    /// <param name="timeout">The command timeout, in seconds; <c>0</c> for the provider's default.</param>
    /// <param name="grouped">The lists, by entry.</param>
    /// <param name="pool">Pools the title values.</param>
    /// <returns>How many rows were read.</returns>
    private int LoadTable(IDbConnection connection, TextKind kind, int timeout, Dictionary<TextEntityKey, List<TextRow>> grouped, Dictionary<string, string> pool)
    {
        var table = kind is TextKind.Title ? "Metadata_Title" : "Metadata_Overview";
        var titleColumns = kind is TextKind.Title ? ", TitleType, Value" : string.Empty;
        using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT {table}ID, EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, IsEnabled, Preference, Ordering, ReferenceID{titleColumns} FROM {table}";
        if (timeout > 0)
            command.CommandTimeout = timeout;

        // The stored numbers and codes repeat on almost every row, so each is
        // turned into the cache's own index once.
        var sources = new int[256];
        var entityTypes = new int[256];
        Array.Fill(sources, -1);
        Array.Fill(entityTypes, -1);
        var locales = new Dictionary<(string Language, string LanguageCode, string? CountryCode, string? ScriptCode), ushort>();
        var count = 0;
        using var reader = command.ExecuteReader();
        var readers = Enumerable.Range(0, reader.FieldCount).Select(index => IntegerReader(reader.GetFieldType(index))).ToArray();
        while (reader.Read())
        {
            var entitySourceNumber = (byte)readers[1](reader, 1);
            var entityTypeNumber = (byte)readers[2](reader, 2);
            var sourceNumber = (byte)readers[4](reader, 4);
            if (sources[entitySourceNumber] < 0)
                sources[entitySourceNumber] = SourceIndex(MetadataNumberRegistry.GetSource(entitySourceNumber));
            if (sources[sourceNumber] < 0)
                sources[sourceNumber] = SourceIndex(MetadataNumberRegistry.GetSource(sourceNumber));
            if (entityTypes[entityTypeNumber] < 0)
                entityTypes[entityTypeNumber] = EntityTypeIndex(MetadataNumberRegistry.GetEntityType(entityTypeNumber));

            var codes = (reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8));
            if (!locales.TryGetValue(codes, out var locale))
                locales[codes] = locale = LocaleIndex(new(codes.Item1.GetTitleLanguage(), codes.Item2, codes.Item3, codes.Item4));

            var row = new TextRow(
                (int)readers[0](reader, 0),
                kind,
                (byte)sources[sourceNumber],
                locale,
                kind is TextKind.Title ? (TitleType)readers[13](reader, 13) : TitleType.None,
                readers[9](reader, 9) is not 0,
                (TextPreference)readers[10](reader, 10),
                (int)readers[11](reader, 11),
                reader.IsDBNull(12) ? 0 : (int)readers[12](reader, 12),
                kind is TextKind.Title ? Pooled(pool, reader.GetString(14)) : null
            );

            var key = TextEntityKey.Create((byte)sources[entitySourceNumber], (byte)entityTypes[entityTypeNumber], reader.GetString(3));
            if (!grouped.TryGetValue(key, out var rows))
                grouped[key] = rows = [];
            rows.Add(row);
            count++;
        }

        return count;
    }

    /// <summary>
    ///   Reads an integer column of whatever type the provider reports, without
    ///   boxing the common ones.
    /// </summary>
    /// <param name="type">The column's type.</param>
    /// <returns>The reader.</returns>
    private static Func<IDataRecord, int, long> IntegerReader(Type type)
    {
        if (type == typeof(long))
            return (record, index) => record.GetInt64(index);
        if (type == typeof(int))
            return (record, index) => record.GetInt32(index);
        if (type == typeof(short))
            return (record, index) => record.GetInt16(index);
        if (type == typeof(byte))
            return (record, index) => record.GetByte(index);
        if (type == typeof(bool))
            return (record, index) => record.GetBoolean(index) ? 1 : 0;

        return (record, index) => record.GetValue(index) switch
        {
            bool flag => flag ? 1 : 0,
            string text => long.Parse(text, CultureInfo.InvariantCulture),
            var value => Convert.ToInt64(value, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    ///   Reads overview values from the database.
    /// </summary>
    /// <param name="ids">The overviews' IDs.</param>
    /// <returns>The values found.</returns>
    private IEnumerable<(int ID, string Value)> LoadOverviewValues(IReadOnlyList<int> ids)
    {
        var found = new List<(int, string)>(ids.Count);
        using var session = _databaseFactory!.SessionFactory.OpenStatelessSession();
        var connection = session.Connection;
        if (connection.State is not ConnectionState.Open)
            connection.Open();

        foreach (var batch in ids.Distinct().Chunk(OverviewBatchSize))
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT Metadata_OverviewID, Value FROM Metadata_Overview WHERE Metadata_OverviewID IN ({string.Join(',', batch.Select(id => id.ToString(CultureInfo.InvariantCulture)))})";
            using var reader = command.ExecuteReader();
            var readID = IntegerReader(reader.GetFieldType(0));
            while (reader.Read())
                found.Add(((int)readID(reader, 0), reader.GetString(1)));
        }

        return found;
    }

    #endregion

    #region Unused Repository Hooks

    /// <inheritdoc />
    public void PopulateIndexes()
    {
    }

    /// <inheritdoc />
    public void RegenerateDb()
    {
    }

    /// <inheritdoc />
    public void PostProcess()
    {
    }

    #endregion
}
