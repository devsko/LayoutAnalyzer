using System;

namespace devsko.LayoutAnalyzer.Host
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
