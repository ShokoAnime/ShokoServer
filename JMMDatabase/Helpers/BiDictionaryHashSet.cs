using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMDatabase.Extensions;

namespace JMMDatabase.Helpers
{

    public class BiDictionaryHashSet<T> 
    {
        private Dictionary<T, HashSet<T>> direct = new Dictionary<T, HashSet<T>>();
        private Dictionary<T, HashSet<T>> inverse = new Dictionary<T, HashSet<T>>();


        public HashSet<T> FindInverse(T k)
        {
            if (inverse.ContainsKey(k))
                return inverse[k];
            return new HashSet<T>();
        }

        public HashSet<T> FindDirect(T k)
        {
            if (direct.ContainsKey(k))
                return direct[k];
            return new HashSet<T>();
        } 


        public void Update(T value, HashSet<T> nvalues)
        {
            HashSet<T> oldhashes = new HashSet<T>();
            if (direct.ContainsKey(value))
                oldhashes = direct[value];
            foreach (T k in nvalues.Except(oldhashes))
            {
                if (inverse.ContainsKey(k))
                {
                    if (!inverse[k].Contains(value))
                        inverse[k].Add(value);
                }
                else
                    inverse[k] = new HashSet<T> { value };
            }
            foreach (T k in oldhashes.Except(nvalues))
            {
                if (inverse.ContainsKey(k))
                {
                    if (inverse[k].Contains(value))
                        inverse[k].Remove(value);
                }
            }
            direct[value] = nvalues;
        }

        public void Add(T value, HashSet<T> nvalues)
        {
            direct[value] = nvalues;
            foreach (T k in nvalues)
            {
                if (inverse.ContainsKey(k))
                {
                    if (!inverse[k].Contains(value))
                        inverse[k].Add(value);
                }
                else
                {
                    inverse[k]=new HashSet<T> { value };
                }
            }
        }

        public void Delete(T value)
        {
            if (direct.ContainsKey(value))
            {
                foreach (T k in direct[value])
                {
                    if (inverse.ContainsKey(k))
                    {
                        if (inverse[k].Contains(value))
                            inverse[k].Remove(value);
                        if (inverse[k].Count == 0)
                            inverse.Remove(k);
                    }
                }
                direct.Remove(value);
            }
        }

    }
    public class BiDictionary<T> where T : class
    {
        private Dictionary<T, T> direct = new Dictionary<T, T>();
        private Dictionary<T, T> inverse = new Dictionary<T, T>();


        public T FindInverse(T k)
        {
            return inverse.Find(k);
        }

        public T FindDirect(T k)
        {
            return direct.Find(k);
        }


        public void Update(T value, T nvalue)
        {
            T oldvalue = null;
            if (direct.ContainsKey(value))
            {
                oldvalue = direct[value];
                if (oldvalue == nvalue)
                    return;
                if (inverse.ContainsKey(oldvalue))
                    inverse.Remove(oldvalue);
            }
            direct[value] = nvalue;
        }

        public void Add(T value, T nvalue)
        {
            direct[value] = nvalue;
            if (!inverse.ContainsKey(nvalue))
            {
                inverse.Add(nvalue, value);
            }
        }

        public void Delete(T value)
        {
            if (direct.ContainsKey(value))
            {
                T n = direct[value];
                inverse.Remove(n);
                direct.Remove(value);
            }
        }

    }
}
