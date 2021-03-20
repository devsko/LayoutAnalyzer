namespace devsko.LayoutAnalyzer
{
    public abstract class FieldBase
    {
        public int Offset { get; protected init; }
        public int Size { get; protected init; }
    }
}
