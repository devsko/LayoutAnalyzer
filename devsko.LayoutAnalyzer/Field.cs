using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
    public sealed class Field : FieldBase
    {
        public string Name { get; private init; }
        public bool IsPublic { get; private init; }
        public TokenizedString TypeName { get; private init; }
        public Field[]? Children { get; private init; }

        internal Field(FieldInfo info, Analyzer analyzer)
        {
            Name = info.Name;
            IsPublic = info.IsPublic;
            Type type = info.FieldType;
            (TypeName, Size) = analyzer.GetNameAndSize(type, GetNativeIntegerData(info));
            Offset = GetOffset(info);
            if (type.IsValueType && !type.IsPrimitive)
            {
                Children = analyzer.GetFields(type);
            }
        }

        [JsonConstructor]
        public Field(int offset, int size, string name, bool isPublic, TokenizedString typeName, Field[]? children)
        {
            Offset = offset;
            Size = size;
            Name = name;
            IsPublic = isPublic;
            TypeName = typeName;
            Children = children;
        }

        private static readonly bool[] s_defaultNativeIntegerFlags = new[] { true };

        private unsafe static int GetOffset(FieldInfo info)
        {
            int dword =
                Unsafe.Add(
                    ref Unsafe.As<IntPtr, int>(
                        ref Unsafe.Add(
                            ref Unsafe.AsRef<IntPtr>(
                                info.FieldHandle.Value.ToPointer()),
                            1)
                        ),
                    1);

            return dword & ((1 << 27) - 1);
        }

        private static bool[]? GetNativeIntegerData(FieldInfo info)
        {
            CustomAttributeData? data = info
                .GetCustomAttributesData()
                .Where(static data => data.AttributeType.FullName == "System.Runtime.CompilerServices.NativeIntegerAttribute")
                .FirstOrDefault();

            if (data is null)
            {
                return null;
            }
            if (data.ConstructorArguments.Count < 1)
            {
                return s_defaultNativeIntegerFlags;
            }

            return ((IReadOnlyCollection<CustomAttributeTypedArgument>)data.ConstructorArguments[0].Value)
                .Select(arg => (bool)arg.Value)
                .ToArray();
        }
    }
}
