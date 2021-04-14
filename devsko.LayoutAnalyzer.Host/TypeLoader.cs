using System;
using System.IO;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed partial class TypeLoader : IAsyncDisposable
    {
        public string AssemblyPath { get; private init; }

        private AppDirectory _appDirectory;
        private readonly FileSystemWatcher _watcher;

        public event Action? AssemblyDirectoryChanged;

        public TypeLoader(ProjectData data)
        {
            // TODO get from MSBuild

            string assemblyPath = Path.Combine(
                Path.GetDirectoryName(data.ProjectFilePath)!,
                "bin",
                data.Platform == Platform.Any ? "" : data.Platform.ToString(),
                data.Debug ? "Debug" : "Release",
                data.TargetFramework,
                Path.ChangeExtension(Path.GetFileName(data.ProjectFilePath), data.Exe ? ".exe" : ".dll"));

            AssemblyPath = assemblyPath;
            _appDirectory = new AppDirectory();
            CopyFiles(Path.GetDirectoryName(assemblyPath)!);

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(assemblyPath)!);
            _watcher.Changed += async (sender, e) =>
            {
                await Log.WriteLineAsync($"{DateTime.Now:mm:ss,fffffff} {e.ChangeType}").ConfigureAwait(false);

                _watcher.EnableRaisingEvents = false;
                AssemblyDirectoryChanged?.Invoke();
            };
            _watcher.EnableRaisingEvents = true;

            //await log.WriteLineAsync("Watching " + _watcher.Path).ConfigureAwait(false);

            InitializeCore();

            //await log.WriteLineAsync("TypeLoader initialized").ConfigureAwait(false);
        }

        public string GetOriginalPath(string path)
        {
            if (path.StartsWith(_appDirectory.Path, StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Path.GetDirectoryName(AssemblyPath)!, path.Substring(_appDirectory.Path.Length + 1));
            }

            return path;
        }

        public async ValueTask DisposeAsync()
        {
            await DisposeCoreAsync().ConfigureAwait(false);

            _watcher.Dispose();
            _appDirectory.Dispose();

            await Log.WriteLineAsync("TypeLoader disposed").ConfigureAwait(false);
        }

        private void CopyFiles(string directory, string searchPattern = "*.*")
            => _appDirectory.CopyFiles(directory, searchPattern);
    }
}
