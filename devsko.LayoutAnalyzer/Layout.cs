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

        private Layout(Type type)
        {
            Fields = Field.GetFields(type);
            (Name, TotalSize) = GetNameAndSize(type);
            TotalPadding = TotalSize - Field.GetUnpaddedSize(Fields);
            IsValueType = type.IsValueType;
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

        public static Layout? Analyze(Type type)
        {
            var layoutAttr = type.StructLayoutAttribute;
            if (layoutAttr is null)
            {
                return null;
            }

            return new Layout(type)
            {
                AttributeKind = layoutAttr.Value,
                AttributeSize = layoutAttr.Size,
                AttributePack = layoutAttr.Pack,
            };
        }

        private unsafe static (TokenizedString Name, int Size) GetNameAndSize(Type type)
        {
            (TokenizedString name, int size) = FieldType.GetNameAndSize(type, null);

            if (!type.IsValueType)
            {
                int dword =
                    Unsafe.Add(
                        ref Unsafe.AsRef<int>(
                            type.TypeHandle.Value.ToPointer()),
                        1);

                size = dword - 2 * sizeof(IntPtr);
            }

            return (name, size);
        }
    }
}
