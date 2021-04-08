using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text;

namespace devsko.LayoutAnalyzer.Host
{
    internal static partial class Interop
    {
        internal static class HostPolicy
        {
            [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
            internal delegate void corehost_resolve_component_dependencies_result_fn(string assemblyPaths,
                string nativeSearchPaths, string resourceSearchPaths);

            [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Auto)]
            internal delegate void corehost_error_writer_fn(string message);

            [DllImport("hostpolicy.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
            internal static extern int corehost_resolve_component_dependencies(string componentMainAssemblyPath,
                corehost_resolve_component_dependencies_result_fn result);

            [DllImport("hostpolicy.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Auto)]
            internal static extern IntPtr corehost_set_error_writer(IntPtr errorWriter);
        }
    }

        public sealed class AssemblyDependencyResolver
        {
            /// <summary>
            /// The name of the neutral culture (same value as in Variables::Init in CoreCLR)
            /// </summary>
            private const string NeutralCultureName = "neutral";

            /// <summary>
            /// The extension of resource assembly (same as in BindSatelliteResourceByResourceRoots in CoreCLR)
            /// </summary>
            private const string ResourceAssemblyExtension = ".dll";

            private readonly Dictionary<string, string> _assemblyPaths;
            private readonly string[] _nativeSearchPaths;
            private readonly string[] _resourceSearchPaths;
            private readonly string[] _assemblyDirectorySearchPaths;

            public AssemblyDependencyResolver(string componentAssemblyPath)
            {
                if (componentAssemblyPath == null)
                {
                    throw new ArgumentNullException(nameof(componentAssemblyPath));
                }

                string? assemblyPathsList = null;
                string? nativeSearchPathsList = null;
                string? resourceSearchPathsList = null;
                int returnCode = 0;

                StringBuilder errorMessage = new StringBuilder();
                try
                {
                    // Setup error writer for this thread. This makes the hostpolicy redirect all error output
                    // to the writer specified. Have to store the previous writer to set it back once this is done.
                    var errorWriter = new Interop.HostPolicy.corehost_error_writer_fn(message => errorMessage.AppendLine(message));

                    IntPtr errorWriterPtr = Marshal.GetFunctionPointerForDelegate(errorWriter);
                    IntPtr previousErrorWriterPtr = Interop.HostPolicy.corehost_set_error_writer(errorWriterPtr);

                    try
                    {
                        // Call hostpolicy to do the actual work of finding .deps.json, parsing it and extracting
                        // information from it.
                        returnCode = Interop.HostPolicy.corehost_resolve_component_dependencies(
                            componentAssemblyPath,
                            (assemblyPaths, nativeSearchPaths, resourceSearchPaths) =>
                            {
                                assemblyPathsList = assemblyPaths;
                                nativeSearchPathsList = nativeSearchPaths;
                                resourceSearchPathsList = resourceSearchPaths;
                            });
                    }
                    finally
                    {
                        // Reset the error write to the one used before
                        Interop.HostPolicy.corehost_set_error_writer(previousErrorWriterPtr);
                        GC.KeepAlive(errorWriter);
                    }
                }
                catch (EntryPointNotFoundException entryPointNotFoundException)
                {
                    throw new InvalidOperationException("AssemblyDependencyResolver_FailedToLoadHostpolicy", entryPointNotFoundException);
                }
                catch (DllNotFoundException dllNotFoundException)
                {
                    throw new InvalidOperationException("AssemblyDependencyResolver_FailedToLoadHostpolicy", dllNotFoundException);
                }

                if (returnCode != 0)
                {
                    // Something went wrong - report a failure
                    throw new InvalidOperationException(string.Format(
                        "AssemblyDependencyResolver_FailedToResolveDependencies {0} {1} {2}",
                        componentAssemblyPath,
                        returnCode,
                        errorMessage));
                }

                string[] assemblyPaths = SplitPathsList(assemblyPathsList);

                // Assembly simple names are case insensitive per the runtime behavior
                // (see SimpleNameToFileNameMapTraits for the TPA lookup hash).
                _assemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string assemblyPath in assemblyPaths)
                {
                    // Add the first entry with the same simple assembly name if there are multiples
                    // and ignore others
                    _assemblyPaths.TryAdd(Path.GetFileNameWithoutExtension(assemblyPath), assemblyPath);
                }

                _nativeSearchPaths = SplitPathsList(nativeSearchPathsList);
                _resourceSearchPaths = SplitPathsList(resourceSearchPathsList);

                _assemblyDirectorySearchPaths = new string[1] { Path.GetDirectoryName(componentAssemblyPath)! };
            }

            public string? ResolveAssemblyToPath(AssemblyName assemblyName)
            {
                if (assemblyName == null)
                {
                    throw new ArgumentNullException(nameof(assemblyName));
                }

                // Determine if the assembly name is for a satellite assembly or not
                // This is the same logic as in AssemblyBinder::BindByTpaList in CoreCLR
                // - If the culture name is non-empty and it's not 'neutral'
                // - The culture name is the value of the AssemblyName.Culture.Name
                //     (CoreCLR gets this and stores it as the culture name in the internal assembly name)
                //     AssemblyName.CultureName is just a shortcut to AssemblyName.Culture.Name.
                if (!string.IsNullOrEmpty(assemblyName.CultureName) &&
                    !string.Equals(assemblyName.CultureName, NeutralCultureName, StringComparison.OrdinalIgnoreCase))
                {
                    // Load satellite assembly
                    // Search resource search paths by appending the culture name and the expected assembly file name.
                    // Copies the logic in BindSatelliteResourceByResourceRoots in CoreCLR.
                    // Note that the runtime will also probe APP_PATHS the same way, but that feature is effectively
                    // being deprecated, so we chose to not support the same behavior for components.
                    foreach (string searchPath in _resourceSearchPaths)
                    {
                        string assemblyPath = Path.Combine(
                            searchPath,
                            assemblyName.CultureName,
                            assemblyName.Name + ResourceAssemblyExtension);
                        if (File.Exists(assemblyPath))
                        {
                            return assemblyPath;
                        }
                    }
                }
                else if (assemblyName.Name != null)
                {
                    // Load code assembly - simply look it up in the dictionary by its simple name.
                    if (_assemblyPaths.TryGetValue(assemblyName.Name, out string? assemblyPath))
                    {
                        // Only returnd the assembly if it exists on disk - this is to make the behavior of the API
                        // consistent. Resource and native resolutions will only return existing files
                        // so assembly resolution should do the same.
                        if (File.Exists(assemblyPath))
                        {
                            return assemblyPath;
                        }
                    }
                }

                return null;
            }

            private static string[] SplitPathsList(string? pathsList)
            {
                if (pathsList == null)
                {
                    return Array.Empty<string>();
                }
                else
                {
                    return pathsList.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }

    partial class TypeLoader
    {
        private class LoadContext : AssemblyLoadContext
        {
            private readonly AssemblyDependencyResolver _resolver;

            public LoadContext(string basePath)
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

                return assembly;
            }
        }

        private LoadContext _loadContext;
        private Analyzer _analyzer;

        [MemberNotNull(nameof(_loadContext))]
        [MemberNotNull(nameof(_analyzer))]
        private void InitializeCore()
        {
            _loadContext = new LoadContext(Path.Combine(_appDirectory.Path, Path.GetFileName(AssemblyPath)));
            _analyzer = new Analyzer();
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

        private void DisposeCore()
        {
            _analyzer = null!;
        }
    }
}
