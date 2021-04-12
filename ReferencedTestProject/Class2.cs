using System;
using System.Collections.Generic;
using System.Text;

namespace RefererncedTestProject
{
    public class Class2
    {
        public int I1=42;
#if NETCOREAPP2_1_OR_GREATER
        public ReferencedTestProject2.Class1 C1 = new();
#endif
        public bool B2;
    }
}
