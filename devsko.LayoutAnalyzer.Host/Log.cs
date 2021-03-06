using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public class Log : IDisposable
    {
        private static Log? _instance;

        private Pipe _pipe;
        private TextWriter? _writer;

        public Log(Pipe pipe)
        {
            _pipe = pipe;
        }

        public async static Task<Log> InitializeAsync(Guid id)
        {
            if (_instance is not null)
            {
                throw new InvalidOperationException();
            }

            _instance = new Log(await Pipe.StartServerAsync(Pipe.LogName, id, bidirectional: false, cancellationToken: default).ConfigureAwait(false));
            await _instance._WriteLineAsync("").ConfigureAwait(false);

            return _instance;
        }

        public static Task WriteLineAsync(string line)
        {
            if (_instance is null)
            {
                throw new InvalidOperationException();
            }
            return _instance._WriteLineAsync(line);
        }

        public void Dispose()
        {
            try
            {
                _writer?.Dispose();
            }
            catch (IOException)
            { }
            _pipe.Dispose();
        }

        private async Task _WriteLineAsync(string line)
        {
            if (_pipe.Stream.IsConnected)
            {
                _writer ??= new StreamWriter(_pipe.Stream, Encoding.UTF8, 1024, leaveOpen: true);
                try
                {
                    await _writer.WriteLineAsync(line).ConfigureAwait(false);
                    await _writer.FlushAsync().ConfigureAwait(false);
                }
                catch (IOException)
                { }
            }
        }
    }
}
