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
using Shoko.Server.Extensions;

#nullable enable
namespace NutzCode.InMemoryIndex;
//NOTE PocoCache is not thread SAFE

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

    public Dictionary<TKey, TEntity>.KeyCollection Keys => _dict.Keys;

    public Dictionary<TKey, TEntity>.ValueCollection Values => _dict.Values;

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
    /// Gets an entity for the given <paramref name="key"/> from the cache, or <langword>null</langword> if not found.
    /// </summary>
    /// <param name="key">The key for the entity.</param>
    /// <returns>The entity, or <langword>null</langword> if not found.</returns>
    public TEntity? Get(TKey key)
        => _dict.GetValueOrDefault(key);

    /// <summary>
    /// Updates an entity in the cache.
    /// </summary>
    /// <param name="entity">The entity to update in the cache.</param>
    public void Update(TEntity entity)
    {
        var key = _keyGetterFunc(entity);
        foreach (var observer in _observers)
            observer.Update(key, entity);
        _dict[key] = entity;
    }

    /// <summary>
    /// Removes an entity from the cache.
    /// </summary>
    /// <param name="entity">The entity to remove from the cache.</param>
    public void Remove(TEntity entity)
    {
        var key = _keyGetterFunc(entity);
        foreach (var observer in _observers)
            observer.Remove(key);
        _dict.Remove(key);
    }

    /// <summary>
    /// Clears the cache.
    /// </summary>
    public void Clear()
    {
        _dict.Clear();
        foreach (var observer in _observers)
            observer.Clear();
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

public class PocoIndex<TKey, TEntity, TInverseKey> : IPocoCacheObserver<TKey, TEntity>
    where TEntity : class where TKey : notnull
{
    private static readonly List<TEntity> _emptyList = [];

    private readonly PocoCache<TKey, TEntity> _cache;

    private readonly BiDictionaryManyToMany<TKey, TInverseKey> _dict;

    private readonly Func<TEntity, IEnumerable<TInverseKey>> _func;

    private PocoIndex(PocoCache<TKey, TEntity> cache, Func<TEntity, TInverseKey> func) : this(cache, a => [func(a)]) { }

    private PocoIndex(PocoCache<TKey, TEntity> cache, Func<TEntity, IEnumerable<TInverseKey>> func)
    {
        _cache = cache;
        _dict = new BiDictionaryManyToMany<TKey, TInverseKey>(_cache.Keys.ToDictionary(a => a, a => func(_cache.Get(a)!).ToHashSet()));
        _func = func;
        cache.AddObserver(this);
    }

    public static PocoIndex<TKey, TEntity, TInverseKey> Create(PocoCache<TKey, TEntity> cache, Func<TEntity, TInverseKey> func)
        => new(cache, func);

    public static PocoIndex<TKey, TEntity, TInverseKey> Create(PocoCache<TKey, TEntity> cache, Func<TEntity, IEnumerable<TInverseKey>> func)
        => new(cache, func);

    public TEntity? GetOne(TInverseKey key)
    {
        if (_cache == null || !_dict.TryGetInverse(key, out var results))
            return null;

        return results is { Count: > 0 } ? _cache.Get(results.First()) : null;
    }

    public List<TEntity> GetMultiple(TInverseKey key)
    {
        if (_cache == null || !_dict.TryGetInverse(key, out var results))
            return _emptyList;

        return results.Select(a => _cache.Get(a)!).ToList();
    }

    #region IPocoCacheObserver implementation

    void IPocoCacheObserver<TKey, TEntity>.Update(TKey key, TEntity obj)
        => _dict[key] = _func(obj).ToHashSet();

    void IPocoCacheObserver<TKey, TEntity>.Remove(TKey key)
        => _dict.Remove(key);

    void IPocoCacheObserver<TKey, TEntity>.Clear()
        => _dict.Clear();

    #endregion
}

#pragma warning disable CS8714

public class BiDictionaryManyToMany<TKey, TInverseKey> where TKey : notnull
{
    private readonly Dictionary<TKey, HashSet<TInverseKey>> _direct = [];

    private readonly Dictionary<TInverseKey, HashSet<TKey>> _inverse = [];

    private readonly bool _valueIsNullable;

    private HashSet<TKey> _inverseNullValueSet;

    public BiDictionaryManyToMany(Dictionary<TKey, HashSet<TInverseKey>> input)
    {
        _valueIsNullable = Nullable.GetUnderlyingType(typeof(TInverseKey)) is not null || typeof(string).IsAssignableFrom(typeof(TInverseKey));
        _direct = input;
        _inverse = [];
        if (_valueIsNullable)
        {
            // Only set the hash-set if the input contained a null value. See `ContainsInverseKey` as to why.
            _inverseNullValueSet = input
                .Where(a => a.Value.Any(b => b is null))
                .Select(a => a.Key)
                .ToHashSet();
            _inverse = input
                .Where(a => !a.Value.Any(b => b is null))
                .SelectMany(a => a.Value.WhereNotNull().Select(b => (b, a.Key)))
                .GroupBy(a => a.b)
                .ToDictionary(a => a.Key, a => a.Select(b => b.Key).ToHashSet());
        }
        else
        {
            _inverseNullValueSet = [];
            _inverse = input
                .SelectMany(a => a.Value.Select(b => (b, a.Key)))
                .GroupBy(a => a.b)
                .ToDictionary(a => a.Key, a => a.Select(b => b.Key).ToHashSet());
        }
    }

    public HashSet<TInverseKey> this[TKey key]
    {
        get => _direct[key];
        set
        {
            // Unset the previous value unless it's the same as the current value.
            if (_direct.TryGetValue(key, out var oldValue))
            {
                if (oldValue.SetEquals(value))
                    return;

                foreach (var s in oldValue)
                {
                    if (_valueIsNullable && s is null)
                    {
                        _inverseNullValueSet.Remove(key);
                        continue;
                    }

                    if (!_inverse.TryGetValue(s, out var inverseValue))
                        continue;

                    inverseValue.Remove(key);
                    if (inverseValue.Count == 0)
                        _inverse.Remove(s);
                }
            }

            foreach (var s in value)
            {
                if (_valueIsNullable && s is null)
                {
                    _inverseNullValueSet.Add(key);
                    continue;
                }

                if (_inverse.TryGetValue(s, out var inverseValue))
                    inverseValue.Add(key);
                else
                    _inverse.Add(s, [key]);
            }

            _direct[key] = value is HashSet<TInverseKey> set ? set : [.. value];
        }
    }

    public bool ContainsKey(TKey key)
        => _direct.ContainsKey(key);

    public bool ContainsInverseKey(TInverseKey key)
        => _valueIsNullable && key is null
            ? _inverseNullValueSet is not null
            : _inverse.ContainsKey(key);

    public bool TryGetValue(TKey key, [NotNullWhen(true)] out HashSet<TInverseKey>? value)
        => _direct.TryGetValue(key, out value);

    public bool TryGetInverse(TInverseKey key, [NotNullWhen(true)] out HashSet<TKey>? value)
        => key is null
            ? (value = _inverseNullValueSet) is not null
            : _inverse.TryGetValue(key, out value);

    public HashSet<TKey> FindInverse(TInverseKey k)
        => k is null ? _inverseNullValueSet : _inverse.TryGetValue(k, out var value) ? value : [];

    public void Remove(TKey key)
    {
        if (!_direct.TryGetValue(key, out var oldValue))
            return;

        foreach (var s in oldValue)
        {
            if (_valueIsNullable && s is null)
            {
                _inverseNullValueSet.Remove(key);
                continue;
            }

            if (!_inverse.TryGetValue(s, out var inverseValue))
                continue;

            inverseValue.Remove(key);
            if (inverseValue.Count == 0)
                _inverse.Remove(s);
        }
        _direct.Remove(key);
    }

    public void Clear()
    {
        _direct.Clear();
        _inverse.Clear();
        _inverseNullValueSet = [];
    }
}
