using System.Diagnostics;
using Sys = System.Collections.Generic;

#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()

namespace WitFX.MT4.Server.Implementation.Collections
{
    public sealed class Dictionary<TKey, TValue> : Sys.Dictionary<TKey, TValue>
    {
        public int size() => Count;

        public void insert((TKey, TValue) pair)
        {
            Debug.Assert(!ContainsKey(pair.Item1));
            //if (!ContainsKey(pair.Item1))
            Add(pair.Item1, pair.Item2);
        }

        public bool erase(TKey key) => Remove(key);
        public bool erase(iterator it) => Remove(it.first);

        public iterator find(TKey key)
            => TryGetValue(key, out var value)
                ? new iterator(this, new Sys.KeyValuePair<TKey, TValue>(key, value))
                : _end;

        public iterator begin()
        {
            var enumerator = GetEnumerator();
            return enumerator.MoveNext() ? new iterator(this, enumerator) : _end;
        }

        public iterator end() => _end;
        private static readonly iterator _end = new iterator();

        public sealed class iterator : EnumerableIterator<Sys.KeyValuePair<TKey, TValue>>
        {
            private readonly Dictionary<TKey, TValue> _map;

            public iterator() : base() { }

            public iterator(Dictionary<TKey, TValue> map, Sys.KeyValuePair<TKey, TValue> pair)
                : base(pair)
            {
                Debug.Assert(map != null);
                _map = map;
            }

            public iterator(
                Dictionary<TKey, TValue> map,
                Sys.IEnumerator<Sys.KeyValuePair<TKey, TValue>> enumerator)
                : base(enumerator)
            {
                Debug.Assert(map != null);
                _map = map;
            }

            public TKey first => Current.Key;

            public TValue second
            {
                get => Current.Value;
                set
                {
                    var key = Current.Key;
                    _map[key] = value;
                    Current = new Sys.KeyValuePair<TKey, TValue>(key, value);
                }
            }

            protected override bool Equals(
                Sys.KeyValuePair<TKey, TValue> x, Sys.KeyValuePair<TKey, TValue> y)
                => Sys.EqualityComparer<TKey>.Default.Equals(x.Key, y.Key);

            protected override int GetHashCode(Sys.KeyValuePair<TKey, TValue> value)
                => Sys.EqualityComparer<TKey>.Default.GetHashCode(value.Key);

            public static iterator operator ++(iterator it)
            {
                it.Increment();
                return it;
            }
        }
    }
}
