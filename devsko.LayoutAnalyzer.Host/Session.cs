using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.CompilerServices;
#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.Loader;
#endif
using System.Text.Json;
using System.Threading;

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
                string? path = _resolver.ResolveAssemblyToPath(assemblyName);
                return path == null ? null : LoadFromAssemblyPath(path);
            }
        }
#endif

#if NETCOREAPP3_1_OR_GREATER
        private LoadContext _loadContext;
#endif
        private Analyzer _analyzer;
        private JsonSerializerOptions _jsonOptions;

        public Session(string assemblyPath)
        {
#if NETCOREAPP3_1_OR_GREATER
            _loadContext = new LoadContext(assemblyPath);
#endif
            _analyzer = new Analyzer();
            _jsonOptions = new JsonSerializerOptions
            {
#if DEBUG
                WriteIndented = true,
#endif
            };
        }

        public void SendAnalysis(string typeName)
        {
#if NETCOREAPP3_1_OR_GREATER
            int comma = typeName.IndexOf(',');
            var assembly = _loadContext.LoadFromAssemblyName(new AssemblyName(typeName.Substring(comma + 1)));
            Type? type = assembly.GetType(typeName.Substring(0, comma));
            if (type is not null)
            {
                Layout? layout = _analyzer.Analyze(type);
                if (layout is not null)
                {
                    Console.WriteLine(JsonSerializer.Serialize(layout, _jsonOptions));
                }
            }
#endif
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

                if (!weakRef.IsAlive)
                {
                    Console.WriteLine("LoadContext collected");
                    break;
                }
                Console.WriteLine("Waiting for GC " + i);
            }
#endif
        }

#if NETCOREAPP3_1_OR_GREATER
        [MethodImpl(MethodImplOptions.NoInlining)]
        private WeakReference UnloadContext()
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
