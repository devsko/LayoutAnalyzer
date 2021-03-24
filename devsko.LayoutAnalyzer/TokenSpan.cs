using System;
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
        struct TokenSpan
    {
        private
#if NETCOREAPP3_1_OR_GREATER
        readonly
#endif
            ushort _value;

        [JsonConstructor]
        public TokenSpan(Token token, int length)
        {
            _value = (ushort)((ushort)token | (ushort)(length << 3));
        }

        public Token Token 
            => (Token)((ushort)_value & 0x7);

        public int Length
            => _value >> 3;
    }
}
