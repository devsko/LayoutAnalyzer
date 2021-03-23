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
        public static async Task Main(string[] args)
        {
            Console.Error.WriteLine($"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");

            if (args.Length > 0 && args[0].ToLowerInvariant() == "-wait")
            {
#if DEBUG
                Console.Error.WriteLine("Waiting for debugger...");
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
                Console.Error.WriteLine("Debugger attached");
#endif
            }

            while (true)
            {
                string command = Console.ReadLine() ?? "";

                using (var consoleAccess = await ConsoleAccessor.WaitAsync().ConfigureAwait(false))
                {
                    Console.Error.WriteLine(command);

                    try
                    {
                        int index = command.IndexOf('|');
                        if (index < 0 || index >= command.Length - 1)
                        {
                            throw new InvalidOperationException($"Wrong command format '{command}'");
                        }
                        string projectAssembly = command.Substring(0, index);
                        string typeName = command.Substring(index + 1);
                        using (Session session = new(consoleAccess.StdOut, projectAssembly))
                        {
                            await session.AnalyzeAsync(typeName).ConfigureAwait(false);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("Error: " + e.Message);
                    }
                }
            }
        }
    }
}
