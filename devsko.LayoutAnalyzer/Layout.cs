using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
    public sealed class Layout
    {
        public Field[] Fields { get; private init; }
        public int TotalSize { get; private init; }
        public int TotalPadding { get; private init; }
        public TokenizedString Name { get; private init; }
        public bool IsValueType { get; private init; }
        public LayoutKind AttributeKind { get; private init; }
        public int AttributeSize { get; private init; }
        public int AttributePack { get; private init; }

        internal Layout(Type type, Analyzer analyzer)
        {
            Fields = analyzer.GetFields(type);
            (Name, TotalSize) = analyzer.GetNameAndSize(type);
            TotalPadding = TotalSize - analyzer.GetUnpaddedSize(Fields);
            IsValueType = type.IsValueType;
            var layoutAttr = type.StructLayoutAttribute;
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
