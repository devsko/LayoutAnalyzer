using System.Collections.Generic;

namespace devsko.LayoutAnalyzer
{
#if NET40_OR_GREATER
    [System.Serializable]
#endif
    public sealed class Padding : FieldBase
    {
        private Padding()
        { }

        internal static IEnumerable<(FieldBase Field, int TotalOffset, int Level)> EnumerateWithPaddings(Field[]? fields, int size, int startOffset, int level, bool recursive)
        {
            if (fields is null)
            {
                yield break;
            }

            int offset = startOffset;
            int padding;
            foreach (Field field in fields)
            {
                padding = field.Offset - offset + startOffset;
                if (padding > 0)
                {
                    yield return (new Padding { Offset = offset - startOffset, Size = padding }, offset, level);
                    offset += padding;
                }

                yield return (field, offset, level);

                if (recursive && field.Children is not null)
                {
                    foreach ((FieldBase, int, int) child in EnumerateWithPaddings(field.Children, field.Size, offset, level + 1, true))
                    {
                        yield return child;
                    }
                }

                offset += field.Size;
            }

            padding = size - offset + startOffset;
            if (padding > 0)
            {
                yield return (new Padding { Offset = offset - startOffset, Size = padding }, offset, level);
            }
        }
    }
}
