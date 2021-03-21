using System;
using System.Collections.Generic;
using devsko.LayoutAnalyzer;

namespace devsko.LayoutAnalyzer
{
    public ref struct TokenizedStringBuilder
    {
        private SpanBuilder<char> _chars;
        private SpanBuilder<TokenSpan> _tokens;
        private readonly Analyzer _analyzer;

        public TokenizedStringBuilder(Span<char> chars, Span<TokenSpan> tokens, Analyzer analyzer)
        {
            _chars = new SpanBuilder<char>(chars);
            _tokens = new SpanBuilder<TokenSpan>(tokens);
            _analyzer = analyzer;
        }

        public TokenizedString ToTokenizedString()
        {
            return new TokenizedString(_chars.ToString(), _tokens.ToArray());
        }

        public void Append(FieldType field)
        {
            //field.Type switch
            //{
            //    { IsNested: true } => AppendNested(field),
            //    { IsPointer: true } => AppendPointer(field),
            //    { IsArray: true } => AppendArray(field),
            //    { IsGenericType: true } => AppendGeneric(field),
            //    _ => AppendPrimitive(field)
            //};

            if (field.Type.IsNested)
            {
                AppendNested(field);
            }
            else if (field.Type.IsPointer)
            {
                AppendPointer(field);
            }
            else if (field.Type.IsArray)
            {
                AppendArray(field);
            }
            else if (field.Type.IsGenericType)
            {
                AppendGeneric(field);
            }
            else
            {
                AppendPrimitive(field);
            }
        }

        private void Append(char value, Token token)
        {
            _chars.Append(value);
            _tokens.Append(new TokenSpan(token, 1));
        }

        private void Append(ReadOnlySpan<char> value, Token token)
        {
            _chars.Append(value);
            _tokens.Append(new TokenSpan(token, value.Length));
        }

        private void AppendPrimitive(FieldType field)
        {
            if (_analyzer.TryGetName(field.Type, out TokenizedString? name))
            {
                _chars.Append(name.Value.AsSpan());
                _tokens.Append(name.Tokens.AsSpan());
            }
            else
            {
                AppendPlain(field);
            }
        }

        private void AppendPlain(FieldType field, bool sliceApostrophe = false, bool omitNamespace = false)
        {
            ReadOnlySpan<char> ns = field.Type.Namespace.AsSpan();

            if (
                !omitNamespace &&
                ns.Length > 0 && (
                    !ns.StartsWith("System".AsSpan()) || (
                        ns.Length >= 18 &&
                        !ns.Slice(6, 12).Equals(".Collections".AsSpan(), StringComparison.Ordinal))))
            {
                _chars.Append(ns);
                int pos;
                while ((pos = ns.IndexOf('.')) >= 0)
                {
                    _tokens.Append(new TokenSpan(Token.Namespace, pos));
                    _tokens.Append(new TokenSpan(Token.Symbol, 1));
                    ns = ns[(pos + 1)..];
                }
                _tokens.Append(new TokenSpan(Token.Namespace, ns.Length));
                Append('.', Token.Symbol);
            }

            ReadOnlySpan<char> name = field.Type.Name.AsSpan();
            Token token = field.Type.IsValueType ? Token.StructRef : Token.ClassRef;
            if (sliceApostrophe)
            {
                Append(name.Slice(0, name.IndexOf('`')), token);
            }
            else
            {
                if (name.EndsWith("IntPtr".AsSpan(), StringComparison.Ordinal) && field.TransformFlags.Next)
                {
                    Append((name[0] == 'U' ? "unint" : "nint").AsSpan(), Token.Keyword);
                }
                else
                {
                    Append(name,  token);
                }
            }
        }

        private void AppendNested(FieldType field)
        {
            Append(new FieldType(field.Type.DeclaringType, field.TransformFlags));
            Append('.', Token.Symbol);
            AppendPlain(field, omitNamespace: true);
        }

        private void AppendPointer(FieldType field)
        {
            Append(new FieldType(field.Type.GetElementType(), field.TransformFlags));
            Append('*', Token.Symbol);
        }

        private void AppendArray(FieldType field)
        {
            Type type = field.Type;
            int firstRank = type.GetArrayRank();
            List<int>? ranks = null;

            while (true)
            {
                type = type.GetElementType();
                if (!type.IsArray)
                {
                    break;
                }
                ranks ??= new List<int>();
                ranks.Add(type.GetArrayRank());
            }

            Append(new FieldType(type, field.TransformFlags));
            AppendArrayRank(firstRank);

            if (ranks is not null)
            {
                foreach (int rank in ranks)
                {
                    AppendArrayRank(rank);
                }
            }
        }

        void AppendArrayRank(int rank)
        {
            _chars.Append('[');
            _chars.Append(',', rank - 1);
            _chars.Append(']');
            _tokens.Append(new TokenSpan(Token.Symbol, rank - 1 + 2));
        }

        private void AppendGeneric(FieldType field)
        {
            Type genericType = field.Type.GetGenericTypeDefinition();
            Type[] arguments = field.Type.GetGenericArguments();


            if (genericType == typeof(Nullable<>))
            {
                Append(new FieldType(arguments[0], field.TransformFlags));
                Append('?', Token.Symbol);
            }
            else
            {
                AppendPlain(new FieldType(genericType, field.TransformFlags), true);
                Append('<', Token.Symbol);

                for (int i = 0; i < arguments.Length; i++)
                {
                    if (i > 0)
                    {
                        _chars.Append(',');
                        _chars.Append(' ');
                        _tokens.Append(new TokenSpan(Token.Symbol, 2));
                    }
                    Append(new FieldType(arguments[i], field.TransformFlags));
                }

                Append('>', Token.Symbol);
            }
        }
    }
}
