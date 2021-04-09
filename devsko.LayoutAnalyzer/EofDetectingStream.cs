using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public class EofDetectingStream : Stream
    {
        private Stream _stream;
        private bool _eofDetected;

        public EofDetectingStream(Stream stream)
        {
            _stream = stream;
        }

        public void Reset()
        {
            _eofDetected = false;
        }

        public override async
#if NETCOREAPP3_1_OR_GREATER
            ValueTask
#else
            Task
#endif
            <int>
            ReadAsync(
#if NETCOREAPP3_1_OR_GREATER
            Memory<byte> buffer,
#else
            byte[] buffer, int offset, int count,
#endif
            CancellationToken cancellationToken)
        {
            if (_eofDetected)
            {
                return 0;
            }
            int result = await _stream.ReadAsync(buffer,
#if !NETCOREAPP3_1_OR_GREATER
                offset, count,
#endif
                cancellationToken).ConfigureAwait(false);
            if (result == 0)
            {
                return 0;
            }
            _eofDetected |=
#if NETCOREAPP3_1_OR_GREATER
                buffer.Span[result - 1]
#else
                buffer[offset + result - 1]
#endif
                == 0x27;

            return _eofDetected ? result - 1 : result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }
            base.Dispose(disposing);
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw new NotImplementedException();

        public override bool CanRead
            => _stream.CanRead;
        public override bool CanSeek
            => _stream.CanSeek;
        public override bool CanWrite
            => _stream.CanWrite;
        public override long Length
            => _stream.Length;
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }
        public override void Flush()
            => _stream.Flush();
        public override long Seek(long offset, SeekOrigin origin)
            => _stream.Seek(offset, origin);
        public override void SetLength(long value)
            => _stream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count)
            => _stream.Write(buffer, offset, count);
    }
}
