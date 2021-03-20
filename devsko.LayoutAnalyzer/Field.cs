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

        public Field(FieldInfo info)
        {
            Name = info.Name;
            IsPublic = info.IsPublic;
            Type type = info.FieldType;
            (TypeName, Size) = FieldType.GetNameAndSize(type, GetNativeIntegerData(info));
            Offset = GetOffset(info);
            if (type.IsValueType && !type.IsPrimitive)
            {
                Children = GetFields(type);
            }
        }

        private static readonly bool[] s_defaultNativeIntegerFlags = new[] { true };

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

        private int UnpaddedSize
            => Children is null ? Size : GetUnpaddedSize(Children);

        internal static int GetUnpaddedSize(Field[] fields)
            => fields.Sum(field => field.UnpaddedSize);

        internal static Field[] GetFields(Type type)
            => type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(info => new Field(info))
                .OrderBy(t => t.Offset)
                .ToArray();

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

    }
}
