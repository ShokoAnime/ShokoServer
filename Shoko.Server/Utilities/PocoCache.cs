/* 
 
The MIT License (MIT)

Copyright (c) 2016 Máximo Piva

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Shoko.Server.Utilities;

/// <summary>
/// Plain Old Class Object (POCO) Cache.
/// </summary>
/// <typeparam name="TKey">The primary key of the entity type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
public class PocoCache<TKey, TEntity> where TKey : notnull where TEntity : class
{
    private readonly Dictionary<TKey, TEntity> _dict;

    private readonly Func<TEntity, TKey> _keyGetterFunc;

    private readonly List<IPocoCacheObserver<TKey, TEntity>> _observers = [];

    internal ReaderWriterLockSlim SyncRoot { get; } = new(LockRecursionPolicy.NoRecursion);

    public PocoCache(IEnumerable<TEntity> objectList, Func<TEntity, TKey> keyGetterFunc)
    {
        _keyGetterFunc = keyGetterFunc;
        _dict = objectList.ToDictionary(keyGetterFunc, a => a);
    }

    /// <summary>
    /// Creates a new index from the current cache.
    /// </summary>
    /// <typeparam name="TInverseKey">The type of the inverse key.</typeparam>
    /// <param name="func1">The function to get the inverse key from each entity.</param>
    /// <returns>The new index.</returns>
    public PocoIndex<TKey, TEntity, TInverseKey> CreateIndex<TInverseKey>(Func<TEntity, TInverseKey> func1)
        => PocoIndex<TKey, TEntity, TInverseKey>.Create(this, func1);

    /// <summary>
    /// Creates a new index from the current cache.
    /// </summary>
    /// <typeparam name="TInverseKey">The type of the inverse key.</typeparam>
    /// <param name="func">The function to get the inverse keys from each entity.</param>
    /// <returns>The new index.</returns>
    public PocoIndex<TKey, TEntity, TInverseKey> CreateIndex<TInverseKey>(Func<TEntity, IEnumerable<TInverseKey>> func)
        => PocoIndex<TKey, TEntity, TInverseKey>.Create(this, func);

    /// <summary>
    /// Creates a new index from the current cache.
    /// </summary>
    /// <typeparam name="TInverseKey">The type of the inverse key.</typeparam>
    /// <param name="func">The function to get the inverse keys from each entity.</param>
    /// <returns>The new index.</returns>
    public PocoIndex<TKey, TEntity, TInverseKey> CreateIndex<TInverseKey>(Func<TEntity, IReadOnlyList<TInverseKey>> func)
        => PocoIndex<TKey, TEntity, TInverseKey>.Create(this, func);


    /// <summary>
    /// Adds a new observer to the cache.
    /// </summary>
    /// <param name="observer">The observer to add.</param>
    public void AddObserver(IPocoCacheObserver<TKey, TEntity> observer)
        => _observers.Add(observer);

    /// <summary>
    /// Gets an entity for the given <paramref name="key"/> from the cache, or <c>null</c> if not found.
    /// </summary>
    /// <param name="key">The key for the entity.</param>
    /// <returns>The entity, or <c>null</c> if not found.</returns>
    public TEntity? Get(TKey key)
    {
        SyncRoot.EnterReadLock();
        try
        {
            return _dict.GetValueOrDefault(key);
        }
        finally
        {
            SyncRoot.ExitReadLock();
        }
    }

    /// <summary>
    /// Gets an entity without acquiring the lock. Only call when the caller already holds <see cref="SyncRoot"/>.
    /// </summary>
    internal TEntity? GetUnsafe(TKey key)
        => _dict.GetValueOrDefault(key);

    /// <summary>
    /// Returns a snapshot of all cached entities.
    /// </summary>
    public IReadOnlyList<TEntity> GetAll()
    {
        SyncRoot.EnterReadLock();
        try
        {
            return [.. _dict.Values];
        }
        finally
        {
            SyncRoot.ExitReadLock();
        }
    }

    /// <summary>
    /// Returns a snapshot of all cached keys.
    /// </summary>
    public IReadOnlyList<TKey> GetAllKeys()
    {
        SyncRoot.EnterReadLock();
        try
        {
            return [.. _dict.Keys];
        }
        finally
        {
            SyncRoot.ExitReadLock();
        }
    }

    /// <summary>
    ///   Returns a snapshot of every cached key with its entity, for building an index.
    /// </summary>
    /// <returns>The pairs, in the cache's enumeration order.</returns>
    internal KeyValuePair<TKey, TEntity>[] GetAllPairs()
    {
        SyncRoot.EnterReadLock();
        try
        {
            return [.. _dict];
        }
        finally
        {
            SyncRoot.ExitReadLock();
        }
    }

    /// <summary>
    /// Updates an entity in the cache.
    /// </summary>
    /// <param name="entity">The entity to update in the cache.</param>
    public void Update(TEntity entity)
    {
        SyncRoot.EnterWriteLock();
        try
        {
            var key = _keyGetterFunc(entity);
            foreach (var observer in _observers)
                observer.Update(key, entity);
            _dict[key] = entity;
        }
        finally
        {
            SyncRoot.ExitWriteLock();
        }
    }

    /// <summary>
    /// Removes an entity from the cache.
    /// </summary>
    /// <param name="entity">The entity to remove from the cache.</param>
    public void Remove(TEntity entity)
    {
        SyncRoot.EnterWriteLock();
        try
        {
            var key = _keyGetterFunc(entity);
            foreach (var observer in _observers)
                observer.Remove(key);
            _dict.Remove(key);
        }
        finally
        {
            SyncRoot.ExitWriteLock();
        }
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Clear()
    {
        SyncRoot.EnterWriteLock();
        try
        {
            _dict.Clear();
            foreach (var observer in _observers)
                observer.Clear();
        }
        finally
        {
            SyncRoot.ExitWriteLock();
        }
    }
}

/// <summary>
/// Observer for <see cref="PocoCache{TKey, TEntity}"/>
/// </summary>
/// <typeparam name="TKey">The primary key type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IPocoCacheObserver<in TKey, in TEntity> where TKey : notnull where TEntity : class
{
    /// <summary>
    /// Dispatched when an entity is updated in the cache.
    /// </summary>
    /// <param name="key">The key for the entity.</param>
    /// <param name="entity">The entity.</param>
    void Update(TKey key, TEntity entity);

    /// <summary>
    /// Dispatched when an entity is removed from the cache.
    /// </summary>
    /// <param name="key">The key for the entity.</param>
    void Remove(TKey key);

    /// <summary>
    /// Dispatched when the cache is cleared.
    /// </summary>
    void Clear();
}

#pragma warning disable CS8714

/// <summary>
///   A secondary index over a <see cref="PocoCache{TKey, TEntity}"/>, finding entities by keys taken from each one.
/// </summary>
/// <remarks>
///   A single-valued index keeps one key per row, a multi-valued one a small array of keys. The rows under each key are
///   kept as one row, a small array or a set (<see cref="PocoRowSet{TKey}"/>). Reads and writes share the cache's lock.
/// </remarks>
/// <typeparam name="TKey">The primary key type.</typeparam>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TInverseKey">The type of the keys the index finds entities by.</typeparam>
public class PocoIndex<TKey, TEntity, TInverseKey> : IPocoCacheObserver<TKey, TEntity>
    where TEntity : class where TKey : notnull
{
    private static readonly List<TEntity> _emptyList = [];

    private readonly PocoCache<TKey, TEntity> _cache;

    private readonly ReaderWriterLockSlim _lock;

    private readonly Func<TEntity, TInverseKey>? _selector;

    private readonly Func<TEntity, IEnumerable<TInverseKey>>? _multiSelector;

    // Set for a single-valued index: the one key each row is indexed under.
    private readonly Dictionary<TKey, TInverseKey>? _keys;

    // Set for a multi-valued index: the distinct keys each row is indexed under.
    private readonly Dictionary<TKey, TInverseKey[]>? _multiKeys;

    private readonly Dictionary<TInverseKey, PocoRowSet<TKey>> _rows;

    // Dictionaries cannot hold a null key, so the rows indexed under null live here.
    private PocoRowSet<TKey> _nullRows;

    private PocoIndex(PocoCache<TKey, TEntity> cache, Func<TEntity, TInverseKey> selector)
    {
        _cache = cache;
        _lock = cache.SyncRoot;
        _selector = selector;

        var pairs = cache.GetAllPairs();
        _keys = new(pairs.Length);
        _rows = [];
        foreach (var (key, entity) in pairs)
        {
            var value = selector(entity);
            _keys.Add(key, value);
            AddRow(value, key);
        }

        _rows.TrimExcess();
        cache.AddObserver(this);
    }

    private PocoIndex(PocoCache<TKey, TEntity> cache, Func<TEntity, IEnumerable<TInverseKey>> selector)
    {
        _cache = cache;
        _lock = cache.SyncRoot;
        _multiSelector = selector;

        var pairs = cache.GetAllPairs();
        _multiKeys = new(pairs.Length);
        _rows = [];
        foreach (var (key, entity) in pairs)
        {
            var values = Distinct(selector(entity));
            _multiKeys.Add(key, values);
            foreach (var value in values)
                AddRow(value, key);
        }

        _rows.TrimExcess();
        cache.AddObserver(this);
    }

    /// <summary>
    ///   Creates an index with one key per entity.
    /// </summary>
    /// <param name="cache">The cache to index.</param>
    /// <param name="func">The function to get the key from each entity.</param>
    /// <returns>The new index.</returns>
    public static PocoIndex<TKey, TEntity, TInverseKey> Create(PocoCache<TKey, TEntity> cache, Func<TEntity, TInverseKey> func)
        => new(cache, func);

    /// <summary>
    ///   Creates an index with any number of keys per entity.
    /// </summary>
    /// <param name="cache">The cache to index.</param>
    /// <param name="func">The function to get the keys from each entity.</param>
    /// <returns>The new index.</returns>
    public static PocoIndex<TKey, TEntity, TInverseKey> Create(PocoCache<TKey, TEntity> cache, Func<TEntity, IEnumerable<TInverseKey>> func)
        => new(cache, func);

    #region Lookups

    /// <summary>
    ///   Gets the first entity indexed under <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>The entity, or <c>null</c> when none is indexed under the key.</returns>
    public TEntity? GetOne(TInverseKey key)
    {
        _lock.EnterReadLock();
        try
        {
            if (key is null)
                return _nullRows.Count > 0 ? _cache.GetUnsafe(_nullRows.First) : null;

            return _rows.TryGetValue(key, out var rows) ? _cache.GetUnsafe(rows.First) : null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    ///   Gets every entity indexed under <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The key to look up.</param>
    /// <returns>A new list of the entities, or a shared empty list when none is indexed under a non-null key.</returns>
    public List<TEntity> GetMultiple(TInverseKey key)
    {
        _lock.EnterReadLock();
        try
        {
            if (key is null)
                return Resolve(_nullRows);

            return _rows.TryGetValue(key, out var rows) ? Resolve(rows) : _emptyList;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    ///   The keys an entity is indexed under, as the cache last saw it, so a
    ///   caller can compare them with the entity before updating the cache.
    /// </summary>
    /// <param name="key">The entity's key.</param>
    /// <param name="indexed">The keys, when the entity is in the cache.</param>
    /// <returns><c>true</c> when the entity is in the cache.</returns>
    public bool TryGetIndexedKeys(TKey key, [NotNullWhen(true)] out TInverseKey[]? indexed)
    {
        _lock.EnterReadLock();
        try
        {
            if (_keys is not null)
                indexed = _keys.TryGetValue(key, out var value) ? [value] : null;
            else
                indexed = _multiKeys!.TryGetValue(key, out var values) ? [.. values] : null;
            return indexed is not null;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    #endregion

    #region IPocoCacheObserver implementation

    void IPocoCacheObserver<TKey, TEntity>.Update(TKey key, TEntity obj)
    {
        if (_keys is not null)
        {
            var value = _selector!(obj);
            ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_keys, key, out var exists);
            if (exists)
            {
                if (EqualityComparer<TInverseKey>.Default.Equals(slot, value))
                    return;

                RemoveRow(slot!, key);
            }

            slot = value;
            AddRow(value, key);
            return;
        }

        var values = Distinct(_multiSelector!(obj));
        if (_multiKeys!.TryGetValue(key, out var previousValues))
        {
            if (SetEquals(previousValues, values))
                return;

            foreach (var previousValue in previousValues)
                RemoveRow(previousValue, key);
        }

        _multiKeys[key] = values;
        foreach (var value in values)
            AddRow(value, key);
    }

    void IPocoCacheObserver<TKey, TEntity>.Remove(TKey key)
    {
        if (_keys is not null)
        {
            if (_keys.Remove(key, out var value))
                RemoveRow(value, key);
            return;
        }

        if (!_multiKeys!.Remove(key, out var values))
            return;

        foreach (var value in values)
            RemoveRow(value, key);
    }

    void IPocoCacheObserver<TKey, TEntity>.Clear()
    {
        _keys?.Clear();
        _multiKeys?.Clear();
        _rows.Clear();
        _nullRows = default;
    }

    #endregion

    #region Helpers

    private List<TEntity> Resolve(in PocoRowSet<TKey> rows)
    {
        var list = new List<TEntity>(rows.Count);
        foreach (var row in rows)
            list.Add(_cache.GetUnsafe(row)!);
        return list;
    }

    private void AddRow(TInverseKey value, TKey row)
    {
        if (value is null)
        {
            _nullRows.Add(row);
            return;
        }

        CollectionsMarshal.GetValueRefOrAddDefault(_rows, value, out _).Add(row);
    }

    private void RemoveRow(TInverseKey value, TKey row)
    {
        if (value is null)
        {
            _nullRows.Remove(row);
            return;
        }

        ref var rows = ref CollectionsMarshal.GetValueRefOrNullRef(_rows, value);
        if (Unsafe.IsNullRef(ref rows) || !rows.Remove(row) || rows.Count > 0)
            return;

        _rows.Remove(value);
    }

    /// <summary>
    ///   Copies the keys a multi-valued selector returned, dropping repeats and keeping the first of each.
    /// </summary>
    /// <param name="values">The keys.</param>
    /// <returns>A new array of the distinct keys.</returns>
    private static TInverseKey[] Distinct(IEnumerable<TInverseKey> values)
    {
        TInverseKey[] array = [.. values];
        if (array.Length < 2)
            return array;
        if (array.Length > 16)
            return [.. array.Distinct()];

        var count = 1;
        for (var index = 1; index < array.Length; index++)
        {
            if (Array.IndexOf(array, array[index], 0, count) < 0)
                array[count++] = array[index];
        }

        return count == array.Length ? array : array[..count];
    }

    private static bool SetEquals(TInverseKey[] left, TInverseKey[] right)
    {
        if (left.Length != right.Length)
            return false;
        if (left.Length > 16)
            return new HashSet<TInverseKey>(left).SetEquals(right);

        foreach (var value in right)
        {
            if (Array.IndexOf(left, value) < 0)
                return false;
        }

        return true;
    }

    #endregion
}

/// <summary>
///   The rows indexed under one key: a single row inline, a few in an array, and many in a set.
/// </summary>
/// <remarks>
///   Two rows move to an array, which grows up to <see cref="MaxArrayLength"/> before turning into a set. A set shrinks
///   back to an array at half that, and an array back to the single row at one. Arrays keep the order rows were added in.
/// </remarks>
/// <typeparam name="TKey">The primary key type.</typeparam>
internal struct PocoRowSet<TKey> where TKey : notnull
{
    /// <summary>
    ///   The most rows kept in an array before they move to a set.
    /// </summary>
    internal const int MaxArrayLength = 32;

    private TKey _one;

    // Null for zero or one row, else a TKey[] or a HashSet<TKey>.
    private object? _many;

    private int _count;

    /// <summary>
    ///   The number of rows.
    /// </summary>
    public readonly int Count => _count;

    /// <summary>
    ///   The first row, only meaningful when <see cref="Count"/> is above zero.
    /// </summary>
    public readonly TKey First
    {
        get
        {
            switch (_many)
            {
                case null:
                    return _one;
                case TKey[] array:
                    return array[0];
                default:
                    foreach (var row in (HashSet<TKey>)_many)
                        return row;
                    return default!;
            }
        }
    }

    /// <summary>
    ///   Adds a row.
    /// </summary>
    /// <param name="row">The row to add.</param>
    /// <returns><c>true</c> when the row was added, <c>false</c> when it was already there.</returns>
    public bool Add(TKey row)
    {
        switch (_many)
        {
            case null when _count is 0:
                _one = row;
                _count = 1;
                return true;

            case null:
                if (EqualityComparer<TKey>.Default.Equals(_one, row))
                    return false;

                _many = new[] { _one, row };
                _one = default!;
                _count = 2;
                return true;

            case TKey[] array:
                if (Array.IndexOf(array, row, 0, _count) >= 0)
                    return false;

                if (_count == array.Length)
                {
                    if (_count >= MaxArrayLength)
                    {
                        var set = new HashSet<TKey>(array) { row };
                        _many = set;
                        _count++;
                        return true;
                    }

                    Array.Resize(ref array, array.Length * 2);
                    _many = array;
                }

                array[_count++] = row;
                return true;

            default:
                if (!((HashSet<TKey>)_many).Add(row))
                    return false;

                _count++;
                return true;
        }
    }

    /// <summary>
    ///   Removes a row.
    /// </summary>
    /// <param name="row">The row to remove.</param>
    /// <returns><c>true</c> when the row was removed, <c>false</c> when it was not there.</returns>
    public bool Remove(TKey row)
    {
        switch (_many)
        {
            case null:
                if (_count is 0 || !EqualityComparer<TKey>.Default.Equals(_one, row))
                    return false;

                _one = default!;
                _count = 0;
                return true;

            case TKey[] array:
                var index = Array.IndexOf(array, row, 0, _count);
                if (index < 0)
                    return false;

                _count--;
                Array.Copy(array, index + 1, array, index, _count - index);
                array[_count] = default!;
                if (_count is 1)
                {
                    _one = array[0];
                    _many = null;
                }

                return true;

            default:
                var set = (HashSet<TKey>)_many;
                if (!set.Remove(row))
                    return false;

                _count--;
                if (_count <= MaxArrayLength / 2)
                {
                    var smaller = new TKey[MaxArrayLength / 2];
                    set.CopyTo(smaller);
                    _many = smaller;
                }

                return true;
        }
    }

    /// <summary>
    ///   Gets an enumerator over the rows.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public readonly Enumerator GetEnumerator()
        => new(_one, _many, _count);

    /// <summary>
    ///   Enumerates the rows of a <see cref="PocoRowSet{TKey}"/> without allocating.
    /// </summary>
    /// <param name="one">The single row, when there is one.</param>
    /// <param name="many">The array or set of rows, when there are more.</param>
    /// <param name="count">The number of rows.</param>
    public struct Enumerator(TKey one, object? many, int count)
    {
        private readonly TKey[]? _array = many as TKey[];

        private readonly bool _isSet = many is HashSet<TKey>;

        private HashSet<TKey>.Enumerator _set = many is HashSet<TKey> set ? set.GetEnumerator() : default;

        private int _index = -1;

        /// <summary>
        ///   The current row.
        /// </summary>
        public TKey Current { get; private set; } = default!;

        /// <summary>
        ///   Moves to the next row.
        /// </summary>
        /// <returns><c>true</c> when there is a next row.</returns>
        public bool MoveNext()
        {
            if (_isSet)
            {
                if (!_set.MoveNext())
                    return false;

                Current = _set.Current;
                return true;
            }

            if (++_index >= count)
                return false;

            Current = _array is null ? one : _array[_index];
            return true;
        }
    }
}
