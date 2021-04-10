using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public static class Program
    {
        private static readonly TimeSpan s_shutdownTimerInterval = TimeSpan.FromMinutes(1);

        public static async Task Main(string[] args)
        {
            using Pipe inOut = await Pipe.StartServerAsync(Pipe.InOutName, bidirectional: true).ConfigureAwait(false);
            using BinaryReader pipeReader = new(inOut.Stream, Encoding.UTF8, leaveOpen: true);
            using Pipe log = await Pipe.StartServerAsync(Pipe.LogName, bidirectional: false).ConfigureAwait(false);

            await log.WriteLineAsync($"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})").ConfigureAwait(false);

            bool waitForDebugger = false;
            int? id = null;

            ProcessArgs();
#if DEBUG
            await WaitForDebuggerAsync().ConfigureAwait(false);
#else
            waitForDebugger.ToString();
#endif

            while (true)
            {
                string projectFilePath;
                string typeName;
                using (ShutdownTimer.Start(s_shutdownTimerInterval, log))
                {
                    projectFilePath = pipeReader.ReadString();
                    typeName = pipeReader.ReadString();
                }

                await log.WriteLineAsync($"ANALYZE {projectFilePath} {typeName}").ConfigureAwait(false);

                try
                {
                    await Session
                        .GetOrCreate(inOut.Stream, projectFilePath, log)
                        .AnalyzeAsync(typeName)
                        .ConfigureAwait(false);
                }
                catch (EndOfStreamException)
                {
                    break;
                }
                catch (Exception e)
                {
                    await log.WriteLineAsync(e.ToStringDemystified()).ConfigureAwait(false);
                }
                finally
                {
                    inOut.Stream.WriteByte(0x27);
                }
            }

            Session.DisposeAll();

            void ProcessArgs()
            {
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
            }

#if DEBUG
            async Task WaitForDebuggerAsync()
            {
                if (waitForDebugger)
                {
                    int pid =
#if NET5_0_OR_GREATER
                Environment.ProcessId;
#else
                Process.GetCurrentProcess().Id;
#endif
                    await log.WriteLineAsync($"PID {pid} Waiting for debugger...").ConfigureAwait(false);

                    using (ShutdownTimer.Start(TimeSpan.FromMinutes(1), log))
                        while (!Debugger.IsAttached)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }

                    await log.WriteLineAsync("Debugger attached").ConfigureAwait(false);
                }
            }
#endif
        }
    }
}
