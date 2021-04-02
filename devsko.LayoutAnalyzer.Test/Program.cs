using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Test
{
    public class Program
    {
        private static readonly Dictionary<Token, ConsoleColor> s_colorMap = new()
        {
            { Token.Identifier, ConsoleColor.Gray },
            { Token.Struct, ConsoleColor.Green },
            { Token.Enum, ConsoleColor.Green },
            { Token.Class, ConsoleColor.DarkBlue },
            { Token.Keyword, ConsoleColor.Cyan },
            { Token.Operator, ConsoleColor.DarkYellow },
            { Token.Punctuation, ConsoleColor.White },
        };

        public static async Task Main()
        {
            try
            {
                HostRunner runner = HostRunner.GetHostRunner(TargetFramework.Net, Platform.x64,
#if DEBUG
                    debug: true, waitForDebugger: false
#else
                    debug: false, waitForDebugger: false
#endif
                    );
                runner.MessageReceived += message =>
                {
                    ConsoleAccessor.WaitAsync()
                        .ContinueWith(task =>
                            {
                                if (task.Status == TaskStatus.RanToCompletion)
                                {
                                    using (task.Result)
                                    {
                                        Console.WriteLine(message);
                                    }
                                }
                            },
                            TaskScheduler.Default);
                };

                // Find the 'TestProject' bins

                string frameworkDirectory = runner.TargetFramework switch
                {
                    TargetFramework.NetFramework => "net472",
                    TargetFramework.NetCore => "netcoreapp3.1",
                    TargetFramework.Net => "net5.0",
                    _ => throw new ArgumentException("")
                };

                string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

                string projectAssemblyPath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(thisAssemblyPath)!,
                    "..", "..", "..", "..", "devsko.LayoutAnalyzer.TestProject", "bin", "Debug", frameworkDirectory, "devsko.LayoutAnalyzer.TestProject.dll"));

                //await AnalyzeAndPrintAsync("").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc,").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc,def").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc, devsko.LayoutAnalyzer.TestProject").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("devsko.LayoutAnalyzer.TestProject.TestClass, devsko.LayoutAnalyzer.TestProject").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("devsko.LayoutAnalyzer.TestProject.S1, devsko.LayoutAnalyzer.TestProject").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("System.IO.Pipelines.Pipe, System.IO.Pipelines").ConfigureAwait(false);

                async Task AnalyzeAndPrintAsync(string typeName)
                {
                    try
                    {
                        Layout? layout = await runner.AnalyzeAsync(projectAssemblyPath + '|' + typeName).ConfigureAwait(false);
                        if (layout is not null)
                        {
                            using (await ConsoleAccessor.WaitAsync().ConfigureAwait(false))
                            {
                                Print(layout);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        using (await ConsoleAccessor.WaitAsync().ConfigureAwait(false))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine(ex.Message);
                        }
                    }
                }
            }
            finally
            {
                HostRunner.DisposeAll();
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

            WriteHeader(0, layout.TotalSize, 0);
            WriteName(layout.Name);
            Console.WriteLine();

            foreach (var fieldOrPadding in layout.FieldsWithPaddings)
            {
                WriteHeader(fieldOrPadding.Field.Offset, fieldOrPadding.Field.Size, fieldOrPadding.Level);

                if (fieldOrPadding.Field is Field field)
                {
                    WriteName(field.TypeName);
                    Console.Write(' ');
                    Console.WriteLine(field.Name);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"=== PADDING ({fieldOrPadding.Field.Size} bytes) ===");
                }
            }

            Console.WriteLine();

            static void WriteHeader(int offset, int size, int level)
            {
                Console.ForegroundColor = ConsoleColor.DarkMagenta;
                Console.Write($"{new string(' ', level * 12)}0x{offset:X3} : {size,-3} ");
            }

            static void WriteName(TokenizedString value)
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
