using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer
{
    public enum TargetFramework
    {
        NetFramework,
        NetCore,
        Net5,
        Net6,
        NetStandard,
    }

    public enum Platform
    {
        Any,
        x64,
        x86,
    }

    public record ProjectData
    (
        string ProjectFilePath,
        bool Debug,
        Platform Platform,
        string TargetFramework,
        bool Exe
    );
}
