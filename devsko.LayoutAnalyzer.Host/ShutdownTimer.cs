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
            _ = Task.Run(
                async () =>
                {
                    while (true)
                    {
                        if (_cts is not null)
                        {
                            await Task.Delay(interval, _cts.Token).ConfigureAwait(false);
                            if (!Debugger.IsAttached)
                            {
                                await Log.WriteLineAsync("Shutdown idle host").ConfigureAwait(false);
                                Environment.Exit(0);
                            }
                        }
                    }
                });

            return default;
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }
    }
}
