using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace devsko.LayoutAnalyzer.Runner
{
    public class HotReloadWatcher
    {
        private FileWatcher _fileWatcher;
        public HotReloadWatcher(HotReload hotReload, Project project)
        {
            _fileWatcher = new FileWatcher(HotReload.GetAllSourceFilePaths(project));

        }
    }
}
