using System;
using System.Collections.Generic;

namespace ForkPlus
{
    public static class RangeExtensions
    {
        public static bool Contains(this Range range, int value)
        {
            if (range.Start <= value)
            {
                return value < range.End;
            }
            return false;
        }

        public static bool Overlaps(this Range range, Range other)
        {
            if (range.Start < other.End)
            {
                return other.Start < range.End;
            }
            return false;
        }

        public static TResult[] Map<TResult>(this Range source, Func<int, TResult> selector)
        {
            TResult[] array = new TResult[source.Length];
            for (int i = source.Start; i < source.End; i++)
            {
                array[i - source.Start] = selector(i);
            }
            return array;
        }

        public static void Merge(this Range fullRange, List<Range>[] ranges, Action<Range, int?, int?, int?> callback)
        {
            Range range = fullRange;
            int[] array = ranges.Map((List<Range> x) => 0);
            while (!range.IsEmpty)
            {
                Range range2 = range;
                for (int i = 0; i < ranges.Length; i++)
                {
                    List<Range> list = ranges[i];
                    if (array[i] < list.Count)
                    {
                        Range range3 = list[array[i]];
                        if (range2.Start >= range3.End)
                        {
                            array[i]++;
                        }
                    }
                    if (array[i] < list.Count)
                    {
                        Range range4 = list[array[i]];
                        range2 = ((range4.Start <= range2.Start) ? new Range(range2.Start, Math.Min(range4.End, range2.End)) : new Range(range2.Start, Math.Min(range4.Start, range2.End)));
                    }
                }
                int? arg = null;
                int? arg2 = null;
                int? arg3 = null;
                for (int j = 0; j < ranges.Length; j++)
                {
                    int? num = null;
                    List<Range> list2 = ranges[j];
                    if (array[j] < list2.Count && list2[array[j]].Overlaps(range2))
                    {
                        num = array[j];
                    }
                    switch (j)
                    {
                    case 0:
                        arg = num;
                        break;
                    case 1:
                        arg2 = num;
                        break;
                    case 2:
                        arg3 = num;
                        break;
                    }
                }
                callback(range2, arg, arg2, arg3);
                range = new Range(range2.End, range.End);
            }
        }
    }
}
