using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
#if NET40_OR_GREATER
    [Serializable]
#endif
    public
#if NETCOREAPP3_1_OR_GREATER
        readonly
#endif
        struct TokenizedString
    {
        public string Value { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }
        public TokenSpan[] Tokens { get; private
#if NETCOREAPP3_1_OR_GREATER
                init;
#else
                set;
#endif
        }


        [JsonConstructor]
        public TokenizedString(string value, TokenSpan[] tokens)
        {
            Value = value;
            Tokens = tokens;
        }

        public TokenizedString(string value, Token token)
        {
            Value = value;
            Tokens = new[] { new TokenSpan(token, value.Length) };
        }

        public TokenizedString Append(TokenizedString append)
        {
            TokenSpan[] tokens = new TokenSpan[Tokens.Length + append.Tokens.Length];
            Tokens.CopyTo(tokens.AsSpan());
            append.Tokens.CopyTo(tokens.AsSpan(Tokens.Length));
            return new TokenizedString(Value + append.Value, (TokenSpan[])tokens);
        }
    }
}
