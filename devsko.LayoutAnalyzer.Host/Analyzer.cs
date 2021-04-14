using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace devsko.LayoutAnalyzer.Host
{
    public sealed class Analyzer
    {
        private static readonly MethodInfo s_unsafeSizeOfT = typeof(Unsafe).GetMethod("SizeOf")!;

        private readonly Dictionary<Type, (TokenizedString Name, int Size)> _cache;

        public Analyzer()
        {
            _cache = EnumeratePrimitiveTypes()
                .ToDictionary(t => t.Type, t => (new TokenizedString(t.Name, t.Token), t.Size));
        }

        public Layout? Analyze(Type type)
        {
            var layoutAttr = type.StructLayoutAttribute;
            if (layoutAttr is null)
            {
                return null;
            }
            Field[] fields = GetFields(type);
            (TokenizedString typeName, int size) = GetNameAndSize(type);

            return new Layout(type, typeName, size, fields, GetUnpaddedSize(fields));
        }

        public unsafe (TokenizedString Name, int Size) GetNameAndSize(Type type)
        {
            (TokenizedString name, int size) = GetNameAndSize(type, null);

            if (!type.IsValueType)
            {
                // For class types we need the actual heap size, not just the size of a pointer.
                // TypeHandle points to a MethodTable structure (https://github.com/dotnet/runtime/blob/102d1e856c7e0e553abeec937783da5debed73ad/src/coreclr/vm/methodtable.h#L610)
                // The size on the heap is found in m_BaseSize (https://github.com/dotnet/runtime/blob/102d1e856c7e0e553abeec937783da5debed73ad/src/coreclr/vm/methodtable.h#L3743)

                int dword =
                    Unsafe.Add(
                        ref Unsafe.AsRef<int>(
                            type.TypeHandle.Value.ToPointer()),
                        1);

                // Subtract the size of the object header and method table pointer

                size = dword - 2 * sizeof(IntPtr);
            }

            return (name, size);
        }

        public unsafe (TokenizedString TokenString, int Size) GetNameAndSize(Type type, bool[]? transformFlags)
        {
            // Don't cache native integers. They have different names depending on transformFlags
            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            {
                return (GetName(), sizeof(IntPtr));
            }

            if (!_cache.TryGetValue(type, out (TokenizedString Name, int Size) entry))
            {
                _cache.Add(type, entry = (GetName(), GetSize()));
            }

            return entry;

            TokenizedString GetName()
            {
                TokenizedStringBuilder builder = new(stackalloc char[300], stackalloc TokenSpan[100], this);
                builder.Append(new FieldType(type, new TransformFlags(transformFlags)));

                return builder.ToTokenizedString();
            }

            int GetSize()
            {
                if (type.IsClass || type.IsPointer || type.IsArray)
                {
                    return sizeof(IntPtr);
                }

                return (int)s_unsafeSizeOfT.MakeGenericMethod(type).Invoke(null, null)!;
            }
        }

        public bool TryGetName(Type type, [MaybeNullWhen(false)] out TokenizedString name)
        {
            if (_cache.TryGetValue(type, out (TokenizedString Name, int _) value))
            {
                name = value.Name;
                return true;
            }
            name = default;
            return false;
        }

        internal Field[] GetFields(Type type)
            => type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(info =>
                {
                    Type type = info.FieldType;
                    Field[]? children = type.IsValueType && !type.IsPrimitive ? GetFields(type) : null;
                    (TokenizedString typeName, int size) = GetNameAndSize(type, GetNativeIntegerData(info));
                    return new Field(info, typeName, size, GetOffset(info), children);
                })
                .OrderBy(t => t.Offset)
                .ToArray();

        internal static int GetUnpaddedSize(Field[] fields)
            => fields.Sum(field
                => field.Children is null
                    ? field.Size
                    : Math.Min(field.Size, GetUnpaddedSize(field.Children)));

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
            if (data.ConstructorArguments.Count < 1 || data.ConstructorArguments[0].Value is null)
            {
                return s_defaultNativeIntegerFlags;
            }

            return ((IReadOnlyCollection<CustomAttributeTypedArgument>)data.ConstructorArguments[0].Value!)
                .Select(arg => (bool)arg.Value!)
                .ToArray();
        }

        private static IEnumerable<(Type Type, string Name, Token Token, int Size)> EnumeratePrimitiveTypes()
        {
            int ptrSize = IntPtr.Size;

            yield return (typeof(void), "void", Token.Keyword, 0);
            yield return (typeof(int), "int", Token.Keyword, sizeof(int));
            yield return (typeof(uint), "uint", Token.Keyword, sizeof(uint));
            yield return (typeof(byte), "byte", Token.Keyword, sizeof(byte));
            yield return (typeof(sbyte), "sbyte", Token.Keyword, sizeof(sbyte));
            yield return (typeof(long), "long", Token.Keyword, sizeof(long));
            yield return (typeof(ulong), "ulong", Token.Keyword, sizeof(ulong));
            yield return (typeof(short), "short", Token.Keyword, sizeof(short));
            yield return (typeof(ushort), "ushort", Token.Keyword, sizeof(ushort));
            yield return (typeof(char), "char", Token.Keyword, sizeof(char));
            yield return (typeof(bool), "bool", Token.Keyword, sizeof(bool));
            yield return (typeof(float), "float", Token.Keyword, sizeof(float));
            yield return (typeof(double), "double", Token.Keyword, sizeof(double));
            yield return (typeof(decimal), "decimal", Token.Keyword, sizeof(decimal));
            yield return (typeof(DateTime), nameof(DateTime), Token.Struct, Unsafe.SizeOf<DateTime>());
            yield return (typeof(string), "string", Token.Keyword, ptrSize);
            yield return (typeof(object), "object", Token.Keyword, ptrSize);
        }
    }
}
