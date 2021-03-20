using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace devsko.LayoutAnalyzer
{
    public class TransformFlags
    {
        private readonly bool[]? _flags;
        private int _index;

        public TransformFlags(bool[]? flags)
        {
            _flags = flags;
        }

        public bool Next
            => _flags is not null && _index < _flags.Length && _flags[_index++];
    }

    public struct FieldType
    {
        public Type Type { get; private init; }
        public TransformFlags TransformFlags { get; private init; }

        public FieldType(Type type, TransformFlags transformFlags)
        {
            Type = type;
            TransformFlags = transformFlags;
        }

        private static readonly MethodInfo s_unsafeSizeOfT = typeof(Unsafe).GetMethod("SizeOf")!;

        public static ConcurrentDictionary<Type, (TokenizedString Name, int Size)> Cache { get; } = new(EnumeratePrimitiveTypes());

        private static IEnumerable<KeyValuePair<Type, (TokenizedString, int)>> EnumeratePrimitiveTypes()
        {
            return EnumerateTypes()
                .Select(t => new KeyValuePair<Type, (TokenizedString, int)>(t.Item1, (new TokenizedString(t.Item2, t.Item3), t.Item4)));

            static IEnumerable<(Type, string, Token, int)> EnumerateTypes()
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

        public static unsafe (TokenizedString TokenString, int Size) GetNameAndSize(Type type, bool[]? transformFlags)
        {
            if (type.IsClass || type.IsPointer || type.IsArray || type == typeof(IntPtr) || type == typeof(UIntPtr))
            {
                return (GetName(), sizeof(IntPtr));
            }

            return Cache.GetOrAdd(type, _ => (GetName(), GetSize()));

            TokenizedString GetName()
            {
                TokenizedStringBuilder builder = new(stackalloc char[300], stackalloc TokenSpan[100]);
                builder.Append(new FieldType(type, new TransformFlags(transformFlags)));

                return builder.ToTokenizedString();
            }

            int GetSize()
                => (int)s_unsafeSizeOfT.MakeGenericMethod(type).Invoke(null, null)!;
        }
    }
}
