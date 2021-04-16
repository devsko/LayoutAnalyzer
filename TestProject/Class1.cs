using System;   
using System.Collections.Generic;   
using System.Runtime.InteropServices;

namespace TestProject
{
#if NET5_0_OR_GREATER
#pragma warning disable CA2211 // Non-constant fields should not be visible
#endif

    public unsafe class TestClass
    {
        public RefererncedTestProject.Class2? x;
        public void**[,,,][][,]? Ptr;
        public Dictionary<IEnumerable<Dictionary<nint, IntPtr[]>>, nuint?*[]>? XXX;
        public nuint NI;
        public IntPtr P;
        public nint[]? NTA;
        public long L1;
        //public long IXX;
        public void M()
        {
            var loc = new Random().Next(1000).ToString();
#if !NETFRAMEWORK
            x?.C1.ToString();
#endif
            }
        }

    public struct Comp
    {
        public IEnumerable<Comp> Children;
    }

    public struct Declaring
    {
        public struct Nested
        {
            public struct NestedNested
            { }
        }
    }

    public struct EmptyStruct
    { }

    public class EmptyClass
    { }
    public struct ByteStruct
    {
        public byte B;
    }

    public class MultiByte
    {
        public ByteStruct B1;
        public ByteStruct B2;
        public ByteStruct B3;
    }

    public class ByteClass
    {
        public byte B;
    }

    public enum E
    {
        E1,
        E2,
    }

    public struct S0
    {
        public long? L1;
        public DateTime? L2;
        public decimal L3;
    }

    public unsafe struct S1
    {
        public void**[,,,][][,] Ptr;
        public Dictionary<IEnumerable<Dictionary<nint, IntPtr[]>>, UIntPtr?> XXX;
        public nint NI;
        public IntPtr P;
        public nint[] NTA;
        public long L;
        public S2 S2;
        public E E;
    }

    public struct S2
    {
        public int I;
        public object O;
    }

    public class C0
    {
        public byte B;
    }

    public class C1
    {
        public static int St;

        public int I;
        public bool Ba;
        public S1 S1;
        public long L;
        public bool Bb;
    }

    public class D1 : C1
    {
        public double D;
        public C1 C1 = default!;
    }

    public unsafe class G1<T>
    {
        public Declaring.Nested.NestedNested*[] N = null!;
        public int I;
        public long L;
        public T T1 = default!;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct Explicit
    {
        [FieldOffset(0)]
        public int I1;
        [FieldOffset(0)]
        public int I2;
        [FieldOffset(0)]
        public int I3;
        [FieldOffset(0)]
        public int I4;
        [FieldOffset(0)]
        public int I5;
        [FieldOffset(0)]
        public int I6;
        [FieldOffset(8)]
        public int I7;
    }

    public struct NoLayoutStructEmpty
    { }

    public struct NoLayoutStruct
    {
        public int I;
    }

    [StructLayout(LayoutKind.Auto)]
    public struct AutoStruct
    { }

    [StructLayout(LayoutKind.Auto, Size = 0)]
    public struct AutoStructSize0
    { }

    [StructLayout(LayoutKind.Auto, Size = 1)]
    public struct AutoStructSize1
    { }

    [StructLayout(LayoutKind.Sequential, Size = 10)]
    public struct AutoStructSize10
    { }

#if NET5_0_OR_GREATER
#pragma warning restore CA2211 // Non-constant fields should not be visible
#endif
}
