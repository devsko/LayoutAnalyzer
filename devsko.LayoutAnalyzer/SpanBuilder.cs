using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace devsko.LayoutAnalyzer
{
    public ref struct SpanBuilder<T>
    {
        private T[]? _valueToReturnToPool;
        private Span<T> _value;
        private int _pos;

        public SpanBuilder(Span<T> initialValue)
        {
            _valueToReturnToPool = null;
            _value = initialValue;
            _pos = 0;
        }

        public SpanBuilder(int initialCapacity)
        {
            _valueToReturnToPool = ArrayPool<T>.Shared.Rent(initialCapacity);
            _value = _valueToReturnToPool;
            _pos = 0;
        }

        public int Length
        {
            get => _pos;
            set
            {
                Debug.Assert(value >= 0);
                Debug.Assert(value <= _value.Length);
                _pos = value;
            }
        }

        public int Capacity 
            => _value.Length;

        public void EnsureCapacity(int capacity)
        {
            // This is not expected to be called this with negative capacity
            Debug.Assert(capacity >= 0);

            // If the caller has a bug and calls this with negative capacity, make sure to call Grow to throw an exception.
            if ((uint)capacity > (uint)_value.Length)
                Grow(capacity - _pos);
        }

        /// <summary>
        /// Get a pinnable reference to the builder.
        /// This overload is pattern matched in the C# 7.3+ compiler so you can omit
        /// the explicit method call, and write eg "fixed (T* c = builder)"
        /// </summary>
        public ref T GetPinnableReference()
        {
            return ref MemoryMarshal.GetReference(_value);
        }

        public ref T this[int index]
        {
            get
            {
                Debug.Assert(index < _pos);
                return ref _value[index];
            }
        }

        public override string ToString()
        {
            string s = _value.Slice(0, _pos).ToString();
            Dispose();
            return s;
        }

        public T[] ToArray()
        {
            T[] a = _value.Slice(0, _pos).ToArray();
            Dispose();
            return a;
        }

        /// <summary>Returns the underlying storage of the builder.</summary>
        public Span<T> RawValue 
            => _value;

        public ReadOnlySpan<T> AsSpan()
            => _value.Slice(0, _pos);

        public ReadOnlySpan<T> AsSpan(int start) 
            => _value[start.._pos];

        public ReadOnlySpan<T> AsSpan(int start, int length) 
            => _value.Slice(start, length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Append(T c)
        {
            int pos = _pos;
            if ((uint)pos < (uint)_value.Length)
            {
                _value[pos] = c;
                _pos = pos + 1;
            }
            else
            {
                GrowAndAppend(c);
            }
        }

        public void Append(T c, int count)
        {
            if (_pos > _value.Length - count)
            {
                Grow(count);
            }

            Span<T> dst = _value.Slice(_pos, count);
            for (int i = 0; i < dst.Length; i++)
            {
                dst[i] = c;
            }
            _pos += count;
        }

        public void Append(ReadOnlySpan<T> value)
        {
            int pos = _pos;
            if (pos > _value.Length - value.Length)
            {
                Grow(value.Length);
            }

            value.CopyTo(_value[_pos..]);
            _pos += value.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<T> AppendSpan(int length)
        {
            int origPos = _pos;
            if (origPos > _value.Length - length)
            {
                Grow(length);
            }

            _pos = origPos + length;
            return _value.Slice(origPos, length);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void GrowAndAppend(T t)
        {
            Grow(1);
            Append(t);
        }

        /// <summary>
        /// Resize the internal buffer either by doubling current buffer size or
        /// by adding <paramref name="additionalCapacityBeyondPos"/> to
        /// <see cref="_pos"/> whichever is greater.
        /// </summary>
        /// <param name="additionalCapacityBeyondPos">
        /// Number of chars requested beyond current position.
        /// </param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Grow(int additionalCapacityBeyondPos)
        {
            Debug.Assert(additionalCapacityBeyondPos > 0);
            Debug.Assert(_pos > _value.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

            // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative
            T[] poolArray = ArrayPool<T>.Shared.Rent((int)Math.Max((uint)(_pos + additionalCapacityBeyondPos), (uint)_value.Length * 2));

            _value.Slice(0, _pos).CopyTo(poolArray);

            T[]? toReturn = _valueToReturnToPool;
            _value = _valueToReturnToPool = poolArray;
            if (toReturn != null)
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            T[]? toReturn = _valueToReturnToPool;
            this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
            if (toReturn != null)
            {
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }
    }
}
