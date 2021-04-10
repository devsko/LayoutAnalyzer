using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed class Session : IAsyncDisposable
    {
        private static readonly Dictionary<string, Session> s_allSessions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim s_semaphore = new(1);

        private static string GetSessionKey(SessionData data)
            => data.ProjectFilePath;

        public static async ValueTask<Session> GetOrCreateAsync(Stream stream, SessionData data, Pipe log)
        {
            Session? session;
            await s_semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!s_allSessions.TryGetValue(GetSessionKey(data), out session))
                {
                    s_allSessions.Add(GetSessionKey(data), session = new Session(stream, data, log));
                }
            }
            finally
            {
                s_semaphore.Release();
            }

            return session;
        }

        public static async ValueTask DisposeAllAsync()
        {
            await s_semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (Session session in s_allSessions.Values)
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                s_allSessions.Clear();
            }
            finally
            {
                s_semaphore.Release();
            }
        }

        private string _key;
        private Stream _stream;
        private Pipe _log;
        private SemaphoreSlim _semaphore;
        private TypeLoader _typeLoader;

        public Session(Stream stream, SessionData data, Pipe log)
        {
            _key = GetSessionKey(data);
            _stream = stream;
            _log = log;
            _semaphore = new SemaphoreSlim(1);
            _typeLoader = new TypeLoader(data, log);
            _typeLoader.AssemblyDirectoryChanged += async () =>
            {
                await DisposeAsync().ConfigureAwait(false);
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
                    await JsonSerializer.SerializeAsync(_stream, layout, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _typeLoader.DisposeAsync().ConfigureAwait(false);

            await s_semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                s_allSessions.Remove(_key);
            }
            finally
            {
                s_semaphore.Release();
            }

            await _log.WriteLineAsync($"Session {Path.GetFileName(_key)} disposed").ConfigureAwait(false);
        }
    }
}
