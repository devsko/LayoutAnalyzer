using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
    public enum Token : ushort
    {
        Namespace = 0,
        StructRef = 1,
        ClassRef = 2,
        Keyword = 3,
        Identifier = 4,
        Symbol = 5,
    }

    public struct TokenSpan
    {
        private readonly ushort _value;

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
