using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace devsko.LayoutAnalyzer
{
    public readonly struct FieldType
    {
        public Type Type { get; private init; }
        public TransformFlags TransformFlags { get; private init; }

        public FieldType(Type type, TransformFlags transformFlags)
        {
            Type = type;
            TransformFlags = transformFlags;
        }
    }
}
