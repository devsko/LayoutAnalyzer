using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using devsko.LayoutAnalyzer.Runner;
using Microsoft.Build.Locator;

namespace devsko.LayoutAnalyzer.Test
{
    public class Program
    {
        private static readonly Dictionary<Token, ConsoleColor> s_colorMap = new()
        {
            { Token.Identifier, ConsoleColor.Gray },
            { Token.Struct, ConsoleColor.Green },
            { Token.Enum, ConsoleColor.DarkGreen },
            { Token.Class, ConsoleColor.DarkBlue },
            { Token.Delegate, ConsoleColor.DarkCyan },
            { Token.Interface, ConsoleColor.Black },
            { Token.Keyword, ConsoleColor.Cyan },
            { Token.Operator, ConsoleColor.DarkYellow },
            { Token.Punctuation, ConsoleColor.White },
        };

        public static async Task Main()
        {
            MSBuildLocator.RegisterDefaults();

            string csprojPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                "..", "..", "..", "..", "TestProject", "TestProject.csproj"));

            try
            {
                string hostBasePath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                    "..", "..", "..", "..", "devsko.LayoutAnalyzer.Host", "bin"));

                HostRunner runner = HostRunner.GetHostRunner(hostBasePath, TargetFramework.Net5, Platform.x64,
#if DEBUG
                    debug: true, waitForDebugger: false
#else
                    debug: false, waitForDebugger: false
#endif
                    );
                runner.MessageReceived += message =>
                    Task.Run(
                        async () =>
                        {
                            using (await ConsoleAccessor.WaitAsync().ConfigureAwait(false))
                            {
                                Console.WriteLine(message);
                            }
                        });

                // Find TestProject.csproj

                string projectFilePath = Path.GetFullPath(Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,
                    "..", "..", "..", "..",
                    "TestProject",
                    "TestProject.csproj"));

                //await AnalyzeAndPrintAsync("").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc,").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc,def").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("abc, TestProject").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("TestProject.TestClass, TestProject").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("TestProject.S1, TestProject").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("System.IO.Pipelines.Pipe, System.IO.Pipelines").ConfigureAwait(false);

                //await AnalyzeAndPrintAsync("TestProject.NoLayoutStruct, TestProject").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("TestProject.NoLayoutStructEmpty, TestProject").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("TestProject.AutoStruct, TestProject").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("TestProject.AutoStructSize0, TestProject").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("TestProject.AutoStructSize1, TestProject").ConfigureAwait(false);
                //await AnalyzeAndPrintAsync("TestProject.AutoStructSize10, TestProject").ConfigureAwait(false);

                //await AnalyzeAndPrintAsync("TestProject.Explicit, TestProject").ConfigureAwait(false);

                Console.WriteLine("waiting...");
                Console.ReadLine();

                async Task AnalyzeAndPrintAsync(string typeName)
                {
                    try
                    {
                        Layout? layout = await runner.AnalyzeAsync(projectFilePath, debug: true, Platform.Any, "net5.0", exe: false, typeName).ConfigureAwait(false);
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

            Console.WriteLine($"Elapsed time:  {layout.ElapsedTime}");
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

            foreach (var fieldOrPadding in layout.AllFieldsAndPaddings)
            {
                WriteHeader(fieldOrPadding.Field.Offset, fieldOrPadding.Field.Size, fieldOrPadding.Level);

                if (fieldOrPadding.Field is Field field)
                {
                    WriteName(field.TypeAndName);
                    Console.WriteLine();
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
