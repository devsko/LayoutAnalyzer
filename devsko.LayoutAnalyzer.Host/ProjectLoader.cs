using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed class ProjectLoader : IAsyncDisposable
    {
        private static readonly Dictionary<string, ProjectLoader> s_all = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim s_semaphore = new(1);

        private static string GetKey(ProjectData data)
            => data.ProjectFilePath;

        public static ProjectLoader? Get(string projectFilePath)
            => s_all.TryGetValue(projectFilePath, out ProjectLoader? loader) ? loader : null;

        public static async ValueTask<ProjectLoader> GetOrCreateAsync(Stream stream, ProjectData data)
        {
            ProjectLoader? project;
            await s_semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!s_all.TryGetValue(GetKey(data), out project))
                {
                    s_all.Add(GetKey(data), project = new ProjectLoader(stream, data));
                }
            }
            finally
            {
                s_semaphore.Release();
            }

            return project;
        }

        public static async ValueTask DisposeAllAsync()
        {
            await s_semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (ProjectLoader project in s_all.Values)
                {
                    await project.DisposeCoreAsync(false).ConfigureAwait(false);
                }
                s_all.Clear();
            }
            finally
            {
                s_semaphore.Release();
            }
        }

        private string _key;
        private Stream _stream;
        private SemaphoreSlim _semaphore;
        private TypeLoader _typeLoader;

        public ProjectLoader(Stream stream, ProjectData data)
        {
            _key = GetKey(data);
            _stream = stream;
            _semaphore = new SemaphoreSlim(1);
            _typeLoader = new TypeLoader(data);
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

                    await Log.WriteLineAsync($"{typeName}: size = {layout.TotalSize}, fields = {layout.Fields.Length}").ConfigureAwait(false);
                    foreach (var field in layout.Fields)
                        await Log.WriteLineAsync($"{field.Handle} {field.TypeAndName.Value}").ConfigureAwait(false);

                    layout.AssemblyPath = _typeLoader.GetOriginalPath(layout.AssemblyPath);
                    await JsonSerializer.SerializeAsync(_stream, layout, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public ValueTask DisposeAsync()
            => DisposeCoreAsync(true);

        private async ValueTask DisposeCoreAsync(bool remove)
        {
            await _typeLoader.DisposeAsync().ConfigureAwait(false);

            if (remove)
            {
                await s_semaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    s_all.Remove(_key);
                }
                finally
                {
                    s_semaphore.Release();
                }
            }

            await Log.WriteLineAsync($"Project loader {Path.GetFileName(_key)} disposed").ConfigureAwait(false);
        }
    }
}
