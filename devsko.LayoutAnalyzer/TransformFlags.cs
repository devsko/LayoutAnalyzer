namespace devsko.LayoutAnalyzer
{
    public sealed class TransformFlags
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
}
