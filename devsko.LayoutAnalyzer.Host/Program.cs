using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public static class Program
    {
        private static readonly TimeSpan s_shutdownTimerInterval = TimeSpan.FromMinutes(10);

        public static async Task Main(string[] args)
        {
            bool waitForDebugger = false;
            Guid id = default;
            bool hotReloadEnabled = false;

            ProcessArgs();

            using Pipe inOut = await Pipe.StartServerAsync(Pipe.InOutName, id, bidirectional: true, cancellationToken: default).ConfigureAwait(false);
            using BinaryReader pipeReader = new(inOut.Stream, Encoding.UTF8, leaveOpen: true);

            using (await Log.InitializeAsync(id).ConfigureAwait(false))
            {
                await Log.WriteLineAsync($"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})").ConfigureAwait(false);

#if DEBUG
                await WaitForDebuggerAsync().ConfigureAwait(false);
#else
                waitForDebugger.ToString();
#endif

                CancellationTokenSource cancelHotReloadLoop = new();
                if (hotReloadEnabled)
                {
#if NET6_0_OR_GREATER
                    _ = HotReloadLoopAsync(cancelHotReloadLoop.Token);
#endif
                }

                while (true)
                {
                    try
                    {
                        ProjectData data;
                        string typeName;
                        using (ShutdownTimer.Start(s_shutdownTimerInterval))
                        {
                            data = new ProjectData
                            (
                                ProjectFilePath: pipeReader.ReadString(),
                                Debug: pipeReader.ReadBoolean(),
                                Platform: (Platform)pipeReader.ReadByte(),
                                TargetFramework: pipeReader.ReadString(),
                                Exe: pipeReader.ReadBoolean()
                            );
                            typeName = pipeReader.ReadString();
                        }

                        await Log.WriteLineAsync($"ANALYZE {data.ProjectFilePath} {typeName}").ConfigureAwait(false);

                        try
                        {
                            ProjectLoader project = await ProjectLoader.GetOrCreateAsync(inOut.Stream, data).ConfigureAwait(false);
                            await project.AnalyzeAsync(typeName).ConfigureAwait(false);
                        }
                        finally
                        {
                            inOut.Stream.WriteByte(0x27);
                        }
                    }
                    catch (EndOfStreamException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            await Log.WriteLineAsync(ex.ToStringDemystified()).ConfigureAwait(false);
                        }
                        catch { }
                    }
                }

                cancelHotReloadLoop.Cancel();
                await ProjectLoader.DisposeAllAsync().ConfigureAwait(false);
            }

            void ProcessArgs()
            {
                foreach (string arg in args)
                {
                    if (arg.Equals("-wait", StringComparison.OrdinalIgnoreCase))
                    {
                        waitForDebugger = true;
                    }
                    else if (arg.Equals("-hotreload", StringComparison.OrdinalIgnoreCase))
                    {
                        hotReloadEnabled = true;
                    }
                    else if (arg.StartsWith("-id:", StringComparison.OrdinalIgnoreCase))
                    {
                        if (Guid.TryParse(arg.Substring(4).Trim(), out Guid guid))
                        {
                            id = guid;
                        }
                    }
                }
                if (id == default)
                {
                    throw new InvalidOperationException("Parameter -id not found");
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
                    await Log.WriteLineAsync($"PID {pid} Waiting for debugger...").ConfigureAwait(false);

                    using (ShutdownTimer.Start(TimeSpan.FromMinutes(1)))
                        while (!Debugger.IsAttached)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                        }

                    await Log.WriteLineAsync("Debugger attached").ConfigureAwait(false);
                }
            }
#endif

#if NET6_0_OR_GREATER
            async Task HotReloadLoopAsync(CancellationToken cancellationToken)
            {
                try
                {
                    using Pipe pipe = await Pipe.ConnectAsync(Pipe.HotReloadName, id, bidirectional: true, cancellationToken).ConfigureAwait(false);
                    using BinaryReader reader = new(pipe.Stream, Encoding.UTF8, leaveOpen: true);

                    await Log.WriteLineAsync("HOT RELOAD Pipe connected").ConfigureAwait(false);

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            string projectFilePath = reader.ReadString();
                            string path = reader.ReadString();
                            int count = reader.ReadInt32();
                            var updates = new (Guid ModuleId, byte[] MetadataDelta, byte[] ILDelta)[count];
                            for (int i = 0; i < updates.Length; i++)
                            {
                                (Guid ModuleId, byte[] MetadataDelta, byte[] ILDelta) update = new()
                                {
                                    ModuleId = Guid.Parse(reader.ReadString()),
                                    MetadataDelta = await ReadArrayAsync(reader, cancellationToken).ConfigureAwait(false),
                                    ILDelta = await ReadArrayAsync(reader, cancellationToken).ConfigureAwait(false),
                                };
                                updates[i] = update;
                            }
                            bool success = true;
                            ProjectLoader? projectLoader = ProjectLoader.Get(projectFilePath);
                            if (projectLoader is null)
                            {
                                success = false;
                            }
                            else
                            {
                                foreach (var update in updates)
                                {
                                    Assembly? assembly = AppDomain.CurrentDomain
                                        .GetAssemblies()
                                        .FirstOrDefault(a =>
                                            a.Modules.FirstOrDefault() is Module m && m.ModuleVersionId == update.ModuleId);
                                    if (assembly is null)
                                    {
                                        success = false;
                                    }
                                    else
                                    {
                                        System.Reflection.Metadata.AssemblyExtensions.ApplyUpdate(assembly, update.MetadataDelta, update.ILDelta, ReadOnlySpan<byte>.Empty);
                                    }
                                }
                            }
                            pipe.Stream.WriteByte(success ? (byte)0 : (byte)0xff);
                        }
                        catch (OperationCanceledException)
                        {
                            await Log.WriteLineAsync("HOT RELOAD Leaving apply loop").ConfigureAwait(false);
                            break;
                        }
                        catch (EndOfStreamException)
                        {
                            await Log.WriteLineAsync("HOT RELOAD Pipe connection lost").ConfigureAwait(false);
                            break;
                        }
                        catch (Exception ex)
                        {
                            await Log.WriteLineAsync("HOT RELOAD applying delta failed: " + ex.ToStringDemystified()).ConfigureAwait(false);
                        }
                    }

                    static async ValueTask<byte[]> ReadArrayAsync(BinaryReader reader, CancellationToken cancellationToken)
                    {
                        int count = reader.ReadInt32();
                        var bytes = new byte[count];

                        var read = 0;
                        while (read < count)
                        {
                            read += await reader.BaseStream.ReadAsync(bytes.AsMemory(read), cancellationToken).ConfigureAwait(false);
                        }

                        return bytes;
                    }
                }
                catch (Exception ex1)
                {
                    await Log.WriteLineAsync("HOT RELOAD apply loop failed: " + ex1.ToStringDemystified()).ConfigureAwait(false);
                }
            }
#endif
        }
    }
}
