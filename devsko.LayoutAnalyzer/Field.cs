using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
#if NET40_OR_GREATER
    [Serializable]
#endif
    public sealed class Field : FieldBase
    {
        public ulong Handle
        {
            get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public TokenizedString TypeAndName
        {
            get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public Token Kind
        {
            get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public bool IsPublic { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public Field[]? Children { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }

        public Field(FieldInfo info, TokenizedString typeName, int size, int offset, Field[]? children)
        {
            Handle = (ulong)info.FieldHandle.Value;
            IsPublic = info.IsPublic;
            Type type = info.FieldType;
            Kind = Layout.GetKind(type);
            TypeAndName = typeName.Append(new TokenizedString(' ' + info.Name, Token.Identifier));
            Size = size;
            Offset = offset;
            Children = children;
        }

        [JsonConstructor]
        public Field(int offset, int size, TokenizedString typeAndName, Token kind, bool isPublic, Field[]? children)
        {
            Offset = offset;
            Size = size;
            TypeAndName = typeAndName;
            Kind = kind;
            IsPublic = isPublic;
            Children = children;
        }

        [JsonIgnore]
        public IEnumerable<FieldBase> FieldsAndPaddings
            => Padding
                .EnumerateWithPaddings(Children, Size, 0, 0, false)
                .Select(tuple => tuple.Field);

    }
}
