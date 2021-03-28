using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

        partial void InitializeCore();

        partial void DisposeCore();

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
