using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace devsko.LayoutAnalyzer
{
    public enum Token : ushort
    {
        Identifier,
        Class,
        Struct,
        Enum,
        Interface,
        Keyword,
        Punctuation,
        Operator,
    }
}
