using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public class Pipe : IDisposable
    {
        public struct PipeAccessor : IDisposable
        {
            private Pipe _pipe;

            public static async Task<PipeAccessor> WaitAsync(Pipe pipe)
            {
                await pipe._semaphore.WaitAsync().ConfigureAwait(false);

                return new PipeAccessor { _pipe = pipe };
            }

            public PipeStream Stream
                => _pipe.Stream;

            public void Dispose()
            {
                _pipe._semaphore.Release();
                this = default;
            }
        }

        public const string InOutName = "layoutanalyzer-inout";
        public const string LogName = "layoutanalyzer-log";

        private readonly PipeStream _stream;
        private TextWriter? _writer;
        private TextReader? _reader;
        private readonly SemaphoreSlim _semaphore = new(1);

        private Pipe(string name, bool isServer, bool bidirectional)
        {
            if (isServer)
            {
                _stream = new NamedPipeServerStream(name, bidirectional ?  PipeDirection.InOut : PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous
#if NETCOREAPP3_1_OR_GREATER
                | PipeOptions.CurrentUserOnly
#endif
                );
            }
            else
            {
                _stream = new NamedPipeClientStream(".", name, bidirectional ? PipeDirection.InOut : PipeDirection.In, PipeOptions.Asynchronous
#if NETCOREAPP3_1_OR_GREATER
                | PipeOptions.CurrentUserOnly
#endif
                );
            }
        }

        public static async Task<Pipe> StartServerAsync(string name, bool bidirectional)
        {
            var pipe = new Pipe(name, true, bidirectional);
            await ((NamedPipeServerStream)pipe._stream).WaitForConnectionAsync().ConfigureAwait(false);

            return pipe;
        }

        public static async Task<Pipe> ConnectAsync(string name, bool bidirectional)
        {
            var pipe = new Pipe(name, false, bidirectional);
            await ((NamedPipeClientStream)pipe._stream).ConnectAsync().ConfigureAwait(false);

            return pipe;
        }

        public PipeStream Stream
            => _stream;

        private TextWriter Writer
            => _writer ??= new StreamWriter(_stream, Encoding.UTF8);

        private TextReader Reader
            => _reader ??= new StreamReader(_stream, Encoding.UTF8);

        public async Task WriteLineAsync(string line)
        {
            await Writer.WriteLineAsync(line).ConfigureAwait(false);
            await Writer.FlushAsync().ConfigureAwait(false);
        }

        public Task<string?> ReadLineAsync()
            => Reader.ReadLineAsync();

        public Task<PipeAccessor> GetAsync()
            => PipeAccessor.WaitAsync(this);

        public void Dispose()
            => _stream.Dispose();
    }
}
