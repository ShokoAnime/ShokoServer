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
    private Dictionary<T, S> _dict;
    private Func<S, T> _func;
    private List<IPocoCacheObserver<T, S>> _observers = new();

    public Dictionary<T, S>.KeyCollection Keys => _dict.Keys;
    public Dictionary<T, S>.ValueCollection Values => _dict.Values;

    public PocoIndex<T, S, U> CreateIndex<U>(Func<S, U> paramfunc1)
    {
        return new PocoIndex<T, S, U>(this, paramfunc1);
    }

    public PocoIndex<T, S, N, U> CreateIndex<N, U>(Func<S, N> paramfunc1, Func<S, U> paramfunc2)
    {
        return new PocoIndex<T, S, N, U>(this, paramfunc1, paramfunc2);
    }

    public PocoIndex<T, S, N, R, U> CreateIndex<N, R, U>(Func<S, N> paramfunc1, Func<S, R> paramfunc2,
        Func<S, U> paramfunc3)
    {
        return new PocoIndex<T, S, N, R, U>(this, paramfunc1, paramfunc2, paramfunc3);
    }

    public PocoCache(IEnumerable<S> objectlist, Func<S, T> keyfunc)
    {
        _func = keyfunc;
        _dict = objectlist.ToDictionary(keyfunc, a => a);
    }

    internal void AddChain(IPocoCacheObserver<T, S> observer)
    {
        _observers.Add(observer);
    }

    public S Get(T key)
    {
        return _dict.ContainsKey(key)
            ? _dict[key]
            : null;
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

public interface IPocoCacheObserver<TKey, TEntity> where TEntity : class
{
    void Update(TKey key, TEntity entity);

    void Remove(TKey key);

    void Clear();
}

public class PocoIndex<T, S, U> : IPocoCacheObserver<T, S>
    where S : class
{
    internal PocoCache<T, S> _cache;
    internal BiDictionaryOneToMany<T, U> _dict;
    internal Func<S, U> _func;

    internal PocoIndex(PocoCache<T, S> cache, Func<S, U> paramfunc1)
    {
        _cache = cache;
        _dict = new BiDictionaryOneToMany<T, U>(_cache.Keys.ToDictionary(a => a, a => paramfunc1(_cache.Get(a))));
        _func = paramfunc1;
        cache.AddChain(this);
    }

    public S GetOne(U key)
    {
        if (_cache == null || !_dict.ContainsInverseKey(key))
        {
            return null;
        }

        var hashes = _dict.FindInverse(key);
        return hashes.Count == 0
            ? null
            : _cache.Get(hashes.First());
    }

    public List<S> GetMultiple(U key)
    {
        if (_cache == null || !_dict.ContainsInverseKey(key))
        {
            return new List<S>();
        }

        return _dict.FindInverse(key).Select(a => _cache.Get(a)).ToList();
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
    public PocoIndex<T, S, Tuple<N, U>> _index;

    internal PocoIndex(PocoCache<T, S> cache, Func<S, N> paramfunc1, Func<S, U> paramfunc2)
    {
        _index = new PocoIndex<T, S, Tuple<N, U>>(cache, a => new Tuple<N, U>(paramfunc1(a), paramfunc2(a)));
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
    public PocoIndex<T, S, Tuple<N, R, U>> _index;

    internal PocoIndex(PocoCache<T, S> cache, Func<S, N> paramfunc1, Func<S, R> paramfunc2, Func<S, U> paramfunc3)
    {
        _index = new PocoIndex<T, S, Tuple<N, R, U>>(cache,
            a => new Tuple<N, R, U>(paramfunc1(a), paramfunc2(a), paramfunc3(a)));
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
    private Dictionary<T, S> direct = new();
    private Dictionary<S, T> inverse = new();

    public BiDictionaryOneToOne()
    {
    }

    public BiDictionaryOneToOne(Dictionary<T, S> input)
    {
        direct = input;
        inverse = direct.ToDictionary(a => a.Value, a => a.Key);
    }

    public S this[T key]
    {
        get => direct[key];
        set
        {
            if (direct.ContainsKey(key))
            {
                var oldvalue = direct[key];
                if (oldvalue.Equals(value))
                {
                    return;
                }

                if (inverse.ContainsKey(oldvalue))
                {
                    inverse.Remove(oldvalue);
                }
            }

            if (!inverse.ContainsKey(value))
            {
                inverse.Add(value, key);
            }

            direct[key] = value;
        }
    }


    public T FindInverse(S k)
    {
        return inverse[k];
    }

    public bool ContainsKey(T key)
    {
        return direct.ContainsKey(key);
    }

    public bool ContainsInverseKey(S key)
    {
        return inverse.ContainsKey(key);
    }

    public void Remove(T value)
    {
        if (direct.ContainsKey(value))
        {
            var n = direct[value];
            inverse.Remove(n);
            direct.Remove(value);
        }
    }

    public void Clear()
    {
        direct.Clear();
        inverse.Clear();
    }
}

public class BiDictionaryOneToMany<T, S>
{
    private Dictionary<T, S> direct = new();
    private Dictionary<S, HashSet<T>> inverse = new();

    private readonly bool valueIsNullable;

    private HashSet<T> inverseNullValueSet;

    public BiDictionaryOneToMany()
    {
        valueIsNullable = Nullable.GetUnderlyingType(typeof(S)) != null;
    }

    public BiDictionaryOneToMany(Dictionary<T, S> input)
    {
        valueIsNullable = Nullable.GetUnderlyingType(typeof(S)) != null;
        direct = input;
        if (valueIsNullable)
        {
            var hashSet = input.Where(a => a.Value == null).Select(a => a.Key).ToHashSet();
            // Only set the hash-set if the input contained a null value. See `ContainsInverseKey` at L348 as to why.
            if (hashSet.Count > 0)
            {
                inverseNullValueSet = hashSet;
            }

            inverse = input.Where(a => a.Value != null).GroupBy(a => a.Value)
                .ToDictionary(a => a.Key, a => a.Select(b => b.Key).ToHashSet());
        }
        else
        {
            inverse = input.GroupBy(a => a.Value).ToDictionary(a => a.Key, a => a.Select(b => b.Key).ToHashSet());
        }
    }

    public S this[T key]
    {
        get => direct[key];
        set
        {
            if (direct.ContainsKey(key))
            {
                var oldvalue = direct[key];
                if (oldvalue.Equals(value))
                {
                    return;
                }

                if (valueIsNullable && oldvalue == null)
                {
                    if (inverseNullValueSet != null && inverseNullValueSet.Contains(key))
                    {
                        inverseNullValueSet.Remove(key);
                    }
                }
                else
                {
                    if (inverse.ContainsKey(oldvalue) && inverse[oldvalue].Contains(key))
                    {
                        inverse[oldvalue].Remove(key);
                    }
                }
            }

            if (valueIsNullable && value == null)
            {
                if (inverseNullValueSet == null)
                {
                    inverseNullValueSet = new HashSet<T>();
                }

                if (!inverseNullValueSet.Contains(key))
                {
                    inverseNullValueSet.Add(key);
                }
            }
            else
            {
                if (!inverse.ContainsKey(value))
                {
                    inverse[value] = new HashSet<T>();
                }

                if (!inverse[value].Contains(key))
                {
                    inverse[value].Add(key);
                }
            }

            direct[key] = value;
        }
    }

    public bool ContainsKey(T key)
    {
        return direct.ContainsKey(key);
    }

    public bool ContainsInverseKey(S key)
    {
        return valueIsNullable && key == null ? inverseNullValueSet != null : inverse.ContainsKey(key);
    }

    public HashSet<T> FindInverse(S key)
    {
        if (valueIsNullable && key == null)
        {
            return inverseNullValueSet ?? new HashSet<T>();
        }

        return inverse.ContainsKey(key)
            ? inverse[key]
            : new HashSet<T>();
    }

    public void Remove(T key)
    {
        if (direct.ContainsKey(key))
        {
            var oldvalue = direct[key];
            if (valueIsNullable && oldvalue == null)
            {
                if (inverseNullValueSet != null && inverseNullValueSet.Contains(key))
                {
                    inverseNullValueSet.Remove(key);
                }
            }
            else
            {
                if (inverse.ContainsKey(oldvalue) && inverse[oldvalue].Contains(key))
                {
                    inverse[oldvalue].Remove(key);
                }
            }

            direct.Remove(key);
        }
    }

    public void Clear()
    {
        direct.Clear();
        inverse.Clear();
        inverseNullValueSet = null;
    }
}

public class BiDictionaryManyToMany<T, S>
{
    private Dictionary<T, HashSet<S>> direct = new();
    private Dictionary<S, HashSet<T>> inverse = new();

    public BiDictionaryManyToMany()
    {
    }

    public BiDictionaryManyToMany(Dictionary<T, HashSet<S>> input)
    {
        direct = input;
        inverse = new Dictionary<S, HashSet<T>>();
        foreach (var t in input.Keys)
        {
            foreach (var s in input[t])
            {
                if (!inverse.ContainsKey(s))
                {
                    inverse[s] = new HashSet<T>();
                }

                inverse[s].Add(t);
            }
        }
    }

    public HashSet<S> this[T key]
    {
        get => direct[key];
        set
        {
            if (direct.ContainsKey(key))
            {
                var oldvalue = direct[key];
                if (oldvalue.SetEquals(value))
                {
                    return;
                }

                foreach (var s in oldvalue.ToList())
                {
                    if (inverse.ContainsKey(s))
                    {
                        if (inverse[s].Contains(key))
                        {
                            inverse[s].Remove(key);
                        }

                        if (inverse[s].Count == 0)
                        {
                            inverse.Remove(s);
                        }
                    }
                }
            }

            foreach (var s in value)
            {
                if (!inverse.ContainsKey(s))
                {
                    inverse[s] = new HashSet<T>();
                }

                inverse[s].Add(key);
            }

            direct[key] = value;
        }
    }

    public bool ContainsKey(T key)
    {
        return direct.ContainsKey(key);
    }

    public bool ContainsInverseKey(S key)
    {
        return inverse.ContainsKey(key);
    }

    public HashSet<T> FindInverse(S k)
    {
        return inverse.ContainsKey(k)
            ? inverse[k]
            : new HashSet<T>();
    }

    public void Remove(T key)
    {
        if (direct.ContainsKey(key))
        {
            var oldvalue = direct[key];
            foreach (var s in oldvalue.ToList())
            {
                if (inverse.ContainsKey(s))
                {
                    if (inverse[s].Contains(key))
                    {
                        inverse[s].Remove(key);
                    }

                    if (inverse[s].Count == 0)
                    {
                        inverse.Remove(s);
                    }
                }
            }

            direct.Remove(key);
        }
    }

    public void Clear()
    {
        direct.Clear();
        inverse.Clear();
    }
}

public static class Extensions
{
    public static bool HasItems<T>(this List<T> org)
    {
        return org != null && org.Count > 0;
    }
}
