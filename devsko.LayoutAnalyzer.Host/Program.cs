using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

            bool waitForDebugger = false;
            int? id = null;
            foreach (string arg in args)
            {
                if (arg.Equals("-wait", StringComparison.OrdinalIgnoreCase))
                {
                    waitForDebugger = true;
                }
                else if (arg.StartsWith("-id:", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(arg.Substring(4).Trim(), out int i))
                    {
                        id = i;
                    }
                }
            }
            if (waitForDebugger)
            {
#if DEBUG
                int pid =
#if NET5_0_OR_GREATER
                Environment.ProcessId;
#else
                Process.GetCurrentProcess().Id;
#endif
                Console.Error.WriteLine($"PID {pid} Waiting for debugger...");

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
                    if (line is null)
                    {
                        break;
                    }
                    command = line;
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
