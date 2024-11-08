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
using System.Linq;

namespace NutzCode.InMemoryIndex;
//NOTE PocoCache is not thread SAFE

public class PocoCache<T, S> where S : class
{
    private readonly Dictionary<T, S> _dict;
    private readonly Func<S, T> _func;
    private readonly List<IPocoCacheObserver<T, S>> _observers = new();

    public Dictionary<T, S>.KeyCollection Keys => _dict.Keys;
    public Dictionary<T, S>.ValueCollection Values => _dict.Values;

    public PocoIndex<T, S, U> CreateIndex<U>(Func<S, U> func1)
    {
        return new PocoIndex<T, S, U>(this, func1);
    }

    public PocoIndex<T, S, N, U> CreateIndex<N, U>(Func<S, N> func1, Func<S, U> func2)
    {
        return new PocoIndex<T, S, N, U>(this, func1, func2);
    }

    public PocoIndex<T, S, N, R, U> CreateIndex<N, R, U>(Func<S, N> func1, Func<S, R> func2, Func<S, U> func3)
    {
        return new PocoIndex<T, S, N, R, U>(this, func1, func2, func3);
    }

    public PocoCache(IEnumerable<S> objectList, Func<S, T> keyFunc)
    {
        _func = keyFunc;
        _dict = objectList.ToDictionary(keyFunc, a => a);
    }

    internal void AddChain(IPocoCacheObserver<T, S> observer)
    {
        _observers.Add(observer);
    }

    public S Get(T key)
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

public class PocoIndex<T, S, U> : IPocoCacheObserver<T, S>
    where S : class
{
    private static readonly List<S> EmptyList = new ();
    private readonly PocoCache<T, S> _cache;
    private readonly BiDictionaryOneToMany<T, U> _dict;
    private readonly Func<S, U> _func;

    internal PocoIndex(PocoCache<T, S> cache, Func<S, U> func1)
    {
        _cache = cache;
        _dict = new BiDictionaryOneToMany<T, U>(_cache.Keys.ToDictionary(a => a, a => func1(_cache.Get(a))));
        _func = func1;
        cache.AddChain(this);
    }

    public S GetOne(U key)
    {
        if (_cache == null || !_dict.TryGetInverse(key, out var results))
            return null;

        return results.Count == 0 ? null : _cache.Get(results.FirstOrDefault());
    }

    public List<S> GetMultiple(U key)
    {
        if (_cache == null || !_dict.TryGetInverse(key, out var results))
            return EmptyList;

        return results.Select(a => _cache.Get(a)).ToList();
    }

    void IPocoCacheObserver<T, S>.Update(T key, S obj)
    {
        _dict[key] = _func(obj);
    }

    void IPocoCacheObserver<T, S>.Remove(T key)
    {
        _dict.Remove(key);
    }

    void IPocoCacheObserver<T, S>.Clear()
    {
        _dict.Clear();
    }
}

public class PocoIndex<T, S, N, U> where S : class
{
    private readonly PocoIndex<T, S, Tuple<N, U>> _index;

    internal PocoIndex(PocoCache<T, S> cache, Func<S, N> func1, Func<S, U> func2)
    {
        _index = new PocoIndex<T, S, Tuple<N, U>>(cache, a => new Tuple<N, U>(func1(a), func2(a)));
    }

    public S GetOne(N key1, U key2)
    {
        var key = new Tuple<N, U>(key1, key2);
        return _index.GetOne(key);
    }

    public List<S> GetMultiple(N key1, U key2)
    {
        var key = new Tuple<N, U>(key1, key2);
        return _index.GetMultiple(key);
    }
}

public class PocoIndex<T, S, N, R, U> where S : class
{
    private readonly PocoIndex<T, S, Tuple<N, R, U>> _index;

    internal PocoIndex(PocoCache<T, S> cache, Func<S, N> func1, Func<S, R> func2, Func<S, U> func3)
    {
        _index = new PocoIndex<T, S, Tuple<N, R, U>>(cache,
            a => new Tuple<N, R, U>(func1(a), func2(a), func3(a)));
    }

    public S GetOne(N key1, R key2, U key3)
    {
        var key = new Tuple<N, R, U>(key1, key2, key3);
        return _index.GetOne(key);
    }

    public List<S> GetMultiple(N key1, R key2, U key3)
    {
        var key = new Tuple<N, R, U>(key1, key2, key3);
        return _index.GetMultiple(key);
    }
}

public class BiDictionaryOneToOne<T, S>
{
    private readonly Dictionary<T, S> _direct = new();
    private readonly Dictionary<S, T> _inverse = new();

    public BiDictionaryOneToOne()
    {
    }

    public BiDictionaryOneToOne(Dictionary<T, S> input)
    {
        _direct = input;
        _inverse = _direct.ToDictionary(a => a.Value, a => a.Key);
    }

    public S this[T key]
    {
        get => _direct[key];
        set
        {
            if (_direct.TryGetValue(key, out var oldValue))
            {
                if (oldValue.Equals(value)) return;
                if (_inverse.ContainsKey(oldValue)) _inverse.Remove(oldValue);
            }

            _inverse.TryAdd(value, key);
            _direct[key] = value;
        }
    }


    public T FindInverse(S k)
    {
        return _inverse[k];
    }

    public bool ContainsKey(T key)
    {
        return _direct.ContainsKey(key);
    }

    public bool ContainsInverseKey(S key)
    {
        return _inverse.ContainsKey(key);
    }

    public void Remove(T value)
    {
        if (!_direct.TryGetValue(value, out var oldValue)) return;
        _inverse.Remove(oldValue);
        _direct.Remove(value);
    }

    public void Clear()
    {
        _direct.Clear();
        _inverse.Clear();
    }
}

public class BiDictionaryOneToMany<T, S>
{
    private readonly Dictionary<T, S> _direct = new();
    private readonly Dictionary<S, HashSet<T>> _inverse = new();

    private readonly bool _valueIsNullable;

    private HashSet<T> _inverseNullValueSet;

    public BiDictionaryOneToMany()
    {
        _valueIsNullable = Nullable.GetUnderlyingType(typeof(S)) != null;
    }

    public BiDictionaryOneToMany(Dictionary<T, S> input)
    {
        _valueIsNullable = Nullable.GetUnderlyingType(typeof(S)) != null;
        _direct = input;
        if (_valueIsNullable)
        {
            var hashSet = input.Where(a => a.Value == null).Select(a => a.Key).ToHashSet();
            // Only set the hash-set if the input contained a null value. See `ContainsInverseKey` at L348 as to why.
            if (hashSet.Count > 0)
            {
                _inverseNullValueSet = hashSet;
            }

            _inverse = input.Where(a => a.Value != null).GroupBy(a => a.Value)
                .ToDictionary(a => a.Key, a => a.Select(b => b.Key).ToHashSet());
        }
        else
        {
            _inverse = input.GroupBy(a => a.Value).ToDictionary(a => a.Key, a => a.Select(b => b.Key).ToHashSet());
        }
    }

    public S this[T key]
    {
        get => _direct[key];
        set
        {
            if (_direct.TryGetValue(key, out var oldValue))
            {
                if (oldValue.Equals(value))
                {
                    return;
                }

                if (_valueIsNullable && oldValue is null)
                    _inverseNullValueSet?.Remove(key);
                else if (_inverse.TryGetValue(oldValue, out var inverseValue) && inverseValue.Contains(key))
                    inverseValue.Remove(key);
            }

            if (_valueIsNullable && value is null)
            {
                _inverseNullValueSet ??= [];
                _inverseNullValueSet.Add(key);
            }
            else
            {
                if (_inverse.TryGetValue(value, out var set))
                    set.Add(key);
                else
                    _inverse.Add(value, [key]);
            }

            _direct[key] = value;
        }
    }

    public bool ContainsKey(T key)
    {
        return _direct.ContainsKey(key);
    }

    public bool ContainsInverseKey(S key)
    {
        return _valueIsNullable && key == null ? _inverseNullValueSet != null : _inverse.ContainsKey(key);
    }

    public bool TryGetValue(T key, out S value) => _direct.TryGetValue(key, out value);
    public bool TryGetInverse(S key, out HashSet<T> value) => _inverse.TryGetValue(key, out value);

    public HashSet<T> FindInverse(S key)
    {
        if (_valueIsNullable && key == null)
        {
            return _inverseNullValueSet ?? new HashSet<T>();
        }

        return _inverse.TryGetValue(key, out var value) ? value : new HashSet<T>();
    }

    public void Remove(T key)
    {
        if (!_direct.TryGetValue(key, out var oldValue)) return;
        if (_valueIsNullable && oldValue == null)
        {
            if (_inverseNullValueSet != null && _inverseNullValueSet.Contains(key)) _inverseNullValueSet.Remove(key);
        }
        else
        {
            if (_inverse.TryGetValue(oldValue, out var inverseValue) && inverseValue.Contains(key)) inverseValue.Remove(key);
        }

        _direct.Remove(key);
    }

    public void Clear()
    {
        _direct.Clear();
        _inverse.Clear();
        _inverseNullValueSet = null;
    }
}

public class BiDictionaryManyToMany<T, S>
{
    private readonly Dictionary<T, HashSet<S>> _direct = new();
    private readonly Dictionary<S, HashSet<T>> _inverse = new();

    public BiDictionaryManyToMany()
    {
    }

    public BiDictionaryManyToMany(Dictionary<T, HashSet<S>> input)
    {
        _direct = input;
        _inverse = new Dictionary<S, HashSet<T>>();
        foreach (var t in input.Keys)
        {
            foreach (var s in input[t])
            {
                if (_inverse.TryGetValue(s, out var inverseValue))
                    inverseValue.Add(t);
                else
                    _inverse.Add(s, new HashSet<T> { t });
            }
        }
    }

    public HashSet<S> this[T key]
    {
        get => _direct[key];
        set
        {
            if (_direct.TryGetValue(key, out var oldValue))
            {
                if (oldValue.SetEquals(value)) return;

                foreach (var s in oldValue)
                {
                    if (!_inverse.TryGetValue(s, out var inverseValue)) continue;
                    if (inverseValue.Contains(key)) inverseValue.Remove(key);
                    if (inverseValue.Count == 0) _inverse.Remove(s);
                }
            }

            foreach (var s in value)
            {
                if (_inverse.TryGetValue(s, out var inverseValue))
                    inverseValue.Add(key);
                else
                    _inverse.Add(s, new HashSet<T> { key });
            }

            _direct[key] = value;
        }
    }

    public bool ContainsKey(T key)
    {
        return _direct.ContainsKey(key);
    }

    public bool ContainsInverseKey(S key)
    {
        return _inverse.ContainsKey(key);
    }

    public HashSet<T> FindInverse(S k)
    {
        return _inverse.TryGetValue(k, out var value) ? value : new HashSet<T>();
    }

    public void Remove(T key)
    {
        if (!_direct.TryGetValue(key, out var oldValue)) return;

        foreach (var s in oldValue)
        {
            if (!_inverse.TryGetValue(s, out var inverseValue)) continue;
            if (inverseValue.Contains(key)) inverseValue.Remove(key);
            if (inverseValue.Count == 0) _inverse.Remove(s);
        }

        _direct.Remove(key);
    }

    public void Clear()
    {
        _direct.Clear();
        _inverse.Clear();
    }
}

public static class Extensions
{
    public static bool HasItems<T>(this List<T> org)
    {
        return org is { Count: > 0 };
    }
}
