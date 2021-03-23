using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Test
{
    public class Program
    {
        private static readonly Dictionary<Token, ConsoleColor> s_colorMap = new()
        {
            { Token.Namespace, ConsoleColor.Gray },
            { Token.StructRef, ConsoleColor.Green },
            { Token.ClassRef, ConsoleColor.DarkBlue },
            { Token.Keyword, ConsoleColor.Cyan },
            { Token.Identifier, ConsoleColor.DarkYellow },
            { Token.Symbol, ConsoleColor.White },
        };

        public static async Task Main()
        {
            using (HostRunner runner = new(TargetFramework.Net, Platform.x64, debug: false, waitForDebugger: false))
            {
                string projectAssembly = typeof(Program).Assembly.Location;
                string frameworkDirectory = "\\" + runner.TargetFramework switch
                {
                    TargetFramework.NetFramework => "net472",
                    TargetFramework.NetCore => "netcoreapp3.1",
                    TargetFramework.Net => "net5.0",
                    _ => throw new ArgumentException("")
                } + "\\";
                projectAssembly = projectAssembly.Replace("\\net472\\", frameworkDirectory);
                projectAssembly = projectAssembly.Replace("\\netcoreapp3.1\\", frameworkDirectory);
                projectAssembly = projectAssembly.Replace("\\net5.0\\", frameworkDirectory);
                if (runner.TargetFramework != TargetFramework.NetFramework)
                {
                    projectAssembly = Path.ChangeExtension(projectAssembly, ".dll");
                }

                //await AnalyzeAndPrintAsync("").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc,").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc,def").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc, devsko.LayoutAnalyzer.Test").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("devsko.LayoutAnalyzer.Test.TestClass, devsko.LayoutAnalyzer.Test").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("System.IO.Pipelines.Pipe, System.IO.Pipelines").ConfigureAwait(false);

                async Task AnalyzeAndPrintAsync(string typeName)
                {
                    var layout = await runner.SendAsync(projectAssembly + '|' + typeName).ConfigureAwait(false);
                    if (layout is not null)
                    {
                        Print(layout);
                    }
                }
            }
        }

        private static void Print(Layout layout)
        {
            Console.BackgroundColor = ConsoleColor.DarkGray;

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

            Console.Write("             ");
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
                    Console.ForegroundColor = ConsoleColor.White;
                    while (level < fieldOrPadding.Level)
                    {
                        Console.WriteLine($"{indent}             {{");
                        level++;
                        indent = new string(' ', level * 4);
                    }
                    while (level > fieldOrPadding.Level)
                    {
                        level--;
                        indent = new string(' ', level * 4);
                        Console.WriteLine($"{indent}             }}");
                    }

                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.Write($"{fieldOrPadding.Field.Offset:X3} : {fieldOrPadding.Field.Size,-3}    ");

                    if (fieldOrPadding.Field is Field field)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
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
                    Console.WriteLine($"{indent}             }}");
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
}
