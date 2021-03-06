using System;
using System.Collections.Generic;
using System.Linq;
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
        public Token Kind
        {
            get; private
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
        public string Runtime { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public string AssemblyName
        {
            get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public string AssemblyPath { get; set; }
        public TimeSpan ElapsedTime { get; set; }

        public Layout(Type type, TokenizedString typeName, int size, Field[] fields, int unpaddedSizeOfFields)
        {
            Fields = fields;
            Name = typeName;
            TotalSize = size;
            TotalPadding = Math.Max(TotalSize - unpaddedSizeOfFields, 0);
            Kind = GetKind(type);
            StructLayoutAttribute? layoutAttr = type.StructLayoutAttribute!;
            AttributeKind = layoutAttr.Value;
            AttributeSize = layoutAttr.Size;
            AttributePack = layoutAttr.Pack;
            Runtime = $"{RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})";
            AssemblyName = type.Assembly.FullName ?? string.Empty;
            AssemblyPath = type.Assembly.Location;
        }

        [JsonConstructor]
        public Layout(Field[] fields, int totalSize, int totalPadding, TokenizedString name, Token kind, LayoutKind attributeKind, int attributeSize, int attributePack, string runtime, string assemblyName, string assemblyPath)
        {
            Fields = fields;
            TotalSize = totalSize;
            TotalPadding = totalPadding;
            Name = name;
            Kind = kind;
            AttributeKind = attributeKind;
            AttributeSize = attributeSize;
            AttributePack = attributePack;
            Runtime = runtime;
            AssemblyName = assemblyName;
            AssemblyPath = assemblyPath;
        }

        [JsonIgnore]
        public bool IsValueType
            => Kind == Token.Struct || Kind == Token.Enum;

        [JsonIgnore]
        public IEnumerable<FieldBase> FieldsAndPaddings
            => Padding
                .EnumerateWithPaddings(Fields, TotalSize, 0, 0, false)
                .Select(tuple => tuple.Field);

        [JsonIgnore]
        public IEnumerable<(FieldBase Field, int TotalOffset, int Level)> AllFieldsAndPaddings
            => Padding.EnumerateWithPaddings(Fields, TotalSize, 0, 1, true);

        public static Token GetKind(Type type)
            => type switch
            {
                { IsClass: true } when type.BaseType == typeof(MulticastDelegate) => Token.Delegate,
                { IsClass: true } => Token.Class,
                { IsEnum: true } => Token.Enum,
                { IsValueType: true } => Token.Struct,
                { IsInterface: true } => Token.Interface,
                _ => throw new InvalidOperationException("unknown kind.")
            };
    }
}
