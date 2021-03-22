using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
    public readonly struct TokenSpan
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
