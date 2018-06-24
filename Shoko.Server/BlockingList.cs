using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;

namespace Shoko.Server
{
    public class BlockingList<T> : IEnumerable<T> where T : class
    {
        private readonly List<T> _list;
        private readonly object _syncRoot;
        private readonly int _maxCap;

        public int Count
        {
            get
            {
                lock (_syncRoot)
                    return _list.Count;
            }
        }

        public void Add(T data)
        {
            if (data == null) throw new ArgumentNullException("data");

            lock (_syncRoot)
            {
                //poll every 100ms until there is space in list to add
                while (_list.Count >= _maxCap)
                {
                    Monitor.Wait(_syncRoot, 100);
                    //if it throws, it throws
                }

                _list.Add(data);
                Monitor.Pulse(_syncRoot);
            }
        }

        public void Remove(T data)
        {
            lock (_syncRoot)
            {
                if (_list.Remove(data))
                    //something was removed, signal
                    Monitor.Pulse(_syncRoot);
            }
        }

        public T GetNextItem()
        {
            lock (_syncRoot)
            {
                while (0 == _list.Count)
                {
                    //poll every 100ms if there is something to return
                    try
                    {
                        Monitor.Wait(_syncRoot, 100);
                    }
                    catch (Exception)
                    {
                        //yield doesnt do exceptions
                        return null;
                    }
                }
                return _list[0];
            }
        }

        public BlockingList(int capacity)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException("capacity");

            _maxCap = capacity;
            _list = new List<T>(_maxCap);
            _syncRoot = ((ICollection) _list).SyncRoot;
        }

        public BlockingList()
        {
            _maxCap = int.MaxValue;
            _list = new List<T>();
            _syncRoot = ((ICollection) _list).SyncRoot;
        }


        public bool Contains(T data)
        {
            lock (_syncRoot)
                return _list.Contains(data);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            T item;
            do {
                item = GetNextItem();
                if (null != item)
                    yield return item;
                //else drop out
            } while (null != item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>) this).GetEnumerator();
        }
    }
}