using System;
using System.Collections.Generic;

namespace devsko.LayoutAnalyzer
{
#if NET40_OR_GREATER
    [Serializable]
#endif
    public sealed class Padding : FieldBase
    {
        private Padding()
        { }

        internal static IEnumerable<(FieldBase Field, int TotalOffset, int Level)> EnumerateWithPaddings(Field[] fields, int size, int startOffset, int level)
        {
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

                if (field.Children is not null)
                {
                    foreach ((FieldBase, int, int) child in EnumerateWithPaddings(field.Children, field.Size, offset, level + 1))
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
