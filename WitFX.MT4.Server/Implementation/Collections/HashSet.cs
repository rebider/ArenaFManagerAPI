using Sys = System.Collections.Generic;

#pragma warning disable CS0660 // Type defines operator == or operator != but does not override Object.Equals(object o)
#pragma warning disable CS0661 // Type defines operator == or operator != but does not override Object.GetHashCode()

namespace WitFX.MT4.Server.Implementation.Collections
{
    public sealed class HashSet<T> : Sys.HashSet<T>
    {
        public void insert(T item) => Add(item);

        public iterator find(T item)
            => Contains(item) ? new iterator(item) : _end;

        public iterator begin()
        {
            var enumerator = GetEnumerator();
            return enumerator.MoveNext() ? new iterator(enumerator) : _end;
        }

        public iterator end() => _end;
        private static readonly iterator _end = new iterator();

        public sealed class iterator : EnumerableIterator<T>
        {
            public iterator() : base() { }
            public iterator(T value) : base(value) { }
            public iterator(Sys.IEnumerator<T> enumerator) : base(enumerator) { }

            public static iterator operator ++(iterator it)
            {
                it.Increment();
                return it;
            }
        }
    }
}
