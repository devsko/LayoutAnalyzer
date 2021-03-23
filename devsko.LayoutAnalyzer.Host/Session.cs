using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.Loader;
#endif
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed class Session : IDisposable
    {
#if NETCOREAPP3_1_OR_GREATER
        private class LoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;

            public LoadContext(string basePath)
                : base("LayoutAnalyzer.Session", isCollectible: true)
            {
                _resolver = new(basePath);
            }

            protected override Assembly? Load(AssemblyName assemblyName)
            {
                Assembly? assembly = null;
                string? path = _resolver.ResolveAssemblyToPath(assemblyName);
                if (path is not null)
                {
                    assembly = LoadFromAssemblyPath(path);
                }
                Console.Error.WriteLine($"loading assembly '{assemblyName.Name} ({assemblyName.Version})' {(assembly is null ? "in default load context" : $"from '{path}'")}");

                return assembly;
            }
        }
#endif

        private Analyzer _analyzer;
        private JsonSerializerOptions _jsonOptions;
        private Stream _outStream;
        private SemaphoreSlim _semaphore;
#if NETCOREAPP3_1_OR_GREATER
        private LoadContext _loadContext;
#endif

        public Session(Stream outStream, string assemblyPath)
        {
            _analyzer = new Analyzer();
            _jsonOptions = new JsonSerializerOptions();
            _outStream = outStream;
            _semaphore = new SemaphoreSlim(1);
#if NETCOREAPP3_1_OR_GREATER
            _loadContext = new LoadContext(assemblyPath);
#endif
        }

        public async Task AnalyzeAsync(string typeName, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                Type? type = null;
#if NETCOREAPP3_1_OR_GREATER
                int index = typeName.IndexOf(',');
                if (index < 0 || index >= typeName.Length - 1)
                {
                    throw new InvalidOperationException($"Wrong type name format '{typeName}'");
                }
                var assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(typeName.Substring(index + 1)));
                type = assembly.GetType(typeName.Substring(0, index));
#endif
                if (type is null)
                {
                    throw new InvalidOperationException($"Type not found {typeName}");
                }
                Layout? layout = _analyzer.Analyze(type);
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
            _analyzer = null!;

#if NETCOREAPP3_1_OR_GREATER
            WeakReference weakRef = UnloadContext();

            for (int i = 0; i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                if (!weakRef.IsAlive)
                {
                    Console.Error.WriteLine("LoadContext collected");
                    break;
                }
                Console.Error.WriteLine("Waiting for GC " + i);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            WeakReference UnloadContext()
            {
                try
                {
                    return new WeakReference(_loadContext);
                }
                finally
                {
                    _loadContext?.Unload();
                    _loadContext = null!;
                }
            }
#endif
        }
    }
}
