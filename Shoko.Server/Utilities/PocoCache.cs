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
using Shoko.Commons.Extensions;

#nullable enable
namespace NutzCode.InMemoryIndex;
//NOTE PocoCache is not thread SAFE

public class PocoCache<T, S> where S : class where T : notnull
{
    private readonly Dictionary<T, S> _dict;

    private readonly Func<S, T> _func;

    private readonly List<IPocoCacheObserver<T, S>> _observers = [];

    public Dictionary<T, S>.KeyCollection Keys => _dict.Keys;

    public Dictionary<T, S>.ValueCollection Values => _dict.Values;

    public PocoIndex<T, S, U> CreateIndex<U>(Func<S, U> func1)
    {
        return new PocoIndex<T, S, U>(this, func1);
    }

    public PocoIndex<T, S, U> CreateIndex<U>(Func<S, IEnumerable<U>> func1)
    {
        return new PocoIndex<T, S, U>(this, func1);
    }

    public PocoIndex<T, S, U> CreateIndex<U>(Func<S, IReadOnlyList<U>> func1)
        => new PocoIndex<T, S, U>(this, func1);

    public PocoCache(IEnumerable<S> objectList, Func<S, T> keyFunc)
    {
        _func = keyFunc;
        _dict = objectList.ToDictionary(keyFunc, a => a);
    }

    internal void AddChain(IPocoCacheObserver<T, S> observer)
    {
        _observers.Add(observer);
    }

    public S? Get(T key)
    {
        return _dict.GetValueOrDefault(key);
    }

    public void Update(S obj)
    {
        var key = _func(obj);
        foreach (var observer in _observers)
        {
            observer.Update(key, obj);
        }

        _dict[key] = obj;
    }

    public void Remove(S obj)
    {
        var key = _func(obj);
        foreach (var observer in _observers)
        {
            observer.Remove(key);
        }

        if (_dict.ContainsKey(key))
        {
            _dict.Remove(key);
        }
    }

    public void Clear()
    {
        _dict.Clear();

        foreach (var observer in _observers)
        {
            observer.Clear();
        }
    }
}

public interface IPocoCacheObserver<in TKey, in TEntity> where TEntity : class
{
    void Update(TKey key, TEntity entity);

    void Remove(TKey key);

    void Clear();
}

public class PocoIndex<TKey, TEntity, TInverseKey> : IPocoCacheObserver<TKey, TEntity>
    where TEntity : class where TKey : notnull
{
    private static readonly List<TEntity> _emptyList = [];

    private readonly PocoCache<TKey, TEntity> _cache;

    private readonly BiDictionaryManyToMany<TKey, TInverseKey> _dict;

    private readonly Func<TEntity, IEnumerable<TInverseKey>> _func;

    internal PocoIndex(PocoCache<TKey, TEntity> cache, Func<TEntity, TInverseKey> func) : this(cache, a => [func(a)]) { }

    internal PocoIndex(PocoCache<TKey, TEntity> cache, Func<TEntity, IEnumerable<TInverseKey>> func)
    {
        _cache = cache;
        _dict = new BiDictionaryManyToMany<TKey, TInverseKey>(_cache.Keys.ToDictionary(a => a, a => func(_cache.Get(a)!).ToHashSet()));
        _func = func;
        cache.AddChain(this);
    }

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

    void IPocoCacheObserver<TKey, TEntity>.Update(TKey key, TEntity obj)
    {
        _dict[key] = _func(obj).ToHashSet();
    }

    void IPocoCacheObserver<TKey, TEntity>.Remove(TKey key)
    {
        _dict.Remove(key);
    }

    void IPocoCacheObserver<TKey, TEntity>.Clear()
    {
        _dict.Clear();
    }
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
            // Only set the hash-set if the input contained a null value. See `ContainsInverseKey` at L348 as to why.
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
            // Unset the previous value.
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
