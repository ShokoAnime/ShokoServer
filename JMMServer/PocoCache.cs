﻿/* 
 
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NutzCode.InMemoryIndex
{
    //NOTE PocoCache is not thread SAFE
    

    public class PocoCache<T, S> where S: class
    {
        private Dictionary<T, S> _dict;
        private Func<S, T> _func;
        private List<Action<T,S>> _updates=new List<Action<T, S>>();
        private List<Action<T>> _deletes=new List<Action<T>>(); 

        public Dictionary<T, S>.KeyCollection Keys => _dict.Keys;
        public Dictionary<T, S>.ValueCollection Values => _dict.Values;

        public PocoIndex<T, S, U> CreateIndex<U>(Func<S, U> paramfunc1)
        {
            return new PocoIndex<T, S, U>(this,paramfunc1);
        }

        public PocoIndex<T, S, N, U> CreateIndex<N,U>(Func<S, N> paramfunc1, Func<S, U> paramfunc2)
        {
            return new PocoIndex<T, S, N, U>(this, paramfunc1,paramfunc2);
        }
        public PocoIndex<T, S, N, R, U> CreateIndex<N,R,U>(Func<S, N> paramfunc1, Func<S, R> paramfunc2, Func<S, U> paramfunc3)
        {
            return new PocoIndex<T, S, N, R, U>(this, paramfunc1, paramfunc2, paramfunc3);
        }

        public PocoCache(IEnumerable<S> objectlist, Func<S, T> keyfunc)
        {
            _func = keyfunc;
            _dict = objectlist.ToDictionary(keyfunc, a => a);
        }
        
        internal void AddChain(Action<T,S> updatefunc, Action<T> deletefunc)
        {
            _updates.Add(updatefunc);
            _deletes.Add(deletefunc);
        }

        public S Get(T key)
        {
            if (_dict.ContainsKey(key))
                return _dict[key];
            return null;
        }
        public void Update(S obj)
        {
            T key = _func(obj);
            foreach (Action<T, S> upd in _updates)
                upd(key, obj);
            _dict[key] = obj;
        }

        public void Remove(S obj)
        {
            T key = _func(obj);
            foreach (Action<T> del in _deletes)
                del(key);
            if (_dict.ContainsKey(key))
                _dict.Remove(key);
        }


    }


    public class PocoIndex<T,S,U> where S:class
    {
        internal PocoCache<T, S> _cache;
        internal BiDictionaryOneToMany<T, U> _dict;
        internal Func<S, U> _func;

        internal PocoIndex(PocoCache<T,S> cache, Func<S,U> paramfunc1)
        {
            _cache = cache;
            _dict = new BiDictionaryOneToMany<T, U>(_cache.Keys.ToDictionary(a => a, a => paramfunc1(_cache.Get(a))));
            _func = paramfunc1;
            cache.AddChain(Update,Remove);
        }

        public S GetOne(U key)
        {
            if (_cache == null || !_dict.ContainsInverseKey(key))
                return null;
            HashSet<T> hashes = _dict.FindInverse(key);
            if (hashes.Count == 0)
                return null;
            return _cache.Get(hashes.First());
        }

        public List<S> GetMultiple(U key)
        {
            if (_cache == null || !_dict.ContainsInverseKey(key))
                return new List<S>();
            return _dict.FindInverse(key).Select(a => _cache.Get(a)).ToList();
        }

        internal void Update(T key, S obj)
        {
            _dict[key] = _func(obj);
        }

        internal void Remove(T key)
        {
            _dict.Remove(key);
        }

    }

    public class PocoIndex<T, S, N, U> where S : class
    {
        public PocoIndex<T, S, Tuple<N,U>> _index;

        internal PocoIndex(PocoCache<T, S> cache, Func<S, N> paramfunc1, Func<S,U> paramfunc2)
        {
            _index=new PocoIndex<T, S, Tuple<N, U>>(cache, a => new Tuple<N, U>(paramfunc1(a), paramfunc2(a)));
        }

        public S GetOne(N key1, U key2)
        {
            Tuple<N,U> key=new Tuple<N,U>(key1,key2);
            return _index.GetOne(key);
        }

        public List<S> GetMultiple(N key1, U key2)
        {
            Tuple<N, U> key = new Tuple<N, U>(key1, key2);
            return _index.GetMultiple(key);
        }

    }
    public class PocoIndex<T, S, N, R, U> where S : class
    {
        public PocoIndex<T, S, Tuple<N, R, U>> _index;

        internal PocoIndex(PocoCache<T, S> cache, Func<S, N> paramfunc1, Func<S, R> paramfunc2, Func<S, U> paramfunc3)
        {
            _index = new PocoIndex<T, S, Tuple<N, R, U>>(cache, a => new Tuple<N, R, U>(paramfunc1(a), paramfunc2(a),paramfunc3(a)));
        }

        public S GetOne(N key1, R key2, U key3)
        {
            Tuple<N, R, U> key = new Tuple<N, R, U>(key1, key2, key3);
            return _index.GetOne(key);
        }

        public List<S> GetMultiple(N key1, R key2, U key3)
        {
            Tuple<N, R, U> key = new Tuple<N, R, U>(key1, key2, key3);
            return _index.GetMultiple(key);
        }


    }


    public class BiDictionaryOneToOne<T,S> 
    {
        private Dictionary<T, S> direct = new Dictionary<T, S>();
        private Dictionary<S, T> inverse = new Dictionary<S, T>();

        public BiDictionaryOneToOne()
        {
        }

        public BiDictionaryOneToOne(Dictionary<T,S> input)
        {
            direct = input;
            inverse = direct.ToDictionary(a => a.Value, a => a.Key);
        } 

        public S this[T key]
        {
            get { return direct[key]; }
            set
            {
                if (direct.ContainsKey(key))
                {
                    S oldvalue = direct[key];
                    if (oldvalue.Equals(value))
                        return;
                    if (inverse.ContainsKey(oldvalue))
                        inverse.Remove(oldvalue);
                }
                if (!inverse.ContainsKey(value))
                    inverse.Add(value,key);
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
                S n = direct[value];
                inverse.Remove(n);
                direct.Remove(value);
            }
        }
    }

    public class BiDictionaryOneToMany<T,S> 
    {
        private Dictionary<T, S> direct = new Dictionary<T, S>();
        private Dictionary<S, HashSet<T>> inverse = new Dictionary<S, HashSet<T>>();

        public BiDictionaryOneToMany()
        {
            
        } 
        public BiDictionaryOneToMany(Dictionary<T,S> input)
        {
            direct = input;
            inverse = direct.GroupBy(a => a.Value).ToDictionary(a => a.Key, a => a.Select(b=>b.Key).ToHashSet());
        }  
        public S this[T key]
        {
            get { return direct[key]; }
            set
            {
                if (direct.ContainsKey(key))
                {
                    S oldvalue = direct[key];
                    if (oldvalue.Equals(value))
                        return;
                    if (inverse.ContainsKey(oldvalue))
                    {
                        if (inverse[oldvalue].Contains(key))
                            inverse[oldvalue].Remove(key);
                    }
                }
                if (!inverse.ContainsKey(value))
                    inverse[value] = new HashSet<T>();
                if (!inverse[value].Contains(key))
                    inverse[value].Add(key);
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
            if (inverse.ContainsKey(k))
                return inverse[k];
            return new HashSet<T>();
        }

        public void Remove(T key)
        {
            if (direct.ContainsKey(key))
            {
                S oldvalue = direct[key];
                if (inverse.ContainsKey(oldvalue))
                {
                    if (inverse[oldvalue].Contains(key))
                        inverse[oldvalue].Remove(key);
                }
                direct.Remove(key);
            }
        }
    }
    public static class Extensions
    {
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> d)
        {
            return new HashSet<T>(d);
        }

        public static bool HasItems<T>(this List<T> org)
        {
            if (org != null && org.Count > 0)
                return true;
            return false;
        }
    }
}
