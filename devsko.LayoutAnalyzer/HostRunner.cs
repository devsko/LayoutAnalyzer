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
        NetCore3,
        NetCore2,
        Net,
    }

    public enum Platform
    {
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

        private JsonSerializerOptions _jsonOptions;
        private SemaphoreSlim _semaphore;
        private ProcessStartInfo _startInfo;
        private Process? _process;
        private EofDetectingStream? _outStream;

        private HostRunner(string hostBasePath, TargetFramework framework, Platform platform, bool debug, bool waitForDebugger)
        {
            debug |= waitForDebugger;

            TargetFramework = framework;
            Platform = platform;
            IsDebug = debug;
            Id = Interlocked.Increment(ref s_currentId);

            _jsonOptions = new JsonSerializerOptions();
            _semaphore = new SemaphoreSlim(1);

            (string frameworkDirectory, string fileExtension) = framework switch
            {
                TargetFramework.NetFramework => ("net472", "exe"),
                TargetFramework.NetCore2 => ("netcoreapp2.1", "dll"),
                TargetFramework.NetCore3 => ("netcoreapp3.1", "dll"),
                TargetFramework.Net => ("net5.0", "exe"),
                _ => throw new ArgumentException("", nameof(framework))
            };
            (string platformDirectory, string programFilesDirectory) = platform switch
            {
                Platform.x64 => ("x64", "%ProgramW6432%"),
                Platform.x86 => ("x86", "%ProgramFiles(x86)%"),
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
                exePath = Path.Combine(Environment.ExpandEnvironmentVariables(programFilesDirectory), "dotnet", "dotnet.exe");
                if (!File.Exists(exePath))
                {
                    throw new InvalidOperationException($"Install .NET ('{exePath}' not found.)");
                }
                arguments = hostAssemblyPath;
            }
            arguments += $" -id:{Id}";
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
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(hostAssemblyPath)!,
            };
        }

        public async Task<Layout?> AnalyzeAsync(string command, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                EnsureRunningProcess();
                Stopwatch watch = Stopwatch.StartNew();
                _process.StandardInput.WriteLine(command);

                try
                {
                    Layout? layout = await JsonSerializer.DeserializeAsync<Layout>(_outStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
                    if (layout is not null)
                    {
                        layout.ElapsedTime = watch.Elapsed;
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
                _outStream?.Reset();
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
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
            _outStream?.Dispose();
        }

        [MemberNotNull(nameof(_process), nameof(_outStream))]
        private void EnsureRunningProcess()
        {
            if (_process is null || _process.HasExited || _outStream is null)
            {
                Dispose();
                StartProcess();
            }
        }

        [MemberNotNull(nameof(_process), nameof(_outStream))]
        private void StartProcess()
        {
            Process? process = Process.Start(_startInfo);
            if (process is null)
            {
                throw new InvalidOperationException("Could not start new host");
            }

            _process = process;
            _outStream = new EofDetectingStream(_process.StandardOutput.BaseStream);
            _process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is not null)
                {
                    MessageReceived?.Invoke(e.Data);
                }
            };
            _process.BeginErrorReadLine();

            Console.WriteLine($"Host process started PID={process.Id}");
        }
    }
}
