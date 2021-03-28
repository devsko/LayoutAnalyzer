﻿using System;
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
            { Token.Namespace, ConsoleColor.Gray },
            { Token.StructRef, ConsoleColor.Green },
            { Token.ClassRef, ConsoleColor.DarkBlue },
            { Token.Keyword, ConsoleColor.Cyan },
            { Token.Identifier, ConsoleColor.DarkYellow },
            { Token.Symbol, ConsoleColor.White },
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

                await AnalyzeAndPrintAsync("").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("abc").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("abc,").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("abc,def").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("abc, devsko.LayoutAnalyzer.TestProject").ConfigureAwait(false);
                await AnalyzeAndPrintAsync("devsko.LayoutAnalyzer.TestProject.TestClass, devsko.LayoutAnalyzer.TestProject").ConfigureAwait(false);

                Console.ReadLine();

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
                Console.ReadLine();
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

            Console.Write("             ");
            Console.Write(layout.IsValueType ? "struct " : "class ");
            Write(layout.Name);
            Console.WriteLine();

            WriteFields(layout.FieldsWithPaddings);

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
