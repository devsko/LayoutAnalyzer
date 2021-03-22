using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    static class Program
    {
        private static readonly Dictionary<Token, ConsoleColor> s_colorMap = new()
        {
            { Token.Namespace, ConsoleColor.Gray },
            { Token.StructRef, ConsoleColor.Green },
            { Token.ClassRef, ConsoleColor.Blue },
            { Token.Keyword, ConsoleColor.Cyan },
            { Token.Identifier, ConsoleColor.DarkYellow },
            { Token.Symbol, ConsoleColor.White },
        };

        private static readonly Analyzer s_analyzer = new();

        public static async Task Main()
        {
            Console.WriteLine($"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");
            Console.WriteLine();

            using (Session session = new("C:\\Users\\stefa\\source\\repos\\LayoutAnalyzer\\devsko.LayoutAnalyzer.Test\\bin\\Debug\\net5.0\\devsko.LayoutAnalyzer.Test.dll"))
            {
                session.SendAnalysis("devsko.LayoutAnalyzer.Test.TestClass, devsko.LayoutAnalyzer.Test");
                session.SendAnalysis("System.IO.Pipelines.Pipe, System.IO.Pipelines");
            }


            Console.ReadLine();


                Inspect(await GetLayoutAsync(typeof(string)).ConfigureAwait(false));

            //Inspect(await GetLayoutAsync(typeof(G1<Comp>)).ConfigureAwait(false));
            //Inspect(await GetLayoutAsync(typeof(G1<D1>)).ConfigureAwait(false));
            //Inspect(await GetLayoutAsync(typeof(G1<S1>)).ConfigureAwait(false));
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

        private unsafe static void Inspect(Layout layout)
        {
            Console.WriteLine($"Total size:    {layout.TotalSize} bytes");
            Console.WriteLine($"Total padding: {layout.TotalPadding} bytes");
            if (layout.AttributeKind != (layout.IsValueType ? LayoutKind.Sequential : LayoutKind.Auto) || layout.AttributeSize != 0)
            {
                Console.Write($"Layout:        {layout.AttributeKind}");
                if (layout.AttributeSize != 0)
                {
                    Console.Write($", Size = {layout.AttributeSize}");
                }
                if (layout.AttributePack != 0)
                {
                    Console.Write($", Pack = {layout.AttributePack}");
                }
                Console.WriteLine();
            }
            Console.WriteLine();

            Console.BackgroundColor = ConsoleColor.DarkGray;
            Console.WriteLine();

            Console.Write(layout.IsValueType ? "struct " : "class ");
            Write(layout.Name);
            Console.WriteLine();

            WriteFields(layout.FieldsWithPaddings);

            Console.WriteLine();
            Console.BackgroundColor = ConsoleColor.Black;
            Console.WriteLine();

            static void WriteFields(IEnumerable<(FieldBase Field, int TotalOffset, int Level)> fieldsAndPaddings)
            {
                int level = 0;
                string indent = "";
                foreach (var fieldOrPadding in fieldsAndPaddings)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.Write($"{fieldOrPadding.Field.Offset:X3} : {fieldOrPadding.Field.Size,-3}    ");

                    Console.ForegroundColor = ConsoleColor.White;
                    while (level < fieldOrPadding.Level)
                    {
                        Console.WriteLine($"{indent}{{");
                        level++;
                        indent = new string(' ', level * 4);
                    }
                    while (level > fieldOrPadding.Level)
                    {
                        level--;
                        indent = new string(' ', level * 4);
                        Console.WriteLine($"{indent}}}");
                    }

                    if (fieldOrPadding.Field is Field field)
                    {
                        Console.Write($"{indent}{(field.IsPublic ? "public" : "private")} ");
                        Write(field.TypeName);
                        Console.Write(' ');
                        Console.WriteLine(field.Name);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"{indent}=== PADDING ({fieldOrPadding.Field.Size} bytes) ===");
                    }
                }
                Console.ForegroundColor = ConsoleColor.White;
                while (level > 0)
                {
                    level--;
                    indent = new string(' ', level * 4);
                    Console.WriteLine($"{indent}}}");
                }
            }

            static void Write(TokenizedString value)
            {
                ReadOnlySpan<char> chars = value.Value.AsSpan();
                foreach (TokenSpan span in value.Tokens)
                {
                    Console.ForegroundColor = s_colorMap[span.Token];
                    Console.Write(chars.Slice(0, span.Length).ToString());
                    chars = chars[span.Length..];
                }
                Console.ForegroundColor = ConsoleColor.White;
            }
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
