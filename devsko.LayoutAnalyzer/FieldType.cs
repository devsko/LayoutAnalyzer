using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace devsko.LayoutAnalyzer
{
    public class TransformFlags
    {
        private readonly bool[]? _flags;
        private int _index;

        public TransformFlags(bool[]? flags)
        {
            _flags = flags;
        }

        public bool Next
            => _flags is not null && _index < _flags.Length && _flags[_index++];
    }

    public struct FieldType
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
