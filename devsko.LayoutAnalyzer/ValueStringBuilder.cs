// Licensed to the.NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace devsko.LayoutAnalyzer
{
    //internal ref partial struct ValueStringBuilder
    //{
    //    private char[]? _charsToReturnToPool;
    //    private Span<char> _chars;
    //    private int _charsPos;

    //    private TokenSpan[]? _tokensToReturnToPool;
    //    private Span<TokenSpan> _tokens;
    //    private int _tokensPos;

    //    public ValueStringBuilder(Span<char> initialChars, Span<TokenSpan> initialTokens)
    //    {
    //        _charsToReturnToPool = null;
    //        _chars = initialChars;
    //        _charsPos = 0;

    //        _tokensToReturnToPool = null;
    //        _tokens = initialTokens;
    //        _tokensPos = 0;
    //    }

    //    public ValueStringBuilder(int initialCharsCapacity, int initialTokensCapacity)
    //    {
    //        _charsToReturnToPool = ArrayPool<char>.Shared.Rent(initialCharsCapacity);
    //        _chars = _charsToReturnToPool;
    //        _charsPos = 0;

    //        _tokensToReturnToPool = ArrayPool<TokenSpan>.Shared.Rent(initialTokensCapacity);
    //        _tokens = _tokensToReturnToPool;
    //        _tokensPos = 0;
    //    }

    //    public int Length
    //    {
    //        get => _charsPos;
    //        set
    //        {
    //            Debug.Assert(value >= 0);
    //            Debug.Assert(value <= _chars.Length);
    //            _charsPos = value;
    //        }
    //    }

    //    public int Capacity => _chars.Length;

    //    public void EnsureCapacity(int capacity)
    //    {
    //        // This is not expected to be called this with negative capacity
    //        Debug.Assert(capacity >= 0);

    //        // If the caller has a bug and calls this with negative capacity, make sure to call Grow to throw an exception.
    //        if ((uint)capacity > (uint)_chars.Length)
    //            Grow(capacity - _charsPos);
    //    }

    //    /// <summary>
    //    /// Get a pinnable reference to the builder.
    //    /// Does not ensure there is a null char after <see cref="Length"/>
    //    /// This overload is pattern matched in the C# 7.3+ compiler so you can omit
    //    /// the explicit method call, and write eg "fixed (char* c = builder)"
    //    /// </summary>
    //    public ref char GetPinnableReference()
    //    {
    //        return ref MemoryMarshal.GetReference(_chars);
    //    }

    //    /// <summary>
    //    /// Get a pinnable reference to the builder.
    //    /// </summary>
    //    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    //    public ref char GetPinnableReference(bool terminate)
    //    {
    //        if (terminate)
    //        {
    //            EnsureCapacity(Length + 1);
    //            _chars[Length] = '\0';
    //        }
    //        return ref MemoryMarshal.GetReference(_chars);
    //    }

    //    public ref char this[int index]
    //    {
    //        get
    //        {
    //            Debug.Assert(index < _charsPos);
    //            return ref _chars[index];
    //        }
    //    }

    //    public override string ToString()
    //    {
    //        string s = _chars.Slice(0, _charsPos).ToString();
    //        Dispose();
    //        return s;
    //    }

    //    /// <summary>Returns the underlying storage of the builder.</summary>
    //    public Span<char> RawChars => _chars;

    //    /// <summary>
    //    /// Returns a span around the contents of the builder.
    //    /// </summary>
    //    /// <param name="terminate">Ensures that the builder has a null char after <see cref="Length"/></param>
    //    public ReadOnlySpan<char> AsSpan(bool terminate)
    //    {
    //        if (terminate)
    //        {
    //            EnsureCapacity(Length + 1);
    //            _chars[Length] = '\0';
    //        }
    //        return _chars.Slice(0, _charsPos);
    //    }

    //    public ReadOnlySpan<char> AsSpan() => _chars.Slice(0, _charsPos);
    //    public ReadOnlySpan<char> AsSpan(int start) => _chars.Slice(start, _charsPos - start);
    //    public ReadOnlySpan<char> AsSpan(int start, int length) => _chars.Slice(start, length);

    //    public bool TryCopyTo(Span<char> destination, out int charsWritten)
    //    {
    //        if (_chars.Slice(0, _charsPos).TryCopyTo(destination))
    //        {
    //            charsWritten = _charsPos;
    //            Dispose();
    //            return true;
    //        }
    //        else
    //        {
    //            charsWritten = 0;
    //            Dispose();
    //            return false;
    //        }
    //    }

    //    public void Insert(int index, char value, int count)
    //    {
    //        if (_charsPos > _chars.Length - count)
    //        {
    //            Grow(count);
    //        }

    //        int remaining = _charsPos - index;
    //        _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
    //        _chars.Slice(index, count).Fill(value);
    //        _charsPos += count;
    //    }

    //    public void Insert(int index, string? s)
    //    {
    //        if (s == null)
    //        {
    //            return;
    //        }

    //        int count = s.Length;

    //        if (_charsPos > (_chars.Length - count))
    //        {
    //            Grow(count);
    //        }

    //        int remaining = _charsPos - index;
    //        _chars.Slice(index, remaining).CopyTo(_chars.Slice(index + count));
    //        s.AsSpan().CopyTo(_chars.Slice(index));
    //        _charsPos += count;
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public void Append(char c)
    //    {
    //        int pos = _charsPos;
    //        if ((uint)pos < (uint)_chars.Length)
    //        {
    //            _chars[pos] = c;
    //            _charsPos = pos + 1;
    //        }
    //        else
    //        {
    //            GrowAndAppend(c);
    //        }
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public void Append(string? s)
    //    {
    //        if (s == null)
    //        {
    //            return;
    //        }

    //        int pos = _charsPos;
    //        if (s.Length == 1 && (uint)pos < (uint)_chars.Length) // very common case, e.g. appending strings from NumberFormatInfo like separators, percent symbols, etc.
    //        {
    //            _chars[pos] = s[0];
    //            _charsPos = pos + 1;
    //        }
    //        else
    //        {
    //            AppendSlow(s);
    //        }
    //    }

    //    private void AppendSlow(string s)
    //    {
    //        int pos = _charsPos;
    //        if (pos > _chars.Length - s.Length)
    //        {
    //            Grow(s.Length);
    //        }

    //        s.AsSpan().CopyTo(_chars.Slice(pos));
    //        _charsPos += s.Length;
    //    }

    //    public void Append(char c, int count)
    //    {
    //        if (_charsPos > _chars.Length - count)
    //        {
    //            Grow(count);
    //        }

    //        Span<char> dst = _chars.Slice(_charsPos, count);
    //        for (int i = 0; i < dst.Length; i++)
    //        {
    //            dst[i] = c;
    //        }
    //        _charsPos += count;
    //    }

    //    public unsafe void Append(char* value, int length)
    //    {
    //        int pos = _charsPos;
    //        if (pos > _chars.Length - length)
    //        {
    //            Grow(length);
    //        }

    //        Span<char> dst = _chars.Slice(_charsPos, length);
    //        for (int i = 0; i < dst.Length; i++)
    //        {
    //            dst[i] = *value++;
    //        }
    //        _charsPos += length;
    //    }

    //    public void Append(ReadOnlySpan<char> value)
    //    {
    //        int pos = _charsPos;
    //        if (pos > _chars.Length - value.Length)
    //        {
    //            Grow(value.Length);
    //        }

    //        value.CopyTo(_chars.Slice(_charsPos));
    //        _charsPos += value.Length;
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public Span<char> AppendSpan(int length)
    //    {
    //        int origPos = _charsPos;
    //        if (origPos > _chars.Length - length)
    //        {
    //            Grow(length);
    //        }

    //        _charsPos = origPos + length;
    //        return _chars.Slice(origPos, length);
    //    }

    //    [MethodImpl(MethodImplOptions.NoInlining)]
    //    private void GrowAndAppend(char c)
    //    {
    //        Grow(1);
    //        Append(c);
    //    }

    //    /// <summary>
    //    /// Resize the internal buffer either by doubling current buffer size or
    //    /// by adding <paramref name="additionalCapacityBeyondPos"/> to
    //    /// <see cref="_charsPos"/> whichever is greater.
    //    /// </summary>
    //    /// <param name="additionalCapacityBeyondPos">
    //    /// Number of chars requested beyond current position.
    //    /// </param>
    //    [MethodImpl(MethodImplOptions.NoInlining)]
    //    private void Grow(int additionalCapacityBeyondPos)
    //    {
    //        Debug.Assert(additionalCapacityBeyondPos > 0);
    //        Debug.Assert(_charsPos > _chars.Length - additionalCapacityBeyondPos, "Grow called incorrectly, no resize is needed.");

    //        // Make sure to let Rent throw an exception if the caller has a bug and the desired capacity is negative
    //        char[] poolArray = ArrayPool<char>.Shared.Rent((int)Math.Max((uint)(_charsPos + additionalCapacityBeyondPos), (uint)_chars.Length * 2));

    //        _chars.Slice(0, _charsPos).CopyTo(poolArray);

    //        char[]? toReturn = _charsToReturnToPool;
    //        _chars = _charsToReturnToPool = poolArray;
    //        if (toReturn != null)
    //        {
    //            ArrayPool<char>.Shared.Return(toReturn);
    //        }
    //    }

    //    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //    public void Dispose()
    //    {
    //        char[]? toReturn = _charsToReturnToPool;
    //        this = default; // for safety, to avoid using pooled array if this instance is erroneously appended to again
    //        if (toReturn != null)
    //        {
    //            ArrayPool<char>.Shared.Return(toReturn);
    //        }
    //    }
    //}
}