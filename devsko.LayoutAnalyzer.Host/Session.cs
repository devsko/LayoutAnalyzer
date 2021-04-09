using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed class Session : IDisposable
    {
        private static Dictionary<string, Session> s_allSessions = new(StringComparer.OrdinalIgnoreCase);
        private static object s_sync = new object();

        public static Session GetOrCreate(Pipe outPipe, string assemblyName, Pipe log)
        {
            lock (s_sync)
            {
                if (!s_allSessions.TryGetValue(assemblyName, out Session? session))
                {
                    s_allSessions.Add(assemblyName, session = new Session(outPipe, assemblyName, log));
                }

                return session;
            }
        }

        public static void DisposeAll()
        {
            lock (s_sync)
            {
                foreach (Session session in s_allSessions.Values)
                {
                    session.Dispose();
                }
                s_allSessions.Clear();
            }
        }

        private JsonSerializerOptions _jsonOptions;
        private Pipe _outPipe;
        private Pipe _log;
        private SemaphoreSlim _semaphore;
        private TypeLoader _typeLoader;

        public Session(Pipe outPipe, string assemblyPath, Pipe log)
        {
            _jsonOptions = new JsonSerializerOptions();
            _outPipe = outPipe;
            _log = log;
            _semaphore = new SemaphoreSlim(1);
            _typeLoader = new TypeLoader(assemblyPath, log);
            _typeLoader.AssemblyDirectoryChanged += () =>
            {
                Dispose();
            };
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
                    layout.AssemblyPath = _typeLoader.GetOriginalPath(layout.AssemblyPath);
                    await JsonSerializer.SerializeAsync(_outPipe.Stream, layout, _jsonOptions, cancellationToken).ConfigureAwait(false);
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
            lock (s_sync)
            {
                s_allSessions.Remove(_typeLoader.AssemblyPath);
            }

            //_log.WriteLine("Session disposed");
        }
    }
}
