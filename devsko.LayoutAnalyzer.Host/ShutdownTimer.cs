using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public static class ShutdownTimer
    {
        public struct ShutdownTimerToken : IDisposable
        {
            public void Dispose()
            {
                ShutdownTimer.Stop();
            }
        }

        private static CancellationTokenSource? _cts;

        public static ShutdownTimerToken Start(TimeSpan interval)
        {
            _cts = new CancellationTokenSource();
            _ = RunAsync();

            return default;

            async Task RunAsync()
            {
                try
                {
                    await Task.Run(async () =>
                    {
                        if (_cts is not null)
                        {
                            await Task.Delay(interval, _cts.Token).ConfigureAwait(false);
                            if (!Debugger.IsAttached)
                            {
                                Console.Error.WriteLine("Shutdown idle host");
                                Environment.Exit(0);
                            }
                        }
                    }).ConfigureAwait(false);
                }
                catch
                { }
            }
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }
    }
}
