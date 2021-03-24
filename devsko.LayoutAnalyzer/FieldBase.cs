using System;

namespace devsko.LayoutAnalyzer
{
#if NET40_OR_GREATER
    [Serializable]
#endif
    public abstract class FieldBase
    {
        public int Offset { get; protected
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public int Size { get; protected
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
    }
}
