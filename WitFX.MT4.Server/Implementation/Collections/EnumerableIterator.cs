using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace WitFX.MT4.Server.Implementation.Collections
{
    public abstract class EnumerableIterator<T> : IEquatable<EnumerableIterator<T>>
    {
        private bool _isAvailable;
        private T _current;
        private readonly IEnumerator<T> _enumerator;

        protected EnumerableIterator() { }

        protected EnumerableIterator(T value)
        {
            _isAvailable = true;
            _current = value;
        }

        protected EnumerableIterator(IEnumerator<T> enumerator)
        {
            Debug.Assert(enumerator != null);
            _isAvailable = true;
            _current = enumerator.Current;
            _enumerator = enumerator;
        }

        public T Current {
            get => _isAvailable ? _current : throw new InvalidOperationException();
            protected set => _current = _isAvailable ? value : throw new InvalidOperationException();
        }

        protected void Increment()
        {
            if (_isAvailable)
            {
                Debug.Assert(_enumerator != null);

                if (_enumerator != null)
                {
                    if (_isAvailable = _enumerator.MoveNext())
                        _current = _enumerator.Current;
                    else
                        _current = default(T);
                }
                else
                    throw new NotSupportedException();
            }
        }

        public static implicit operator T(EnumerableIterator<T> it) => it.Current;

        public static bool operator ==(EnumerableIterator<T> x, EnumerableIterator<T> y)
            => x.Equals(y);

        public static bool operator !=(EnumerableIterator<T> x, EnumerableIterator<T> y)
            => !x.Equals(y);

        public override bool Equals(object obj)
            => Equals((EnumerableIterator<T>)obj);

        public bool Equals(EnumerableIterator<T> it)
        {
            if (_isAvailable != it._isAvailable)
                return false;

            if (_isAvailable)
                return Equals(_current, it._current);

            return true;
        }

        protected virtual bool Equals(T x, T y)
            => EqualityComparer<T>.Default.Equals(x, y);

        public override int GetHashCode()
            => _isAvailable ? GetHashCode(_current) : 0;

        protected virtual int GetHashCode(T value)
            => EqualityComparer<T>.Default.GetHashCode(value);
    }
}
