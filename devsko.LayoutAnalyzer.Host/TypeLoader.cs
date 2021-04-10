using System;
using System.IO;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed partial class TypeLoader : IDisposable
    {
        public string AssemblyPath { get; private init; }

        private AppDirectory _appDirectory;
        private readonly FileSystemWatcher _watcher;
        private readonly Pipe _log;

        public event Action? AssemblyDirectoryChanged;

        public TypeLoader(string projectFilePath, Pipe log)
        {
            // TODO
            string assemblyPath = Path.Combine(
                Path.GetDirectoryName(projectFilePath)!,
                "bin", "Debug", "net5.0",
                Path.ChangeExtension(Path.GetFileName(projectFilePath), ".dll"));

            _log = log;
            AssemblyPath = assemblyPath;
            _appDirectory = new AppDirectory();
            CopyFiles(Path.GetDirectoryName(assemblyPath)!);

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(assemblyPath)!);
            _watcher.Changed += async (sender, e) =>
            {
                await log.WriteLineAsync($"{DateTime.Now:mm:ss,fffffff} {e.ChangeType}").ConfigureAwait(false);

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

        public void Dispose()
        {
            DisposeCore();

            _watcher.Dispose();
            _appDirectory.Dispose();

            //_log.WriteLine("TypeLoader disposed");
        }

        private void CopyFiles(string directory, string searchPattern = "*.*")
            => _appDirectory.CopyFiles(directory, searchPattern);
    }
}
