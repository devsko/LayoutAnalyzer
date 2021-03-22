using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Test
{
    public enum HostType
    {
        NetFramework,
        NetCore,
        Net5Plus,
    }

    public sealed class HostRunner
    {
        public HostRunner()
        {
            ProcessStartInfo start = new();
        }
    }
}
