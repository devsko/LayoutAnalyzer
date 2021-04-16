using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace devsko.LayoutAnalyzer.Runner
{
    public static class Log
    {
        public static event Action<string>? MessageReceived;

        public static void WriteLine(string line)
        {
            MessageReceived?.Invoke(line);
        }
    }
}
