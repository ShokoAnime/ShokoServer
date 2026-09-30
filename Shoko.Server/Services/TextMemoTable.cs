using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   What the text manager remembers per entry, kept until something the
///   choice was worked out from changes.
/// </summary>
/// <remarks>
///   Every entry a value reads is recorded as a dependency, and forgetting an
///   entry forgets its dependents down the chain. What a filter matches by is
///   kept apart: forgotten with what it read, but forgetting nothing else.
///   Past the capacity, the entries read longest ago go until a fifth of the
///   room is free.
/// </remarks>
/// <param name="capacity">How many entries are kept before the least read go.</param>
internal sealed class TextMemoTable(int capacity = TextMemoTable.DefaultCapacity)
{
    #region Fields

    /// <summary>
    ///   How many entries are kept by default, under a third of what a walk
    ///   through a large library works out.
    /// </summary>
    internal const int DefaultCapacity = 250_000;

    /// <summary>
    ///   How many entries are kept before the least read go.
    /// </summary>
    private readonly int _capacity = capacity > 0 ? capacity : DefaultCapacity;

    /// <summary>
    ///   How many entries <see cref="_entries"/> holds, kept with the lock
    ///   held, as counting a concurrent dictionary takes every one of its
    ///   locks.
    /// </summary>
    private int _count;

    /// <summary>
    ///   How many values are being worked out right now, on any thread. A
    ///   forget made while none is only has to drop what was kept.
    /// </summary>
    private int _computing;

    /// <summary>
    ///   Counts the values kept, standing for the time an entry was last
    ///   read or kept at.
    /// </summary>
    private int _clock;

    /// <summary>
    ///   Marks a slot nothing was worked out for yet.
    /// </summary>
    private static readonly object _missing = new();

    /// <summary>
    ///   The values worked out, per entry.
    /// </summary>
    private readonly ConcurrentDictionary<MemoKey, Entry> _entries = new();

    /// <summary>
    ///   The values worked out from an entry, per entry.
    /// </summary>
    private readonly Dictionary<MetadataGuid, Dependents> _dependents = [];

    /// <summary>
    ///   Held while dependencies are added, a value is kept, or entries are
    ///   forgotten, so a value is never kept once what it was read from has
    ///   been forgotten.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    ///   When each entry was last forgotten, by <see cref="_sequence"/>, so a
    ///   value is dropped only when something it read was forgotten while it
    ///   was worked out.
    /// </summary>
    private readonly Dictionary<MetadataGuid, long> _forgottenAt = [];

    /// <summary>
    ///   How many entries <see cref="_forgottenAt"/> holds before it is
    ///   cleared.
    /// </summary>
    private const int MaxForgottenKept = 4096;

    /// <summary>
    ///   Counts the forgets. Raised only once a forget has dropped its
    ///   values, so a value worked out after reading it saw them gone.
    /// </summary>
    private long _sequence;

    /// <summary>
    ///   The forget <see cref="_forgottenAt"/> was last cleared at: a value
    ///   started before it is not kept when anything was forgotten since.
    /// </summary>
    private long _floor;

    /// <summary>
    ///   The values this thread is working out, innermost first.
    /// </summary>
    [ThreadStatic]
    private static Frame? _frame;

    #endregion

    #region Types

    /// <summary>
    ///   Where an entry's values are kept: with its texts, or apart for the
    ///   filters.
    /// </summary>
    /// <param name="Entity">The entry.</param>
    /// <param name="ForFilters">Whether the values are what a filter matches the entry by.</param>
    private readonly record struct MemoKey(MetadataGuid Entity, bool ForFilters)
    {
        /// <summary>
        ///   Where a value is kept.
        /// </summary>
        /// <param name="entity">The entry.</param>
        /// <param name="slot">The value.</param>
        /// <returns>The key.</returns>
        internal static MemoKey Of(MetadataGuid entity, TextMemoSlot slot)
            => new(entity, slot is TextMemoSlot.FilterNames or TextMemoSlot.FilterPreferredNames);
    }

    /// <summary>
    ///   The values kept for one entry, all worked out with the same language
    ///   settings.
    /// </summary>
    /// <param name="owner">The table the values are kept in.</param>
    /// <param name="entity">The entry.</param>
    /// <param name="generation">The settings' generation.</param>
    private sealed class Entry(TextMemoTable owner, MetadataGuid entity, int generation)
    {
        internal readonly TextMemoTable Owner = owner;

        internal readonly MetadataGuid Entity = entity;

        internal readonly int Generation = generation;

        /// <summary>
        ///   Set once the values are forgotten, so a model holding on to them
        ///   reads them again.
        /// </summary>
        internal volatile bool IsForgotten;

        /// <summary>
        ///   When a value was last read or kept, by the owner's clock, so a
        ///   trim lets the entries read longest ago go first.
        /// </summary>
        internal int LastUsed = owner._clock;

        /// <summary>
        ///   Marks an inline place no slot was kept in yet.
        /// </summary>
        private const byte NoSlot = byte.MaxValue;

        /// <summary>
        ///   The slot of the first value kept, set once its value is.
        /// </summary>
        private volatile byte _firstSlot = NoSlot;

        private object? _firstValue;

        /// <summary>
        ///   The slot of the second value kept, set once its value is.
        /// </summary>
        private volatile byte _secondSlot = NoSlot;

        private object? _secondValue;

        /// <summary>
        ///   The values past the second, by slot, as far as the highest slot
        ///   kept yet, so most entries, which keep two values, hold no array.
        /// </summary>
        private volatile object?[]? _more;

        /// <summary>
        ///   A slot's value.
        /// </summary>
        /// <param name="slot">The slot.</param>
        /// <param name="value">The value, when one was kept.</param>
        /// <returns><c>true</c> when a value was kept in the slot.</returns>
        internal bool TryGet(TextMemoSlot slot, out object? value)
        {
            if (_firstSlot == (byte)slot)
                value = Volatile.Read(ref _firstValue);
            else if (_secondSlot == (byte)slot)
                value = Volatile.Read(ref _secondValue);
            else if (_more is { } more && (int)slot < more.Length)
                value = Volatile.Read(ref more[(int)slot]);
            else
                value = _missing;

            // The values are cleared once forgotten, so a value read while
            // they were is not trusted.
            if (ReferenceEquals(value, _missing) || IsForgotten)
            {
                value = null;
                return false;
            }

            var now = Volatile.Read(ref Owner._clock);
            if (LastUsed != now)
                LastUsed = now;
            return true;
        }

        /// <summary>
        ///   Forgets the values and lets them go, so a model holding on to the
        ///   entry holds nothing more. Called with the table's lock held.
        /// </summary>
        internal void Release()
        {
            IsForgotten = true;

            // A reader that finds a value cleared finds the entry forgotten.
            Interlocked.MemoryBarrier();
            _firstSlot = NoSlot;
            _secondSlot = NoSlot;
            _firstValue = null;
            _secondValue = null;
            _more = null;
        }

        /// <summary>
        ///   Keeps a slot's value. Called with the table's lock held.
        /// </summary>
        /// <param name="slot">The slot.</param>
        /// <param name="value">The value.</param>
        internal void Set(TextMemoSlot slot, object? value)
        {
            LastUsed = Owner._clock;

            // A value is written before its slot, so a reader that finds the
            // slot finds the value.
            if (_firstSlot == (byte)slot)
            {
                Volatile.Write(ref _firstValue, value);
                return;
            }

            if (_secondSlot == (byte)slot)
            {
                Volatile.Write(ref _secondValue, value);
                return;
            }

            if (_firstSlot == NoSlot)
            {
                Volatile.Write(ref _firstValue, value);
                _firstSlot = (byte)slot;
                return;
            }

            if (_secondSlot == NoSlot)
            {
                Volatile.Write(ref _secondValue, value);
                _secondSlot = (byte)slot;
                return;
            }

            var more = _more ?? [];
            if ((int)slot >= more.Length)
            {
                var grown = new object?[(int)slot + 1];
                Array.Fill(grown, _missing);
                more.CopyTo(grown, 0);
                grown[(int)slot] = value;
                _more = grown;
                return;
            }

            Volatile.Write(ref more[(int)slot], value);
        }
    }

    /// <summary>
    ///   The values worked out from an entry: most entries have one, so it is
    ///   kept without a set.
    /// </summary>
    /// <param name="first">The first value worked out from the entry.</param>
    private sealed class Dependents(MemoKey first) : IEnumerable<MemoKey>
    {
        private MemoKey _first = first;

        private HashSet<MemoKey>? _more;

        /// <summary>
        ///   Adds a value worked out from the entry.
        /// </summary>
        /// <param name="key">Where the value is kept.</param>
        internal void Add(MemoKey key)
        {
            if (!key.Equals(_first))
                (_more ??= []).Add(key);
        }

        /// <summary>
        ///   Drops the values no longer kept.
        /// </summary>
        /// <param name="isKept">Whether a value is still kept.</param>
        /// <returns><c>true</c> when any value is left.</returns>
        internal bool Prune(Func<MemoKey, bool> isKept)
        {
            _more?.RemoveWhere(key => !isKept(key));
            if (!isKept(_first))
            {
                if (_more is not { Count: > 0 })
                    return false;

                _first = _more.First();
                _more.Remove(_first);
            }

            if (_more is { Count: 0 })
                _more = null;
            return true;
        }

        public IEnumerator<MemoKey> GetEnumerator()
        {
            yield return _first;
            if (_more is not null)
                foreach (var key in _more)
                    yield return key;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    /// <summary>
    ///   A value being worked out on this thread, and what it read.
    /// </summary>
    /// <param name="parent">The value being worked out around it.</param>
    /// <param name="table">The table it is kept in.</param>
    /// <param name="entity">The entry.</param>
    /// <param name="slot">The value.</param>
    /// <param name="start">The forget it started after.</param>
    private sealed class Frame(Frame? parent, TextMemoTable table, MetadataGuid entity, TextMemoSlot slot, long start)
    {
        internal readonly Frame? Parent = parent;

        internal readonly TextMemoTable Table = table;

        internal readonly MetadataGuid Entity = entity;

        internal readonly TextMemoSlot Slot = slot;

        internal readonly long Start = start;

        internal readonly DependencySet Dependencies = new();

        /// <summary>
        ///   Set when a value worked out inside this one was not kept, as it
        ///   may have read something before it changed.
        /// </summary>
        internal bool IsTainted;
    }

    /// <summary>
    ///   The entries a value read, listed while they are few and hashed once
    ///   they are many, as most values read one to three.
    /// </summary>
    private sealed class DependencySet
    {
        /// <summary>
        ///   How many entries are listed before they are hashed.
        /// </summary>
        private const int MaxListed = 8;

        private MetadataGuid[]? _listed;

        private int _count;

        private HashSet<MetadataGuid>? _hashed;

        /// <summary>
        ///   Adds an entry read, once.
        /// </summary>
        /// <param name="dependency">The entry.</param>
        internal void Add(MetadataGuid dependency)
        {
            if (_hashed is not null)
            {
                _hashed.Add(dependency);
                return;
            }

            _listed ??= new MetadataGuid[4];
            for (var i = 0; i < _count; i++)
                if (ReferenceEquals(_listed[i], dependency) || _listed[i].Equals(dependency))
                    return;

            if (_count == _listed.Length && _count < MaxListed)
                Array.Resize(ref _listed, MaxListed);

            if (_count < _listed.Length)
            {
                _listed[_count++] = dependency;
                return;
            }

            _hashed = [.. _listed, dependency];
            _listed = null;
            _count = 0;
        }

        /// <summary>
        ///   Walks the entries read.
        /// </summary>
        /// <returns>The walker.</returns>
        public Enumerator GetEnumerator()
            => new(this);

        /// <summary>
        ///   Walks the entries read without allocating.
        /// </summary>
        /// <param name="set">The entries.</param>
        public struct Enumerator(DependencySet set)
        {
            private HashSet<MetadataGuid>.Enumerator _hashed = set._hashed?.GetEnumerator() ?? default;

            private int _index = -1;

            public MetadataGuid Current
                => set._hashed is not null ? _hashed.Current : set._listed![_index];

            public bool MoveNext()
                => set._hashed is not null ? _hashed.MoveNext() : ++_index < set._count;
        }
    }

    #endregion

    #region Events

    /// <summary>
    ///   Raised once per entry forgotten, the entry itself and each entry
    ///   depending on it.
    /// </summary>
    internal event Action<MetadataGuid>? Forgotten;

    #endregion

    #region Reading

    /// <summary>
    ///   A value worked out once per entry and language settings.
    /// </summary>
    /// <remarks>
    ///   The entry is recorded as read by any value being worked out around
    ///   this one. Asking for the same value while it is being worked out on
    ///   this thread answers with the fallback, which is not kept.
    /// </remarks>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="entity">The entry.</param>
    /// <param name="slot">Which of the entry's values.</param>
    /// <param name="generation">The language settings' generation.</param>
    /// <param name="compute">Works the value out.</param>
    /// <param name="reentrant">Works out the value to answer with while it is being worked out, or <c>null</c> for the type's default.</param>
    /// <returns>The value.</returns>
    internal T Get<T>(MetadataGuid entity, TextMemoSlot slot, int generation, Func<T> compute, Func<T>? reentrant = null)
    {
        object? handle = null;
        return Get(entity, slot, generation, compute, reentrant, ref handle);
    }

    /// <summary>
    ///   A value worked out once per entry and language settings, as
    ///   <see cref="Get{T}(MetadataGuid, TextMemoSlot, int, Func{T}, Func{T}?)"/>
    ///   works it out, handing the entry's values back to keep for
    ///   <see cref="TryGet{T}(ref object?, TextMemoSlot, int, out T)"/>.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="entity">The entry.</param>
    /// <param name="slot">Which of the entry's values.</param>
    /// <param name="generation">The language settings' generation.</param>
    /// <param name="compute">Works the value out.</param>
    /// <param name="reentrant">Works out the value to answer with while it is being worked out, or <c>null</c> for the type's default.</param>
    /// <param name="handle">Where the caller keeps the entry's values; set when the value is kept.</param>
    /// <returns>The value.</returns>
    internal T Get<T>(MetadataGuid entity, TextMemoSlot slot, int generation, Func<T> compute, Func<T>? reentrant, ref object? handle)
    {
        if (TryGet<T>(entity, slot, generation, out var remembered))
            return remembered;

        var key = MemoKey.Of(entity, slot);

        for (var frame = _frame; frame is not null; frame = frame.Parent)
            if (frame.Table == this && frame.Slot == slot && frame.Entity.Equals(entity))
                return reentrant is null ? default! : reentrant();

        // Counted before anything is read, and until the value is kept, so
        // a forget made meanwhile is noted for it.
        Interlocked.Increment(ref _computing);
        try
        {
            var current = new Frame(_frame, this, entity, slot, Interlocked.Read(ref _sequence));
            _frame = current;
            T result;
            try
            {
                result = compute();
            }
            finally
            {
                _frame = current.Parent;
            }

            lock (_lock)
            {
                Keep(current, key, generation, result, ref handle);
                if (_count > _capacity)
                    Trim();
            }

            return result;
        }
        finally
        {
            Interlocked.Decrement(ref _computing);
        }
    }

    /// <summary>
    ///   Keeps a value worked out, with what it read, unless something it
    ///   read was forgotten meanwhile. Called with the lock held.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="current">The value's frame.</param>
    /// <param name="key">Where the value is kept.</param>
    /// <param name="generation">The language settings' generation.</param>
    /// <param name="result">The value.</param>
    /// <param name="handle">Where the caller keeps the entry's values; set when the value is kept.</param>
    private void Keep<T>(Frame current, MemoKey key, int generation, T result, ref object? handle)
    {
        foreach (var dependency in current.Dependencies)
        {
            if (dependency.Equals(current.Entity) && !key.ForFilters)
                continue;

            if (_dependents.TryGetValue(dependency, out var dependents))
                dependents.Add(key);
            else
                _dependents[KeptInstanceOf(dependency)] = new(key);
        }

        // What it read may have changed while the value was worked out, so neither
        // the value nor anything worked out from it is kept.
        if (current.IsTainted || !IsUnchangedSince(current))
        {
            for (var frame = current.Parent; frame is not null; frame = frame.Parent)
            {
                if (frame.Table != this)
                    continue;

                frame.IsTainted = true;
                break;
            }

            return;
        }

        if (!_entries.TryGetValue(key, out var entry) || entry.Generation != generation)
        {
            if (entry is null)
                _count++;
            else
                entry.Release();
            _entries[key] = entry = new(this, current.Entity, generation);
        }

        Volatile.Write(ref _clock, unchecked(_clock + 1));
        entry.Set(current.Slot, result);
        if (!key.ForFilters)
            handle = entry;
    }

    /// <summary>
    ///   The instance of an entry's ID its kept values are under, if any, so
    ///   the table holds one copy of each ID.
    /// </summary>
    /// <param name="entity">The entry.</param>
    /// <returns>The kept instance, or the one given.</returns>
    private MetadataGuid KeptInstanceOf(MetadataGuid entity)
        => _entries.TryGetValue(new(entity, false), out var entry) ? entry.Entity : entity;

    /// <summary>
    ///   Whether nothing a value read, nor its own entry, was forgotten while
    ///   it was worked out. Called with the lock held.
    /// </summary>
    /// <param name="frame">The value's frame.</param>
    /// <returns><c>true</c> when the value can be kept.</returns>
    private bool IsUnchangedSince(Frame frame)
    {
        if (_sequence == frame.Start)
            return true;

        if (frame.Start < _floor)
            return false;

        if (_forgottenAt.TryGetValue(frame.Entity, out var at) && at > frame.Start)
            return false;

        foreach (var dependency in frame.Dependencies)
            if (_forgottenAt.TryGetValue(dependency, out at) && at > frame.Start)
                return false;

        return true;
    }

    /// <summary>
    ///   A value worked out before, without working it out when there is
    ///   none, so a caller can skip building what it would need to.
    /// </summary>
    /// <remarks>
    ///   The entry is recorded as read by any value being worked out around
    ///   this one, whether or not the value was found.
    /// </remarks>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="entity">The entry.</param>
    /// <param name="slot">Which of the entry's values.</param>
    /// <param name="generation">The language settings' generation.</param>
    /// <param name="value">The value, when it was found.</param>
    /// <returns><c>true</c> when the value was worked out before with these settings.</returns>
    internal bool TryGet<T>(MetadataGuid entity, TextMemoSlot slot, int generation, out T value)
    {
        Record(entity);
        if (_entries.TryGetValue(MemoKey.Of(entity, slot), out var entry) && entry.Generation == generation && entry.TryGet(slot, out var found))
        {
            value = (T)found!;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    ///   A value worked out before, read off the entry's values a model keeps,
    ///   which is quicker than looking them up.
    /// </summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="handle">Where the model keeps the entry's values, as a <c>Get</c> with a handle set it.</param>
    /// <param name="slot">Which of the entry's values.</param>
    /// <param name="generation">The language settings' generation.</param>
    /// <param name="value">The value, when it was found.</param>
    /// <returns><c>true</c> when the value was worked out before, with these settings, and not forgotten since.</returns>
    internal bool TryGet<T>(ref object? handle, TextMemoSlot slot, int generation, out T value)
    {
        if (handle is Entry { IsForgotten: false } entry && entry.Owner == this && entry.Generation == generation &&
            entry.TryGet(slot, out var found))
        {
            Record(entry.Entity);
            value = (T)found!;
            return true;
        }

        value = default!;
        return false;
    }

    /// <summary>
    ///   Records that the values being worked out on this thread read an
    ///   entry, so they are forgotten with it.
    /// </summary>
    /// <param name="dependency">The entry read.</param>
    internal void Record(MetadataGuid dependency)
    {
        for (var frame = _frame; frame is not null; frame = frame.Parent)
        {
            if (frame.Table != this)
                continue;

            frame.Dependencies.Add(dependency);
            return;
        }
    }

    /// <summary>
    ///   Whether this thread is working out a value right now.
    /// </summary>
    internal bool IsRecording
    {
        get
        {
            for (var frame = _frame; frame is not null; frame = frame.Parent)
                if (frame.Table == this)
                    return true;
            return false;
        }
    }

    #endregion

    #region Forgetting

    /// <summary>
    ///   Forgets what was worked out for an entry, and for every entry that
    ///   was worked out from it.
    /// </summary>
    /// <param name="entity">The entry that changed.</param>
    internal void Forget(MetadataGuid entity)
    {
        var forgotten = new List<MetadataGuid>();
        lock (_lock)
        {
            var sequence = _sequence + 1;
            var pending = new Stack<MetadataGuid>();
            var seen = new HashSet<MetadataGuid>();
            pending.Push(entity);
            while (pending.TryPop(out var current))
            {
                if (!seen.Add(current))
                    continue;

                forgotten.Add(current);
                Drop(new(current, false));
                Drop(new(current, true));
                if (!_dependents.Remove(current, out var dependents))
                    continue;

                // What a filter matches by is read by nothing else, so it is
                // dropped alone.
                foreach (var dependent in dependents)
                {
                    if (dependent.ForFilters)
                        Drop(dependent);
                    else
                        pending.Push(dependent.Entity);
                }
            }

            Note(forgotten, sequence);
            Advance(sequence);
        }

        foreach (var id in forgotten)
            Forgotten?.Invoke(id);
    }

    /// <summary>
    ///   Notes when entries were forgotten, for the values being worked out
    ///   right now, if any. Called with the lock held, once their values are
    ///   dropped.
    /// </summary>
    /// <remarks>
    ///   A value started after the check reads the entries after their values
    ///   were dropped, so it has nothing to note.
    /// </remarks>
    /// <param name="entries">The entries forgotten.</param>
    /// <param name="sequence">The forget's number.</param>
    private void Note(IEnumerable<MetadataGuid> entries, long sequence)
    {
        Interlocked.MemoryBarrier();
        if (Volatile.Read(ref _computing) is 0)
            return;

        foreach (var entry in entries)
            _forgottenAt[entry] = sequence;
    }

    /// <summary>
    ///   Makes a forget visible to the values started after it, once its
    ///   values are dropped. Called with the lock held.
    /// </summary>
    /// <param name="sequence">The forget's number.</param>
    private void Advance(long sequence)
    {
        if (_forgottenAt.Count > MaxForgottenKept)
        {
            _forgottenAt.Clear();
            _floor = sequence;
        }

        Interlocked.Exchange(ref _sequence, sequence);
    }

    /// <summary>
    ///   Drops an entry's values, telling a model holding on to them.
    /// </summary>
    /// <param name="key">Where the values are kept.</param>
    /// <returns><c>true</c> when values were kept there.</returns>
    private bool Drop(MemoKey key)
    {
        if (!_entries.TryRemove(key, out var entry))
            return false;

        entry.Release();
        _count--;
        return true;
    }

    /// <summary>
    ///   Lets the entries read longest ago go, until a fifth of the room is
    ///   free, and the links to what they read. Called with the lock held.
    /// </summary>
    /// <remarks>
    ///   Nothing is forgotten: a value let go is worked out again when next
    ///   asked for, and a model holding on to it reads it again.
    /// </remarks>
    private void Trim()
    {
        var excess = _count - (_capacity - (_capacity / 5));
        if (excess <= 0)
            return;

        // The clock may wrap, so entries are ordered by how long ago they
        // were read rather than by when.
        var now = _clock;
        var byAge = new List<(int Age, MemoKey Key)>(_count);
        foreach (var (key, entry) in _entries)
            byAge.Add((unchecked(now - entry.LastUsed), key));
        byAge.Sort((a, b) => b.Age.CompareTo(a.Age));
        foreach (var (_, key) in byAge)
        {
            if (excess <= 0)
                break;

            if (Drop(key))
                excess--;
        }

        // A link lives while its value or any value worked out from it does, so a forget at a chain's start
        // still reaches its end. Cuts may leave other links leading nowhere, so passes repeat until one cuts none.
        List<MetadataGuid> emptied;
        do
        {
            emptied = [];
            foreach (var (dependency, dependents) in _dependents)
                if (!dependents.Prune(IsLinkKept))
                    emptied.Add(dependency);
            foreach (var dependency in emptied)
                _dependents.Remove(dependency);
        } while (emptied.Count > 0);
    }

    /// <summary>
    ///   Whether a link to a value worked out from an entry is still needed:
    ///   the value is kept, or values worked out from it may be. Called with
    ///   the lock held.
    /// </summary>
    /// <param name="key">Where the value is kept.</param>
    /// <returns><c>true</c> when the link is kept.</returns>
    private bool IsLinkKept(MemoKey key)
        => _entries.ContainsKey(key) || (!key.ForFilters && _dependents.ContainsKey(key.Entity));

    /// <summary>
    ///   How many entries are kept.
    /// </summary>
    internal int Count
    {
        get
        {
            lock (_lock)
                return _count;
        }
    }

    /// <summary>
    ///   Forgets what a filter matches an entry by, and nothing else.
    /// </summary>
    /// <param name="entity">The entry.</param>
    internal void ForgetFilters(MetadataGuid entity)
    {
        lock (_lock)
        {
            var sequence = _sequence + 1;
            Drop(new(entity, true));
            Note([entity], sequence);
            Advance(sequence);
        }
    }

    /// <summary>
    ///   Forgets everything worked out for every entry.
    /// </summary>
    /// <returns>The entries that had something worked out.</returns>
    internal IReadOnlyList<MetadataGuid> ForgetAll()
    {
        List<MetadataGuid> forgotten;
        lock (_lock)
        {
            var sequence = _sequence + 1;
            forgotten = [.. _entries.Keys.Where(key => !key.ForFilters).Select(key => key.Entity).Distinct()];
            foreach (var entry in _entries.Values)
                entry.Release();
            _entries.Clear();
            _count = 0;
            _dependents.Clear();
            _forgottenAt.Clear();
            _floor = sequence;
            Interlocked.Exchange(ref _sequence, sequence);
        }

        foreach (var id in forgotten)
            Forgotten?.Invoke(id);
        return forgotten;
    }

    #endregion
}

/// <summary>
///   The values the text manager remembers for an entry.
/// </summary>
internal enum TextMemoSlot
{
    /// <summary>
    ///   The title chosen for an entry whose texts the store keeps.
    /// </summary>
    StoredTitle,

    /// <summary>
    ///   The overview chosen for an entry whose texts the store keeps.
    /// </summary>
    StoredOverview,

    /// <summary>
    ///   A Shoko entry's default title.
    /// </summary>
    DefaultTitle,

    /// <summary>
    ///   A Shoko entry's preferred title.
    /// </summary>
    PreferredTitle,

    /// <summary>
    ///   A Shoko series' titles.
    /// </summary>
    Titles,

    /// <summary>
    ///   A Shoko entry's preferred overview.
    /// </summary>
    PreferredOverview,

    /// <summary>
    ///   A Shoko group's name.
    /// </summary>
    Name,

    /// <summary>
    ///   A Shoko group's overview.
    /// </summary>
    Overview,

    /// <summary>
    ///   The names a filter matches a Shoko series or group by.
    /// </summary>
    FilterNames,

    /// <summary>
    ///   The names in preferred languages a filter matches a Shoko series or
    ///   group by.
    /// </summary>
    FilterPreferredNames,

    /// <summary>
    ///   The titles a Shoko series is found by in a search.
    /// </summary>
    SearchTitles,

    /// <summary>
    ///   A Shoko entry's default overview.
    /// </summary>
    DefaultOverview,
}
