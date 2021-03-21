using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace devsko.LayoutAnalyzer
{
    public class Analyzer
    {
        private static readonly MethodInfo s_unsafeSizeOfT = typeof(Unsafe).GetMethod("SizeOf")!;

        public Dictionary<Type, (TokenizedString Name, int Size)> Cache { get; }

        public Analyzer()
        {
            Cache = EnumeratePrimitiveTypes()
                .ToDictionary(t => t.Type, t => (new TokenizedString(t.Name, t.Token), t.Size));
        }

        public Layout? Analyze(Type type)
        {
            var layoutAttr = type.StructLayoutAttribute;
            if (layoutAttr is null)
            {
                return null;
            }

            return new Layout(type, this);
        }

        public unsafe (TokenizedString Name, int Size) GetNameAndSize(Type type)
        {
            (TokenizedString name, int size) = GetNameAndSize(type, null);

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

        public unsafe (TokenizedString TokenString, int Size) GetNameAndSize(Type type, bool[]? transformFlags)
        {
            // Don't cache native integers. They have different names depending on transformFlags
            if (type == typeof(IntPtr) || type == typeof(UIntPtr))
            {
                return (GetName(), sizeof(IntPtr));
            }

            if (!Cache.TryGetValue(type, out (TokenizedString Name, int Size) entry))
            {
                Cache.Add(type, entry = (GetName(), GetSize()));
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
            if (Cache.TryGetValue(type, out (TokenizedString Name, int _) value))
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
                .Select(info => new Field(info, this))
                .OrderBy(t => t.Offset)
                .ToArray();

        internal int GetUnpaddedSize(Field[] fields)
            => fields.Sum(field => field.Children is null ? field.Size : GetUnpaddedSize(field.Children));

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
            yield return (typeof(DateTime), nameof(DateTime), Token.StructRef, Unsafe.SizeOf<DateTime>());
            yield return (typeof(string), "string", Token.Keyword, ptrSize);
            yield return (typeof(object), "object", Token.Keyword, ptrSize);
        }
    }
}
