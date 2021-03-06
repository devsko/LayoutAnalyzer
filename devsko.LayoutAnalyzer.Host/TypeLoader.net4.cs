using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Host
{
    partial class TypeLoader
    {
        private AppDomain _appDomain;
        private MarshaledTypeLoader _marshaledTypeLoader;

        [MemberNotNull(nameof(_appDomain))]
        [MemberNotNull(nameof(_marshaledTypeLoader))]
        private void InitializeCore()
        {
            string hostAssemblyDirectoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            CopyFiles(hostAssemblyDirectoryPath, "devsko.LayoutAnalyzer.???");
            CopyFiles(hostAssemblyDirectoryPath, "devsko.LayoutAnalyzer.Host.???");
            CopyFiles(hostAssemblyDirectoryPath, "System.Runtime.CompilerServices.Unsafe.???");
            CopyFiles(hostAssemblyDirectoryPath, "System.Memory.???");
            CopyFiles(hostAssemblyDirectoryPath, "System.Buffers.???");
            CopyFiles(hostAssemblyDirectoryPath, "System.Numerics.Vectors.???");

            AppDomainSetup setup = new()
            {
                ApplicationBase = _appDirectory.Path,
            };
            _appDomain = AppDomain.CreateDomain("LayoutAnalyzer", null, setup);
            _marshaledTypeLoader = (MarshaledTypeLoader)_appDomain.CreateInstanceAndUnwrap(
                typeof(MarshaledTypeLoader).Assembly.FullName,
                typeof(MarshaledTypeLoader).FullName);
        }

        public Layout? LoadAndAnalyze(AssemblyName assemblyName, string typeName)
            => _marshaledTypeLoader.LoadAndAnalyze(assemblyName, typeName);

        private ValueTask DisposeCoreAsync()
        {
            AppDomain.Unload(_appDomain);

            return default;
        }
    }
}
