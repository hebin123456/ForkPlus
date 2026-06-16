using System;

namespace ForkPlus.Git
{
    public struct ShaBufferIterator
    {
        public struct Enumerator
        {
            private Sha[] _items;
            private int _cursor;
            private int _end;

            public Sha Current => _items[_cursor];

            public Enumerator(Sha[] items, int start, int end)
            {
                _items = items;
                _cursor = start - 1;
                _end = end;
            }

            public bool MoveNext()
            {
                if (++_cursor < _end)
                {
                    return true;
                }
                return false;
            }
        }

        public readonly Sha[] Items;
        public readonly int Start;
        public readonly int End;

        public int Length => End - Start;

        public ShaBufferIterator(Sha[] items, int start, int end)
        {
            Items = items;
            Start = start;
            End = end;
        }

        public Sha[] ToArray()
        {
            Sha[] array = new Sha[Length];
            Array.Copy(Items, Start, array, 0, Length);
            return array;
        }

        public Sha? Nth(int index)
        {
            if (index >= Length)
            {
                return null;
            }
            return Items[Start + index];
        }

        public ShaBufferIterator FirstOnly()
        {
            return new ShaBufferIterator(Items, Start, Math.Min(Start + 1, End));
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(Items, Start, End);
        }
    }
}
