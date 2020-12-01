using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace ElasticKilla.Core.Collections
{
    public class ConcurrentSet<T> : ISet<T>
    {
        private readonly ConcurrentDictionary<T, byte> _data;

        public int Count => _data.Count;

        public bool IsReadOnly => false;

        public bool Add(T item) => _data.TryAdd(item, 0);

        void ICollection<T>.Add(T item)
        {
            if (item == null)
                throw new ArgumentException("Item can't be null.");

            if (!Add(item))
                throw new ArgumentException("Item already exists in set.");
        }

        public bool TryAdd(T item) => _data.TryAdd(item, 0);

        public bool Remove(T item) => TryRemove(item);

        public bool TryRemove(T item) => _data.TryRemove(item, out _);

        public void Clear()
        {
            _data.Clear();
        }

        public void UnionWith(IEnumerable<T> other)
        {
            foreach (var item in other)
                TryAdd(item);
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            foreach (var item in this)
            {
                if (!enumerable.Contains(item))
                    TryRemove(item);
            }
        }
        
        public void ExceptWith(IEnumerable<T> other)
        {
            foreach (var item in other)
                TryRemove(item);
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return this.AsParallel().All(enumerable.Contains);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return other.AsParallel().All(Contains);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return this.Count != enumerable.Count && IsSupersetOf(enumerable);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return Count != enumerable.Count && IsSubsetOf(enumerable);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return other.AsParallel().Any(Contains);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            var enumerable = other as IList<T> ?? other.ToArray();
            return Count == enumerable.Count && enumerable.AsParallel().All(Contains);
        }

        public bool Contains(T item)
        {
            return item != null && _data.ContainsKey(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            _data.Keys.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _data.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public ConcurrentSet()
        {
            _data = new ConcurrentDictionary<T, byte>();
        }

        public ConcurrentSet(IEnumerable<T> collection)
        {
            _data = new ConcurrentDictionary<T, byte>(collection.Select(_ => new KeyValuePair<T, byte>(_, 0)));
        }
    }
}