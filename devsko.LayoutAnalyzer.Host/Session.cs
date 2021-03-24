using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed class Session : IDisposable
    {
        private JsonSerializerOptions _jsonOptions;
        private Stream _outStream;
        private SemaphoreSlim _semaphore;
        private TypeLoader _typeLoader;

        public Session(Stream outStream, string assemblyPath)
        {
            _jsonOptions = new JsonSerializerOptions();
            _outStream = outStream;
            _semaphore = new SemaphoreSlim(1);
            _typeLoader = new TypeLoader(assemblyPath);
        }

        public async Task AnalyzeAsync(string typeName, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                int index = typeName.IndexOf(',');
                if (index < 0 || index >= typeName.Length - 1)
                {
                    throw new InvalidOperationException($"Wrong type name format '{typeName}'");
                }

                Layout? layout = _typeLoader.LoadAndAnalyze(
                    new AssemblyName(typeName.Substring(index + 1)),
                    typeName.Substring(0, index));

                if (layout is not null)
                {
                    await JsonSerializer.SerializeAsync(_outStream, layout, _jsonOptions, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
            _jsonOptions = null!;
            _typeLoader.Dispose();
        }
    }
}
