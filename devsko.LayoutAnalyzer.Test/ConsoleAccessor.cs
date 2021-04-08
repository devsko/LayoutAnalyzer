using System;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Test
{
    public static class ConsoleAccessor
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        public static async Task<Access> WaitAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            return new Access(Console.ForegroundColor, Console.BackgroundColor);
        }

        public struct Access : IDisposable
        {
            private readonly ConsoleColor _foreground;
            private readonly ConsoleColor _background;

            public Access(ConsoleColor foreground, ConsoleColor background)
            {
                _foreground = foreground;
                _background = background;
            }

            public void Dispose()
            {
                Console.ForegroundColor = _foreground;
                Console.BackgroundColor = _background;
                _semaphore.Release();
            }
        }
    }
}
