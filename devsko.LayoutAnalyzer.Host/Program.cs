﻿using System;
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
        private static readonly TimeSpan s_shutdownTimerInterval = TimeSpan.FromMinutes(1);

        public static async Task Main(string[] args)
        {
            Console.Error.WriteLine($"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");

            if (args.Length > 0 && args[0].ToLowerInvariant() == "-wait")
            {
#if DEBUG
                Console.Error.WriteLine("Waiting for debugger...");

                using (ShutdownTimer.Start(TimeSpan.FromMinutes(1)))
                    while (!Debugger.IsAttached)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(1));
                    }

                Console.Error.WriteLine("Debugger attached");
#endif
            }

            while (true)
            {
                string command;
                using (ShutdownTimer.Start(s_shutdownTimerInterval))
                {
                    string? line = Console.ReadLine();
                    command = line ?? "";
                }

                if (command == "")
                {
                    break;
                }

                using (var consoleAccess = await ConsoleOutAccessor.WaitAsync().ConfigureAwait(false))
                {
                    Console.Error.WriteLine("ANALYZE " + command);

                    try
                    {
                        int index = command.IndexOf('|');
                        if (index < 0 || index >= command.Length - 1)
                        {
                            throw new InvalidOperationException($"Wrong command format '{command}'");
                        }
                        string assemblyName = command.Substring(0, index);
                        string typeName = command.Substring(index + 1);
                        await Session
                            .GetOrCreate(consoleAccess.StdOut, assemblyName)
                            .AnalyzeAsync(typeName)
                            .ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine(e.ToStringDemystified());
                    }
                }
            }
            Session.DisposeAll();
        }
    }
}
