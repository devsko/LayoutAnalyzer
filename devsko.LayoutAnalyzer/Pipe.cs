using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public class Pipe : IDisposable
    {
        public const string InOutName = "layoutanalyzer-inout";
        public const string LogName = "layoutanalyzer-log";
        public const string HotReloadName = "layoutanalyzer-hotreload";

        private readonly PipeStream _stream;
        private TextWriter? _writer;
        private TextReader? _reader;

        private Pipe(PipeStream stream)
        {
            _stream = stream;
        }

        public static async Task<Pipe> StartServerAsync(string name, Guid id, bool bidirectional)
        {
            NamedPipeServerStream stream = new(GetName(name, id), bidirectional ? PipeDirection.InOut : PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous
#if NETCOREAPP3_1_OR_GREATER
                | PipeOptions.CurrentUserOnly
#endif
                );
            var pipe = new Pipe(stream);
            await stream.WaitForConnectionAsync().ConfigureAwait(false);

            return pipe;
        }

        public static async Task<Pipe> ConnectAsync(string name, Guid id, bool bidirectional, TimeSpan? timeout = null)
        {
            NamedPipeClientStream stream = new(".", GetName(name, id), bidirectional ? PipeDirection.InOut : PipeDirection.In, PipeOptions.Asynchronous
#if NETCOREAPP3_1_OR_GREATER
                | PipeOptions.CurrentUserOnly
#endif
                );
            var pipe = new Pipe(stream);
            await stream.ConnectAsync((int?)timeout?.TotalMilliseconds ?? -1).ConfigureAwait(false);

            return pipe;
        }

        private static string GetName(string name, Guid id)
            => $"{name}-{id}";

        public PipeStream Stream
            => _stream;

        private TextWriter Writer
            => _writer ??= new StreamWriter(_stream, Encoding.UTF8, 1024, leaveOpen: true);

        private TextReader Reader
            => _reader ??= new StreamReader(_stream, Encoding.UTF8, detectEncodingFromByteOrderMarks:false, -1, leaveOpen: true);

        public async Task WriteLineAsync(string line)
        {
            await Writer.WriteLineAsync(line).ConfigureAwait(false);
            await Writer.FlushAsync().ConfigureAwait(false);
        }

        public Task<string?> ReadLineAsync()
            => Reader.ReadLineAsync();

        public void Dispose()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _stream.Dispose();
        }
    }
}
