using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public enum TargetFramework
    {
        NetFramework,
        NetCore,
        Net5,
        Net6,
        NetStandard,
    }

    public enum Platform
    {
        Any,
        x64,
        x86,
    }

    public sealed class HostRunner : IDisposable
    {
        private static readonly ConcurrentDictionary<(TargetFramework, Platform), HostRunner> s_cache = new();
        private static int s_currentId;

        public static HostRunner GetHostRunner(string hostBasePath, TargetFramework framework, Platform platform, bool debug = false, bool waitForDebugger = false)
        {
            return s_cache.GetOrAdd((framework, platform), _ => new HostRunner(hostBasePath, framework, platform, debug, waitForDebugger));
        }

        public static void DisposeAll()
        {
            foreach (HostRunner runner in s_cache.Values)
            {
                runner.Dispose();
            }
        }

        public event Action<string>? MessageReceived;

        public TargetFramework TargetFramework { get; private init; }
        public Platform Platform { get; private init; }
        public bool IsDebug { get; private init; }
        public int Id { get; private init; }
        public Guid HostId { get; private init; }

        private JsonSerializerOptions _jsonOptions;
        private SemaphoreSlim _semaphore;
        private ProcessStartInfo _startInfo;
        private Process? _process;
        private Pipe? _logPipe;
        private BinaryWriter? _pipeWriter;
        private EofDetectingStream? _responseStream;

        private HostRunner(string hostBasePath, TargetFramework framework, Platform platform, bool debug, bool waitForDebugger)
        {
            debug |= waitForDebugger;

            TargetFramework = framework;
            Platform = platform;
            IsDebug = debug;
            Id = Interlocked.Increment(ref s_currentId);
            HostId = Guid.NewGuid();

            _jsonOptions = new JsonSerializerOptions();
            _semaphore = new SemaphoreSlim(1);

            (string frameworkDirectory, string fileExtension) = framework switch
            {
                TargetFramework.NetFramework => ("net472", "exe"),
                TargetFramework.NetCore => ("netcoreapp3.1", "dll"),
                TargetFramework.Net5 => ("net5.0", "exe"),
                _ => throw new ArgumentException("", nameof(framework))
            };
            (string platformDirectory, string programFilesDirectory) = platform switch
            {
                Platform.x64 => ("x64", "ProgramW6432"),
                Platform.x86 => ("x86", "ProgramFiles(x86)"),
                _ => throw new ArgumentException("", nameof(platform))
            };

            string hostAssemblyPath = Path.GetFullPath(Path.Combine(
                hostBasePath,
                platformDirectory,
                debug ? "Debug" : "Release",
                frameworkDirectory,
                Path.ChangeExtension("devsko.LayoutAnalyzer.Host.", fileExtension)));

            string exePath;
            string arguments;
            if (Path.GetExtension(hostAssemblyPath) == ".exe")
            {
                exePath = hostAssemblyPath;
                arguments = "";
            }
            else
            {
                exePath = Path.Combine(Environment.GetEnvironmentVariable(programFilesDirectory)!, "dotnet", "dotnet.exe");
                if (!File.Exists(exePath))
                {
                    throw new InvalidOperationException($"Install .NET ('{exePath}' not found.)");
                }
                arguments = hostAssemblyPath;
            }

            arguments += $" -id:{HostId}";

#if DEBUG
            if (waitForDebugger)
            {
                arguments += " -wait";
            }
#endif

            _startInfo = new()
            {
                FileName = exePath,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(hostAssemblyPath)!,
            };
        }

        public async Task<Layout?> AnalyzeAsync(string projectFilePath, bool debug, Platform platform, string targetFramework,bool exe, string typeName, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await EnsureRunningProcessAsync().ConfigureAwait(false);
                long start = Stopwatch.GetTimestamp();

                _pipeWriter!.Write(projectFilePath);
                _pipeWriter.Write(debug);
                _pipeWriter.Write((byte)platform);
                _pipeWriter.Write(targetFramework);
                _pipeWriter.Write(exe);
                _pipeWriter.Write(typeName);
                _pipeWriter.Flush();

                try
                {
                    Layout? layout = await JsonSerializer.DeserializeAsync<Layout>(_responseStream!, _jsonOptions, cancellationToken).ConfigureAwait(false);
                    if (layout is not null)
                    {
                        layout.ElapsedTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp() - start);
                    }

                    return layout;
                }
                catch (JsonException ex) when (ex.LineNumber == 0 && ex.BytePositionInLine == 0)
                {
                    return null;
                }
            }
            finally
            {
                _responseStream?.Reset();
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _pipeWriter?.Dispose();
            _responseStream?.Dispose();
            _logPipe?.Dispose();

            if (_process?.HasExited == false)
            {
                try
                {
                    _process?.Kill();
                }
                catch (InvalidOperationException)
                { }
            }
            _process?.Dispose();
        }

        [MemberNotNull(nameof(_process))]
        private async Task EnsureRunningProcessAsync()
        {
            if (_process is null || _process.HasExited)
            {
                Dispose();
                await StartProcessAsync().ConfigureAwait(false);
            }
        }

        [MemberNotNull(nameof(_process))]
        private async Task StartProcessAsync()
        {
            Process? process = Process.Start(_startInfo);
            if (process is null)
            {
                throw new InvalidOperationException("Could not start new host");
            }

            _process = process;

            Pipe hotReloadPipe = await Pipe.StartServerAsync(Pipe.HotReloadName, HostId, true).ConfigureAwait(false);

            Pipe inOutPipe = await Pipe.ConnectAsync(Pipe.InOutName, HostId, true).ConfigureAwait(false);
            _responseStream = new EofDetectingStream(inOutPipe.Stream);
            _pipeWriter = new BinaryWriter(inOutPipe.Stream);

            _logPipe = await Pipe.ConnectAsync(Pipe.LogName, HostId, false).ConfigureAwait(false);

            _ = ReadLogAsync();

            Console.WriteLine($"Host process started PID={process.Id}");

            async Task ReadLogAsync()
            {
                while (true)
                {
                    string? line = await _logPipe.ReadLineAsync().ConfigureAwait(false);
                    if (line is null)
                    {
                        break;
                    }
                    MessageReceived?.Invoke(line);
                }
            }
        }
    }
}
