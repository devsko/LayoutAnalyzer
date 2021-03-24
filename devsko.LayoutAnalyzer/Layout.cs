using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
#if NET40_OR_GREATER
    [Serializable]
#endif
    public sealed class Layout
    {
        public Field[] Fields { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public int TotalSize { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public int TotalPadding { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public TokenizedString Name { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public bool IsValueType { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public LayoutKind AttributeKind { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public int AttributeSize { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public int AttributePack { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }

        internal Layout(Type type, Analyzer analyzer)
        {
            Fields = analyzer.GetFields(type);
            (Name, TotalSize) = analyzer.GetNameAndSize(type);
            TotalPadding = TotalSize - analyzer.GetUnpaddedSize(Fields);
            IsValueType = type.IsValueType;
            StructLayoutAttribute? layoutAttr = type.StructLayoutAttribute!;
            AttributeKind = layoutAttr.Value;
            AttributeSize = layoutAttr.Size;
            AttributePack = layoutAttr.Pack;
        }

        [JsonConstructor]
        public Layout(Field[] fields, int totalSize, int totalPadding, TokenizedString name, bool isValueType, LayoutKind attributeKind, int attributeSize, int attributePack)
        {
            Fields = fields;
            TotalSize = totalSize;
            TotalPadding = totalPadding;
            Name = name;
            IsValueType = isValueType;
            AttributeKind = attributeKind;
            AttributeSize = attributeSize;
            AttributePack = attributePack;
        }

        [JsonIgnore]
        public IEnumerable<(FieldBase Field, int TotalOffset, int Level)> FieldsWithPaddings
            => Padding.EnumerateWithPaddings(Fields, TotalSize, 0, 1);
    }
}
