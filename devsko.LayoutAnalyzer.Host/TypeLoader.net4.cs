using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        private MarshaledTypeLoader _marshaledTypeLoader;

        [MemberNotNull(nameof(_appDomain))]
        [MemberNotNull(nameof(_marshaledTypeLoader))]
        partial void InitializeCore()
        {
            string hostAssemblyDirectoryPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            CopyFiles(hostAssemblyDirectoryPath, "devsko.LayoutAnalyzer.???");
            CopyFiles(hostAssemblyDirectoryPath, "devsko.LayoutAnalyzer.Host.net4.???");
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

        partial void DisposeCore()
        {
            AppDomain.Unload(_appDomain);
        }
    }
}
