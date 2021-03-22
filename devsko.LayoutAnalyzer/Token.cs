using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace devsko.LayoutAnalyzer
{
    public enum Token : ushort
    {
        Namespace = 0,
        StructRef = 1,
        ClassRef = 2,
        Keyword = 3,
        Identifier = 4,
        Symbol = 5,
    }
}
