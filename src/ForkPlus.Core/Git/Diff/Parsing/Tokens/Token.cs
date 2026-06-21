namespace ForkPlus.Git.Diff.Parsing.Tokens
{
    public struct Token
    {
        public TokenType Type { get; }

        public Range Range { get; }

        public Token(TokenType type, Range range)
        {
            Type = type;
            Range = range;
        }
    }
}
