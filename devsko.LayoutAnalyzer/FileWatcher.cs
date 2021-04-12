using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public class FileWatcher : IDisposable
    {
        private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _paths;
        private TaskCompletionSource<string>? _taskCompletionSource;

        public FileWatcher(IEnumerable<string> paths)
        {
            _paths = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
            foreach (string path in paths)
            {
                WatchDirectory(Path.GetDirectoryName(path)!);
            }
        }

        public Task<string> GetChangedFileAsync(CancellationToken cancellationToken)
        {
            _taskCompletionSource = new TaskCompletionSource<string>();
            cancellationToken.Register(() => _taskCompletionSource.TrySetCanceled());

            return _taskCompletionSource.Task;
        }

        private void WatchDirectory(string directory)
        {
            if (directory[^1] != Path.DirectorySeparatorChar)
            {
                directory += Path.DirectorySeparatorChar;
            }

            foreach (KeyValuePair<string, FileSystemWatcher> kvp in _watchers)
            {
                if (directory.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                if (kvp.Key.StartsWith(directory, StringComparison.OrdinalIgnoreCase))
                {
                    DisposeWatcher(kvp.Key);
                }
            }

            FileSystemWatcher newWatcher = new(directory);
            newWatcher.Changed += WatcherChangedHandler;
            newWatcher.Deleted += WatcherChangedHandler;
            newWatcher.Renamed += WatcherRenamedHandler;
            newWatcher.EnableRaisingEvents = true;

            _watchers.Add(directory, newWatcher);
        }

        public void Dispose()
        {
            foreach (KeyValuePair<string, FileSystemWatcher> kvp in _watchers)
            {
                FileSystemWatcher watcher = kvp.Value;
                watcher.Changed -= WatcherChangedHandler;
                watcher.Deleted -= WatcherChangedHandler;
                watcher.Renamed -= WatcherRenamedHandler;
                watcher.Dispose();
            }
            _watchers.Clear();
        }

        private void WatcherChangedHandler(object sender, FileSystemEventArgs args)
        {
            FileChanged(args.FullPath);
        }

        private void WatcherRenamedHandler(object sender, RenamedEventArgs args)
        {
            if (!string.IsNullOrEmpty(args.OldFullPath))
            {
                FileChanged(args.OldFullPath);
            }
            if (!string.IsNullOrEmpty(args.FullPath))
            {
                FileChanged(args.FullPath);
            }
        }

        private void FileChanged(string path)
        {
            TaskCompletionSource<string>? tcs = _taskCompletionSource;
            if (tcs is null)
            {
                return;
            }

            if (_paths.TryGetValue(path, out string? registered) && registered is not null)
            {
                tcs = Interlocked.CompareExchange(ref _taskCompletionSource, null, tcs);
                tcs?.TrySetResult(registered);
            }
        }

        private void DisposeWatcher(string directory)
        {
            var watcher = _watchers[directory];
            _watchers.Remove(directory);

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= WatcherChangedHandler;
            watcher.Dispose();
        }
    }
}
