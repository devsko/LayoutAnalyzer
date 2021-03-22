using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    static class Program
    {
        private static readonly Analyzer s_analyzer = new();

        public static async Task Main(string[] args)
        {
            if (args.Length > 0 && args[0].ToLowerInvariant() == "-wait")
            {
                while (true)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(10));
                }
            }

            Console.Error.WriteLine($"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");

            Stream consoleOut = Console.OpenStandardOutput();
            while (true)
            {
                string? command = Console.ReadLine();

                Console.Error.WriteLine(command);

                if (string.IsNullOrEmpty(command))
                {
                    break;
                }

                int index = command.IndexOf('|');
                if (index < 0 || index >= command.Length - 1)
                {
                    throw new Exception();
                }
                string projectAssembly = command.Substring(0, index);
                string typeName = command.Substring(index + 1);
                using (Session session = new(consoleOut, projectAssembly))
                {
                    await session.SendAnalysisAsync(typeName).ConfigureAwait(false);
                }
            }
        }

        private static async Task<Layout> GetLayoutAsync(Type type)
        {
            Layout? layout = s_analyzer.Analyze(type);

            if (layout is null)
            {
                throw new ArgumentException($"Type {type.Name} has no layout.");
            }

            using (Stream stream = new MemoryStream())
            {
                await SerializeAsync().ConfigureAwait(false);
                stream.Position = 0;
                await DeserializeAsync().ConfigureAwait(false);

                async ValueTask SerializeAsync()
                {
                    await JsonSerializer.SerializeAsync(stream, layout).ConfigureAwait(false);
                    await stream.FlushAsync().ConfigureAwait(false);
                }
                async ValueTask DeserializeAsync()
                {
                    layout = await JsonSerializer.DeserializeAsync<Layout>(stream).ConfigureAwait(false);
                }
            }

            return layout ?? throw new InvalidOperationException();
        }

    }

#if NET5_0_OR_GREATER
#pragma warning disable CA2211 // Non-constant fields should not be visible
#endif

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

#if NET5_0_OR_GREATER
#pragma warning restore CA2211 // Non-constant fields should not be visible
#endif
}
