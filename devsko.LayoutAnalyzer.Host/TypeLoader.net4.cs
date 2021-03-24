using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using devsko.LayoutAnalyzer.Host.net4;

namespace devsko.LayoutAnalyzer.Host
{
    partial class TypeLoader
    {
        private AppDomain _appDomain;
        private MarshaledTypeLoader _typeLoader;

        public TypeLoader(string projectAssemblyPath)
        {
            string projectDirectoryPath = Path.GetDirectoryName(projectAssemblyPath);
            CopyFiles(projectDirectoryPath, "*.*", AppDirectoryPath);

            string hostAssemblyDirectoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            CopyFiles(hostAssemblyDirectoryPath, "devsko.LayoutAnalyzer.???", AppDirectoryPath);
            CopyFiles(hostAssemblyDirectoryPath, "devsko.LayoutAnalyzer.Host.net4.???", AppDirectoryPath);
            CopyFiles(hostAssemblyDirectoryPath, "System.Runtime.CompilerServices.Unsafe.???", AppDirectoryPath);
            CopyFiles(hostAssemblyDirectoryPath, "System.Memory.???", AppDirectoryPath);
            CopyFiles(hostAssemblyDirectoryPath, "System.Buffers.???", AppDirectoryPath);
            CopyFiles(hostAssemblyDirectoryPath, "System.Numerics.Vectors.???", AppDirectoryPath);

            AppDomainSetup setup = new()
            {
                ApplicationBase = AppDirectoryPath,
            };
            _appDomain = AppDomain.CreateDomain("LayoutAnalyzer", null, setup);
            _typeLoader = (MarshaledTypeLoader)_appDomain.CreateInstanceAndUnwrap(
                typeof(MarshaledTypeLoader).Assembly.FullName,
                typeof(MarshaledTypeLoader).FullName);
        }

        public Layout? LoadAndAnalyze(AssemblyName assemblyName, string typeName)
            => _typeLoader.LoadAndAnalyze(assemblyName, typeName);

        partial void DisposeInternal()
        {
            AppDomain.Unload(_appDomain);
        }
    }
}
