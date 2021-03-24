using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

namespace devsko.LayoutAnalyzer.Host
{
    partial class TypeLoader
    {
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
                //Console.Error.WriteLine($"loading assembly '{assemblyName.Name} ({assemblyName.Version})' {(assembly is null ? "in default load context" : $"from '{path}'")}");

                return assembly;
            }
        }

        private Analyzer _analyzer;
        private LoadContext _loadContext;

        public TypeLoader(string path)
        {
            _analyzer = new Analyzer();
            _loadContext = new LoadContext(path);
        }

        public Layout? LoadAndAnalyze(AssemblyName assemblyName, string typeName)
        {
            Assembly assembly = _loadContext.LoadFromAssemblyName(assemblyName);
            Type? type = assembly.GetType(typeName);

            if (type is null)
            {
                throw new InvalidOperationException($"Type not found {typeName}");
            }

            return _analyzer.Analyze(type);
        }

        partial void DisposeInternal()
        {
            _analyzer = null!;

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
        }
    }
}
