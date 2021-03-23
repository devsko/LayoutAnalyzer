using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public static class ShutdownTimer
    {
        private static CancellationTokenSource? _cts;

        public static void Start(TimeSpan interval)
        {
            _cts = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await Task.Delay(interval, _cts.Token).ConfigureAwait(false);
                Process.GetCurrentProcess().Kill();
            });
        }

        public static void Stop()
        {
            _cts?.Cancel();
            _cts = null;
        }
    }
}
