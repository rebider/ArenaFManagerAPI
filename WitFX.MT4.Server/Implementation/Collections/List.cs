using System;
using System.Diagnostics;
using Sys = System.Collections.Generic;

namespace WitFX.MT4.Server.Implementation.Collections
{
    public sealed class List<T> : Sys.List<T>
    {
        public void push_back(T item) => Add(item);
        public int size() => Count;

        public void erase(iterator position)
        {
            Debug.Assert(position.List == this);
            RemoveAt(position.Index);
        }

        public iterator begin() => Count > 0 ? new iterator(this, 0) : iterator.End;
        public iterator end() => iterator.End;

        public struct iterator : IEquatable<iterator>
        {
            public static readonly iterator End = new iterator();

            internal readonly List<T> List;
            private readonly int _position;

            public iterator(List<T> list, int index)
            {
                Debug.Assert(list != null);
                Debug.Assert(index >= 0);
                List = list;
                _position = index + 1;
            }

            public bool IsAvailable => _position > 0;
            public int Index => _position - 1;

            public T Current
            {
                get
                {
                    Debug.Assert(Index >= 0 && Index < List.Count);
                    return IsAvailable ? List[Index] : throw new InvalidOperationException();
                }
            }

            public static implicit operator T(iterator it) => it.Current;

            public static iterator operator ++(iterator it)
            {
                var index = it.Index;
                var list = it.List;

                if (index >= 0 && list != null && index + 1 < list.Count)
                    return new iterator(list, index + 1);

                return End;
            }

            public static bool operator ==(iterator x, iterator y) => x.Equals(y);
            public static bool operator !=(iterator x, iterator y) => !x.Equals(y);
            public override bool Equals(object obj) => Equals((iterator)obj);

            public bool Equals(iterator it)
            {
                Debug.Assert(List != null && it.List != null ? List == it.List : true);
                return _position == it._position;
            }

            public override int GetHashCode() => _position;
        }
    }
}
