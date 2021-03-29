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
        public string Name { get; private
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
        public TokenizedString TypeName { get; private
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
            // FieldHandle points to a FieldDesc structure defined in (https://github.com/dotnet/runtime/blob/102d1e856c7e0e553abeec937783da5debed73ad/src/coreclr/vm/field.h#L34)
            // The offset of the field is found in m_dwOffset (https://github.com/dotnet/runtime/blob/102d1e856c7e0e553abeec937783da5debed73ad/src/coreclr/vm/field.h#L74)

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
            if (data.ConstructorArguments.Count < 1 || data.ConstructorArguments[0].Value is null)
            {
                return s_defaultNativeIntegerFlags;
            }

            return ((IReadOnlyCollection<CustomAttributeTypedArgument>)data.ConstructorArguments[0].Value!)
                .Select(arg => (bool)arg.Value!)
                .ToArray();
        }
    }
}
