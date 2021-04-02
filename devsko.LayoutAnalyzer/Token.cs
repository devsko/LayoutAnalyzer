using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace devsko.LayoutAnalyzer
{
    public enum Token : ushort
    {
        Identifier,
        Struct,
        Class,
        Interface,
        Keyword,
        Punctuation,
        Operator,
    }
}
