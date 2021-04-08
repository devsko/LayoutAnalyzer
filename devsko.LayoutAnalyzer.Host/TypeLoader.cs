using System;
using System.IO;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed partial class TypeLoader : IDisposable
    {
        public string AssemblyPath { get; private init; }

        private AppDirectory _appDirectory;
        private readonly FileSystemWatcher _watcher;

        public event Action? AssemblyDirectoryChanged;

        public TypeLoader(string assemblyPath)
        {
            AssemblyPath = assemblyPath;
            _appDirectory = new AppDirectory();
            CopyFiles(Path.GetDirectoryName(assemblyPath)!);

            _watcher = new FileSystemWatcher(Path.GetDirectoryName(assemblyPath)!);
            _watcher.Changed += (sender, e) =>
            {
                Console.Error.WriteLine($"{DateTime.Now:mm:ss,fffffff} {e.ChangeType}");

                _watcher.EnableRaisingEvents = false;
                AssemblyDirectoryChanged?.Invoke();
            };
            _watcher.EnableRaisingEvents = true;

            Console.Error.WriteLine("Watching " + _watcher.Path);

            InitializeCore();

            Console.Error.WriteLine("TypeLoader initialized");
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

            Console.Error.WriteLine("TypeLoader disposed");
        }

        private void CopyFiles(string directory, string searchPattern = "*.*")
            => _appDirectory.CopyFiles(directory, searchPattern);
    }
}
