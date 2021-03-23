using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Test
{
    public enum TargetFramework
    {
        NetFramework,
        NetCore,
        Net,
    }

    public enum Platform
    {
        x64,
        x86,
    }

    public sealed class HostRunner : IDisposable
    {
        private Process _process;
        private EofDetectingStream _outStream;
        private JsonSerializerOptions _jsonOptions;
        private SemaphoreSlim _semaphore;

        public HostRunner(TargetFramework framework, Platform platform, bool waitForDebugger)
        {
            _jsonOptions = new JsonSerializerOptions();
            _semaphore = new SemaphoreSlim(1);

            (string frameworkDirectory, string fileExtension) = framework switch
            {
                TargetFramework.NetFramework => ("net472", "exe"),
                TargetFramework.NetCore => ("netcoreapp3.1", "dll"),
                TargetFramework.Net => ("net5.0", "exe"),
                _ => throw new ArgumentException("", nameof(framework))
            };
            (string platformDirectory, string programFilesDirectory) = platform switch
            {
                Platform.x64 => ("x64", "%ProgramW6432%"),
                Platform.x86 => ("x86", "%ProgramFiles(x86)%"),
                _ => throw new ArgumentException("", nameof(platform))
            };

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
#if DEBUG
            string hostAssemblyPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(thisAssemblyPath)!,
                "..", "..", "..", "..", "devsko.LayoutAnalyzer.Host", "bin", platformDirectory, "Debug", frameworkDirectory, Path.ChangeExtension("devsko.LayoutAnalyzer.Host.", fileExtension)));
#else
            throw new NotImplementedException();
#endif
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
#if DEBUG
            if (waitForDebugger)
            {
                arguments += " -wait";
            }
#endif

            ProcessStartInfo start = new()
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

            Process? process = Process.Start(start);
            if (process is null)
            {
                throw new InvalidOperationException("could not start new host");
            }

            _process = process;
            _outStream = new EofDetectingStream(_process.StandardOutput.BaseStream);
            _process.ErrorDataReceived += (sender, e) => Console.WriteLine(e.Data);
            _process.BeginErrorReadLine();
        }

        public async Task<Layout?> SendAsync(string command, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _process.StandardInput.WriteLine(command);

                return await JsonSerializer.DeserializeAsync<Layout?>(_outStream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            finally
            {
                _outStream.Reset();
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _outStream.Dispose();
            _process.Kill();
            _process.Dispose();
        }
    }
}
