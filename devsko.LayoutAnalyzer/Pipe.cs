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

#if DEBUG
        private const int Timeout = 60_000;
#else
        private const int Timeout = 5_000;
#endif

        private readonly PipeStream _stream;

        private Pipe(PipeStream stream)
        {
            _stream = stream;
        }

        public static async Task<Pipe> StartServerAsync(string name, Guid id, bool bidirectional, CancellationToken cancellationToken)
        {
            NamedPipeServerStream stream = new(GetName(name, id), bidirectional ? PipeDirection.InOut : PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous
#if NETCOREAPP3_1_OR_GREATER
                | PipeOptions.CurrentUserOnly
#endif
                );
            Pipe pipe = new(stream);

            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(Timeout);
            await stream.WaitForConnectionAsync(cts.Token).ConfigureAwait(false);

            return pipe;
        }

        public static async Task<Pipe> ConnectAsync(string name, Guid id, bool bidirectional, CancellationToken cancellationToken = default)
        {
            NamedPipeClientStream stream = new(".", GetName(name, id), bidirectional ? PipeDirection.InOut : PipeDirection.In, PipeOptions.Asynchronous
#if NETCOREAPP3_1_OR_GREATER
                | PipeOptions.CurrentUserOnly
#endif
                );
            var pipe = new Pipe(stream);
            await stream.ConnectAsync(Timeout, cancellationToken).ConfigureAwait(false);

            return pipe;
        }

        private static string GetName(string name, Guid id)
            => $"{name}-{id}";

        public PipeStream Stream
            => _stream;

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
