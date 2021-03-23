﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public static class ConsoleAccessor
    {
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private static readonly Stream _stdOut = Console.OpenStandardOutput();

        public static async Task<Access> WaitAsync()
        {
            await _semaphore.WaitAsync().ConfigureAwait(false);
            return new Access();
        }

        public struct Access : IDisposable
        {
            public Stream StdOut => _stdOut;

            public void Dispose()
            {
                _stdOut.WriteByte(0x27);
                _semaphore.Release();
            }
        }
    }
}
