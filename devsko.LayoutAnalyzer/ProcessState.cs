using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public class ProcessState : IDisposable
    {
        private readonly Process _process;

        public ProcessState(string fileName, string arguments, string workingDirectory)
        {
            _process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                }
            };
        }

        public async Task<(int ExitCode, string Output)> StartAndWaitForExitAsync(CancellationToken cancellationToken)
        {
            using (cancellationToken.Register(() => TryKill()))
            {
                TaskCompletionSource<object?> tcs = new();
                StringBuilder outputBuilder = new();

                _process.Exited += OnExited;
                _process.OutputDataReceived += OnOutput;
                _process.ErrorDataReceived += OnOutput;

                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();

                await tcs.Task.ConfigureAwait(false);

                try
                {
                    // We need to use two WaitForExit calls to ensure that all of the output/events are processed. Previously
                    // this code used Process.Exited, which could result in us missing some output due to the ordering of
                    // events.
                    //
                    // See the remarks here: https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit#System_Diagnostics_Process_WaitForExit_System_Int32_
                    if (!_process.WaitForExit(Int32.MaxValue))
                    {
                        throw new TimeoutException();
                    }

                    _process.WaitForExit();
                }
                catch (InvalidOperationException)
                { }

                return (_process.ExitCode, outputBuilder.ToString());

                void OnExited(object? sender, EventArgs args)
                    => tcs.TrySetResult(null);

                void OnOutput(object sender, DataReceivedEventArgs args)
                    => outputBuilder.AppendLine(args.Data);
            }
        }

        public void TryKill()
        {
            try
            {
                if (_process is not null && !_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch (Exception)
            { }
        }


        public void Dispose()
        {
            TryKill();
            _process?.Dispose();
        }
    }
}
