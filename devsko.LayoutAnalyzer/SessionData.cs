using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public record SessionData
    (
        string ProjectFilePath,
        bool Debug,
        Platform Platform,
        string TargetFramework,
        bool Exe
    );
}
