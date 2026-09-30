using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Repositories.Cached.Metadata.Text;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Writes the titles and overviews of every entry, whoever they are from,
///   into their two tables and the text cache. Each text belongs to one entry,
///   named by its <see cref="MetadataGuid"/>, and to the source that wrote it.
/// </summary>
/// <remarks>
///   A write keeps the rows that stay, with whatever a user set on them, and
///   takes a user's picks of a text along when the text changes or goes.
/// </remarks>
/// <param name="cache">The text cache, which the reads are served from.</param>
/// <param name="writer">Writes the changes.</param>
public class MetadataTextStore(TextCache cache, MetadataRowWriter writer)
{
    /// <summary>
    ///   The longest script code a text may have.
    /// </summary>
    internal const int MaxScriptCodeLength = 8;

    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    /// <summary>
    ///   The text cache the store writes through.
    /// </summary>
    internal TextCache Cache => cache;

    #region Reading

    /// <summary>
    ///   The titles an entry has.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The titles, by source, each source's in the order given, disabled ones included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    public IReadOnlyList<ITitle> GetTitles(MetadataGuid entry, MetadataSource? source = null)
        => cache.GetTitles(entry, source);

    /// <summary>
    ///   The overviews an entry has.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The overviews, by source, each source's in the order given, disabled ones included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    public IReadOnlyList<IText> GetOverviews(MetadataGuid entry, MetadataSource? source = null)
        => cache.GetOverviews(entry, source);

    #endregion

    #region Writing

    /// <summary>
    ///   Sets a source's titles on an entry, replacing the ones it gave
    ///   before. Its overviews are left alone.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">The source the titles are from.</param>
    /// <param name="titles">The titles, in order. Empty removes the source's titles.</param>
    /// <returns>Whether anything changed.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/>, <paramref name="source"/> or
    ///   <paramref name="titles"/> is or holds <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    public bool SetTitles(MetadataGuid entry, MetadataSource source, IEnumerable<ITitle> titles)
    {
        ArgumentNullException.ThrowIfNull(titles);
        return Set(entry, source, TextKind.Title, titles, nameof(titles));
    }

    /// <summary>
    ///   Sets a source's overviews on an entry, replacing the ones it gave
    ///   before. Its titles are left alone.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">The source the overviews are from.</param>
    /// <param name="overviews">The overviews, in order. Empty removes the source's overviews.</param>
    /// <returns>Whether anything changed.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/>, <paramref name="source"/> or
    ///   <paramref name="overviews"/> is or holds <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    public bool SetOverviews(MetadataGuid entry, MetadataSource source, IEnumerable<IText> overviews)
    {
        ArgumentNullException.ThrowIfNull(overviews);
        return Set(entry, source, TextKind.Overview, overviews, nameof(overviews));
    }

    /// <summary>
    ///   Sets the one title a source gives an entry as its overall preferred
    ///   title, such as the name the core gives an AniDB tag, replacing the
    ///   titles the source gave it before.
    /// </summary>
    /// <remarks>
    ///   A title that is still given keeps its row and whatever a user set
    ///   on it; a new one is preferred over every other title of the entry.
    /// </remarks>
    /// <param name="entry">The entry.</param>
    /// <param name="source">The source the title is from.</param>
    /// <param name="title">The title, or <c>null</c> to remove the source's titles.</param>
    /// <returns>Whether anything changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> or <paramref name="source"/> is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    internal bool SetOverallTitle(MetadataGuid entry, MetadataSource source, ITitle? title)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(source);

        IReadOnlyList<CheckedText> items = title is null ? [] : [Check(title, nameof(title))];
        IReadOnlyList<TextEntityChange> changes;
        lock (_writeLock)
        {
            var saving = new List<MetadataTextRow>();
            var deleting = new List<MetadataTextRow>();
            Plan(entry, source, TextKind.Title, items, saving, deleting);
            foreach (var row in saving)
                if (row.RowID is 0)
                    row.Preference = TextPreference.Overall;

            changes = Write([], saving, WithPicks(deleting));
        }

        cache.Notify(changes);
        return changes.Count is not 0;
    }

    /// <summary>
    ///   Removes the texts an entry has, and every pick of them.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">Optional. Only this source's texts, or every source's when left out.</param>
    /// <returns>How many texts of the entry were removed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is <c>null</c>.</exception>
    public int RemoveEntry(MetadataGuid entry, MetadataSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        IReadOnlyList<TextEntityChange> changes;
        int count;
        lock (_writeLock)
        {
            var deleting = RowsOf(entry, source is null ? null : row => cache.SourceOf(row) == source, withValues: false);
            count = deleting.Count;
            changes = Write([], [], WithPicks(deleting));
        }

        cache.Notify(changes);
        return count;
    }

    /// <summary>
    ///   Removes every text a source gave, on the entries a filter picks, and
    ///   every pick of them.
    /// </summary>
    /// <param name="source">The source whose text goes.</param>
    /// <param name="entries">Which entries to clear.</param>
    /// <returns>The entries that lost text.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="source"/> or <paramref name="entries"/> is <c>null</c>.
    /// </exception>
    public IReadOnlyList<MetadataGuid> RemoveSource(MetadataSource source, Func<MetadataGuid, bool> entries)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entries);

        return RemoveWhere(entries, row => cache.SourceOf(row) == source);
    }

    /// <summary>
    ///   Removes every text of the entries a filter picks, whoever gave it,
    ///   and every pick of them, as when the entries themselves are gone.
    /// </summary>
    /// <param name="entries">Which entries to clear.</param>
    /// <returns>The entries that lost text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <c>null</c>.</exception>
    public IReadOnlyList<MetadataGuid> RemoveEntries(Func<MetadataGuid, bool> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return RemoveWhere(entries, _ => true);
    }

    /// <summary>
    ///   Removes the texts a filter picks on the entries another picks, and
    ///   every pick of them, in one write.
    /// </summary>
    /// <param name="entries">Which entries to clear.</param>
    /// <param name="rows">Which of their texts to remove.</param>
    /// <returns>The entries that lost text.</returns>
    private List<MetadataGuid> RemoveWhere(Func<MetadataGuid, bool> entries, Func<TextRow, bool> rows)
    {
        IReadOnlyList<TextEntityChange> changes;
        List<MetadataGuid> cleared;
        lock (_writeLock)
        {
            var deleting = new List<MetadataTextRow>();
            cleared = [];
            foreach (var (entity, entityRows) in cache.Enumerate())
            {
                if (!entries(entity))
                    continue;

                var before = deleting.Count;
                // A removed row needs no value, so none is read.
                foreach (var row in entityRows)
                    if (rows(row))
                        deleting.Add(cache.ToRow(entity, row, string.Empty));
                if (deleting.Count > before)
                    cleared.Add(entity);
            }

            if (deleting.Count is 0)
                return cleared;

            changes = Write([], [], WithPicks(deleting));
        }

        cache.Notify(changes);
        return cleared;
    }

    /// <summary>
    ///   Writes rows a caller worked out itself, such as a user's changes,
    ///   taking along the picks of every removed text.
    /// </summary>
    /// <param name="saving">The rows to insert or update.</param>
    /// <param name="deleting">The rows to remove.</param>
    internal void WriteRows(IReadOnlyCollection<MetadataTextRow> saving, IReadOnlyCollection<MetadataTextRow> deleting)
    {
        IReadOnlyList<TextEntityChange> changes;
        lock (_writeLock)
            changes = Write([], [.. saving, .. ValueFollowers(saving)], WithPicks(deleting));

        cache.Notify(changes);
    }

    /// <summary>
    ///   Runs a change worked out from the current texts under the write lock,
    ///   so nothing is written between reading and writing.
    /// </summary>
    /// <typeparam name="T">What the change returns.</typeparam>
    /// <param name="plan">Works out the rows to save and remove, and what to return.</param>
    /// <returns>What the plan returned.</returns>
    internal T Change<T>(Func<(IReadOnlyCollection<MetadataTextRow> Saving, IReadOnlyCollection<MetadataTextRow> Deleting, T Result)> plan)
    {
        IReadOnlyList<TextEntityChange> changes;
        T result;
        lock (_writeLock)
        {
            var (saving, deleting, planned) = plan();
            result = planned;
            changes = Write([], [.. saving, .. ValueFollowers(saving)], WithPicks(deleting));
        }

        cache.Notify(changes);
        return result;
    }

    /// <summary>
    ///   Writes other rows together with a source's titles on several of its
    ///   own entries, in one transaction, so a person and its alternative
    ///   names are saved as one. Each entry's titles from the source are
    ///   replaced by the ones given.
    /// </summary>
    /// <param name="titles">The entries and their titles, in order. Every entry is on the titles' source.</param>
    /// <param name="changes">The other rows to write.</param>
    /// <exception cref="ArgumentNullException">A title, or its value, is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    internal void WriteWithTitles(IReadOnlyList<(MetadataGuid Entry, IReadOnlyList<ITitle> Titles)> titles, params IReadOnlyList<MetadataRowChanges> changes)
    {
        // Every text is checked before anything is written.
        var items = titles.Select(entry => (entry.Entry, Items: entry.Titles.Select(text => Check(text, nameof(titles))).ToList())).ToList();
        IReadOnlyList<TextEntityChange> textChanges;
        lock (_writeLock)
        {
            var saving = new List<MetadataTextRow>();
            var deleting = new List<MetadataTextRow>();
            foreach (var (entry, texts) in items)
                Plan(entry, entry.Source, TextKind.Title, texts, saving, deleting);

            textChanges = Write(changes, saving, WithPicks(deleting));
        }

        cache.Notify(textChanges);
    }

    /// <summary>
    ///   Writes other rows together with the titles and overviews of several
    ///   entries, each under its own source, and removes every text of the
    ///   entries that go, in one transaction. A stored series, its seasons
    ///   and its episodes are saved this way as one.
    /// </summary>
    /// <param name="texts">The entries kept and their texts, in order.</param>
    /// <param name="removing">The entries whose texts all go.</param>
    /// <param name="changesFor">
    ///   Works out the other rows to write, given the kept entries whose
    ///   texts changed. Called with the write lock held.
    /// </param>
    /// <returns>The kept entries whose texts changed.</returns>
    /// <exception cref="ArgumentNullException">A text, or its value, is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    internal IReadOnlySet<MetadataGuid> WriteWithTexts(
        IReadOnlyList<(MetadataGuid Entry, IReadOnlyList<ITitle> Titles, IReadOnlyList<IText> Overviews)> texts,
        IReadOnlyCollection<MetadataGuid> removing,
        Func<IReadOnlySet<MetadataGuid>, IReadOnlyList<MetadataRowChanges>> changesFor
    )
    {
        // Every text is checked before anything is written.
        var items = texts
            .Select(entry => (
                entry.Entry,
                Titles: entry.Titles.Select(text => Check(text, nameof(texts))).ToList(),
                Overviews: entry.Overviews.Select(text => Check(text, nameof(texts))).ToList()
            ))
            .ToList();
        IReadOnlyList<TextEntityChange> textChanges;
        var changed = new HashSet<MetadataGuid>();
        lock (_writeLock)
        {
            var saving = new List<MetadataTextRow>();
            var deleting = new List<MetadataTextRow>();
            foreach (var (entry, titles, overviews) in items)
            {
                var before = saving.Count + deleting.Count;
                Plan(entry, entry.Source, TextKind.Title, titles, saving, deleting);
                Plan(entry, entry.Source, TextKind.Overview, overviews, saving, deleting);
                if (saving.Count + deleting.Count > before)
                    changed.Add(entry);
            }

            foreach (var entry in removing)
                deleting.AddRange(RowsOf(entry, null, withValues: false));

            textChanges = Write(changesFor(changed), saving, WithPicks(deleting));
        }

        cache.Notify(textChanges);
        return changed;
    }

    /// <summary>
    ///   Writes the titles and overviews of several entries, each under its
    ///   own source, in one transaction, leaving a kind alone when it is not
    ///   given and leaving room where an entry lists the default on its row.
    /// </summary>
    /// <remarks>
    ///   The positions after a gap move up by one, so the default is put back
    ///   where the source listed it when the texts are read.
    /// </remarks>
    /// <param name="texts">The entries and their texts, in order.</param>
    /// <returns>The entries and kinds of text that changed.</returns>
    /// <exception cref="ArgumentNullException">A text, or its value, is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    internal IReadOnlySet<(MetadataGuid Entry, TextKind Kind)> WriteListedTexts(IReadOnlyList<ListedTexts> texts)
    {
        // Every text is checked before anything is written.
        var items = texts
            .Select(entry => (
                entry,
                Titles: entry.Titles?.Select(text => Check(text, nameof(texts))).ToList(),
                Overviews: entry.Overviews?.Select(text => Check(text, nameof(texts))).ToList()
            ))
            .ToList();
        IReadOnlyList<TextEntityChange> textChanges;
        var changed = new HashSet<(MetadataGuid, TextKind)>();
        lock (_writeLock)
        {
            var saving = new List<MetadataTextRow>();
            var deleting = new List<MetadataTextRow>();
            foreach (var (entry, titles, overviews) in items)
            {
                var before = saving.Count + deleting.Count;
                if (titles is not null)
                    Plan(entry.Entry, entry.Entry.Source, TextKind.Title, titles, saving, deleting, entry.TitleGap);
                if (saving.Count + deleting.Count > before)
                    changed.Add((entry.Entry, TextKind.Title));

                before = saving.Count + deleting.Count;
                if (overviews is not null)
                    Plan(entry.Entry, entry.Entry.Source, TextKind.Overview, overviews, saving, deleting, entry.OverviewGap);
                if (saving.Count + deleting.Count > before)
                    changed.Add((entry.Entry, TextKind.Overview));
            }

            textChanges = Write([], saving, WithPicks(deleting));
        }

        cache.Notify(textChanges);
        return changed;
    }

    /// <summary>
    ///   Writes other rows together with removing every text of some entries,
    ///   in one transaction, so a removed person takes its names along.
    /// </summary>
    /// <remarks>
    ///   The rows of entries that keep their default name and overview on
    ///   themselves, such as tags, studios and orderings, are written here
    ///   too, and their repositories tell the text manager of the change.
    /// </remarks>
    /// <param name="entries">The entries whose texts go.</param>
    /// <param name="changes">The other rows to write.</param>
    internal void WriteWithoutEntries(IEnumerable<MetadataGuid> entries, params IReadOnlyList<MetadataRowChanges> changes)
    {
        IReadOnlyList<TextEntityChange> textChanges;
        lock (_writeLock)
        {
            var deleting = entries.SelectMany(entry => RowsOf(entry, null, withValues: false)).ToList();
            textChanges = Write(changes, [], WithPicks(deleting));
        }

        cache.Notify(textChanges);
    }

    #endregion

    #region Planning

    /// <summary>
    ///   Replaces one source's texts of one kind on an entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">The source the texts are from.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="texts">The texts, in order.</param>
    /// <param name="paramName">The argument the texts came in through.</param>
    /// <returns>Whether anything changed.</returns>
    /// <exception cref="ArgumentNullException">
    ///   <paramref name="entry"/> or <paramref name="source"/> is, or
    ///   <paramref name="texts"/> holds, <c>null</c>.
    /// </exception>
    /// <exception cref="ArgumentException">A language, country or script code is too long.</exception>
    private bool Set(MetadataGuid entry, MetadataSource source, TextKind kind, IEnumerable<IText> texts, string paramName)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(source);

        // Every text is checked before anything is written, so a bad one
        // leaves the entry as it was.
        var items = texts.Select(text => Check(text, paramName)).ToList();
        IReadOnlyList<TextEntityChange> changes;
        lock (_writeLock)
        {
            var saving = new List<MetadataTextRow>();
            var deleting = new List<MetadataTextRow>();
            Plan(entry, source, kind, items, saving, deleting);
            changes = Write([], saving, WithPicks(deleting));
        }

        cache.Notify(changes);
        return changes.Count is not 0;
    }

    /// <summary>
    ///   Works out the rows that replace one source's texts of one kind on an
    ///   entry. Called with the write lock held.
    /// </summary>
    /// <remarks>
    ///   A text that is still given keeps its row. A text whose value changed
    ///   takes over the row of a vanished text with the same codes and type, so
    ///   what a user set on it stays, and the picks of it follow the new value.
    /// </remarks>
    /// <param name="entry">The entry.</param>
    /// <param name="source">The source the texts are from.</param>
    /// <param name="kind">Titles or overviews.</param>
    /// <param name="items">The checked texts, in order.</param>
    /// <param name="saving">Collects the rows to save.</param>
    /// <param name="deleting">Collects the rows to remove.</param>
    /// <param name="gap">Optional. The position left free for the default the entry keeps on its row.</param>
    private void Plan(MetadataGuid entry, MetadataSource source, TextKind kind, IReadOnlyList<CheckedText> items, List<MetadataTextRow> saving, List<MetadataTextRow> deleting, int? gap = null)
    {
        // A user's picks of other texts stay as they are when the user
        // source's own texts are replaced, since they follow what they pick.
        var stored = RowsOf(entry, row => row.Kind == kind && cache.SourceOf(row) == source && row.ReferenceID is 0);
        var unmatched = new List<MetadataTextRow>(stored);
        var matches = new MetadataTextRow?[items.Count];

        // First the texts that are still given as they were.
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var match = unmatched.FindIndex(row => item.SameText(row));
            if (match < 0)
                continue;

            matches[index] = unmatched[match];
            unmatched.RemoveAt(match);
        }

        // Then the ones whose value changed, in the order given.
        for (var index = 0; index < items.Count; index++)
        {
            if (matches[index] is not null)
                continue;

            var item = items[index];
            var match = unmatched.FindIndex(row => item.SameSlot(row));
            if (match < 0)
                continue;

            matches[index] = unmatched[match];
            unmatched.RemoveAt(match);
        }

        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var row = matches[index];
            var ordering = gap is { } free && index >= free ? index + 1 : index;
            if (row is not null && item.SameText(row) && row.Ordering == ordering && row.Language == item.Language)
                continue;

            var changedValue = row is not null && !string.Equals(row.Value, item.Value, StringComparison.Ordinal);
            row ??= MetadataTextRow.Create(kind);
            row.EntryID = entry;
            row.Source = source;
            item.CopyTo(row);
            row.Ordering = ordering;
            saving.Add(row);
            if (changedValue)
                saving.AddRange(Followers(row));
        }

        deleting.AddRange(unmatched);
    }

    /// <summary>
    ///   Detached copies of an entry's rows.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="filter">Which rows, or <c>null</c> for all of them.</param>
    /// <param name="withValues">Whether to read the overviews' values, which a row being removed does not need.</param>
    /// <returns>The rows, with their values, or with empty overview values when they were not read.</returns>
    private List<MetadataTextRow> RowsOf(MetadataGuid entry, Func<TextRow, bool>? filter, bool withValues = true)
    {
        var rows = cache.GetRows(entry).Where(row => filter?.Invoke(row) ?? true).ToList();
        var values = withValues
            ? cache.OverviewValues(rows.Where(row => row.Kind is TextKind.Overview).Select(row => row.ID))
            : new Dictionary<int, string>();
        return
        [
            .. rows.Select(row => cache.ToRow(entry, row, row.Kind is TextKind.Overview ? values.GetValueOrDefault(row.ID, string.Empty) : row.Value)),
        ];
    }

    /// <summary>
    ///   The rows to remove, with every pick of them, and every pick of those.
    /// </summary>
    /// <param name="deleting">The rows being removed.</param>
    /// <returns>The rows and their picks, each once.</returns>
    private List<MetadataTextRow> WithPicks(IReadOnlyCollection<MetadataTextRow> deleting)
    {
        var all = new List<MetadataTextRow>(deleting);
        var seen = deleting.Select(row => (row.Kind, row.RowID)).ToHashSet();
        for (var index = 0; index < all.Count; index++)
        {
            var row = all[index];
            if (row.RowID is 0)
                continue;

            foreach (var (owner, id) in cache.PicksOf(row.Kind, row.RowID))
            {
                if (!seen.Add((row.Kind, id)))
                    continue;

                if (cache.GetRows(owner).FirstOrDefault(pick => pick.ID == id && pick.Kind == row.Kind) is { ID: not 0 } pick)
                    all.Add(cache.ToRow(owner, pick, string.Empty));
            }
        }

        return all;
    }

    /// <summary>
    ///   The picks of saved rows whose value changed, given the new value.
    /// </summary>
    /// <param name="saving">The rows being saved.</param>
    /// <returns>The picks to save.</returns>
    private IEnumerable<MetadataTextRow> ValueFollowers(IReadOnlyCollection<MetadataTextRow> saving)
        => saving.Where(row => row.RowID is not 0).SelectMany(Followers).ToList();

    /// <summary>
    ///   The picks of a row, each given the row's value.
    /// </summary>
    /// <param name="row">The picked row, with its new value.</param>
    /// <returns>The picks whose value differs.</returns>
    private IEnumerable<MetadataTextRow> Followers(MetadataTextRow row)
    {
        if (row.RowID is 0)
            yield break;

        foreach (var (owner, id) in cache.PicksOf(row.Kind, row.RowID))
        {
            if (cache.GetRows(owner).FirstOrDefault(pick => pick.ID == id && pick.Kind == row.Kind) is not { ID: not 0 } pick)
                continue;

            var copy = cache.ToRow(owner, pick);
            if (string.Equals(copy.Value, row.Value, StringComparison.Ordinal))
                continue;

            copy.Value = row.Value;
            yield return copy;
        }
    }

    /// <summary>
    ///   Writes the rows, with any other stores' rows, in one transaction.
    /// </summary>
    /// <param name="others">The other rows.</param>
    /// <param name="saving">The texts to save.</param>
    /// <param name="deleting">The texts to remove.</param>
    /// <returns>What changed, per entry, to tell listeners once the lock is let go.</returns>
    private IReadOnlyList<TextEntityChange> Write(IReadOnlyList<MetadataRowChanges> others, IReadOnlyCollection<MetadataTextRow> saving, IReadOnlyCollection<MetadataTextRow> deleting)
    {
        var texts = new TextRowChanges(cache, saving, deleting);
        writer.Write([.. others, texts]);
        return texts.Changes;
    }

    #endregion

    #region Checking

    /// <summary>
    ///   Checks a text before it is written, and reads it into the shape it
    ///   is stored in.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <returns>The text as it will be stored.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or its value is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">Its language, country or script code is too long.</exception>
    internal static CheckedText Check(IText text, string paramName)
    {
        ArgumentNullException.ThrowIfNull(text, paramName);
        ArgumentNullException.ThrowIfNull(text.Value, paramName);
        return new(
            text.Language,
            MetadataEntries.CheckLanguageCode(text.LanguageCode, paramName) ?? "unk",
            CheckCode(text.CountryCode, MetadataEntries.MaxLanguageCodeLength, "country", paramName),
            CheckCode(text.ScriptCode, MaxScriptCodeLength, "script", paramName),
            text is ITitle title ? title.Type : TitleType.None,
            text.Value
        );
    }

    /// <summary>
    ///   Checks a country or script code before it is written.
    /// </summary>
    /// <param name="code">The code, which may be left out.</param>
    /// <param name="maxLength">The longest the code may be.</param>
    /// <param name="what">What the code names, for the message.</param>
    /// <param name="paramName">The argument it came in through.</param>
    /// <returns>The code, or <c>null</c> when it was left out or blank.</returns>
    /// <exception cref="ArgumentException">The code is too long.</exception>
    private static string? CheckCode(string? code, int maxLength, string what, string paramName)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        if (code.Length > maxLength)
            throw new ArgumentException($"A {what} code must be at most {maxLength} characters, but got '{code}'.", paramName);
        return code;
    }

    /// <summary>
    ///   A text as it is stored.
    /// </summary>
    /// <param name="Language">The language.</param>
    /// <param name="LanguageCode">The language code, <c>unk</c> when not known.</param>
    /// <param name="CountryCode">The country code, when there is one.</param>
    /// <param name="ScriptCode">The script code, when there is one.</param>
    /// <param name="TitleType">The kind of title, or none for an overview.</param>
    /// <param name="Value">The text itself.</param>
    internal sealed record CheckedText(TitleLanguage Language, string LanguageCode, string? CountryCode, string? ScriptCode, TitleType TitleType, string Value)
    {
        /// <summary>
        ///   Whether a stored row holds this very text.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns><c>true</c> when the codes, type and value all match.</returns>
        internal bool SameText(MetadataTextRow row)
            => SameSlot(row) && string.Equals(row.Value, Value, StringComparison.Ordinal) && string.Equals(row.ScriptCode, ScriptCode, StringComparison.Ordinal);

        /// <summary>
        ///   Whether a stored row could be this text with another value.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns><c>true</c> when the language and country codes and the type match.</returns>
        internal bool SameSlot(MetadataTextRow row)
            => row.TitleType == TitleType &&
               string.Equals(row.LanguageCode, LanguageCode, StringComparison.Ordinal) &&
               string.Equals(row.CountryCode, CountryCode, StringComparison.Ordinal);

        /// <summary>
        ///   Copies the text onto a row, leaving what a user set on it.
        /// </summary>
        /// <param name="row">The row.</param>
        internal void CopyTo(MetadataTextRow row)
        {
            row.Language = Language;
            row.LanguageCode = LanguageCode;
            row.CountryCode = CountryCode;
            row.ScriptCode = ScriptCode;
            row.TitleType = TitleType;
            row.Value = Value;
        }
    }

    #endregion

    #region Listed Texts

    /// <summary>
    ///   The texts a provider lists for one of its own entries.
    /// </summary>
    /// <param name="Entry">The entry, whose source the texts are from.</param>
    /// <param name="Titles">The titles, in order, or <c>null</c> to leave them alone.</param>
    /// <param name="TitleGap">The position of the default title kept on the entry's row, or <c>null</c> when it is not listed.</param>
    /// <param name="Overviews">The overviews, in order, or <c>null</c> to leave them alone.</param>
    /// <param name="OverviewGap">The position of the default overview kept on the entry's row, or <c>null</c> when it is not listed.</param>
    internal sealed record ListedTexts(MetadataGuid Entry, IReadOnlyList<ITitle>? Titles, int? TitleGap, IReadOnlyList<IText>? Overviews, int? OverviewGap);

    #endregion
}
