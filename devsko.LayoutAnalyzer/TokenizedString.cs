using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace devsko.LayoutAnalyzer
{
    public readonly struct TokenizedString
    {
        public string Value { get; private init; }
        public TokenSpan[] Tokens { get; private init; }

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
    }
}
