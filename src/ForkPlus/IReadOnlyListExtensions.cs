using System;
using System.Collections.Generic;

namespace ForkPlus
{
	public static class IReadOnlyListExtensions
	{
		public static int BinarySearchBy<T>(this IReadOnlyList<T> source, Func<T, int> f)
		{
			int num = 0;
			int num2 = source.Count;
			while (num < num2)
			{
				int num3 = (num + num2) / 2;
				// Migration note：原 switch 只匹配 -1/0/1，比较器返回其他整数（如
				// string.Compare/char.CompareTo 的任意差值）时三分支全部落空，
				// num/num2 不再更新 → 死循环（UI 线程 100% CPU 卡死实证）。
				// 按二分语义归一：负数 → 目标在右半区，正数 → 左半区，0 → 命中。
				int num4 = f(source[num3]);
				if (num4 < 0)
				{
					num = num3 + 1;
				}
				else if (num4 > 0)
				{
					num2 = num3;
				}
				else
				{
					return num3;
				}
			}
			return ~num;
		}

		public static IReadOnlyList<TResult> CompactMapStruct<TSource, TResult>(this IReadOnlyList<TSource> source, Func<TSource, TResult?> selector) where TResult : struct
		{
			List<TResult> list = new List<TResult>(source.Count);
			for (int i = 0; i < source.Count; i++)
			{
				TResult? val = selector(source[i]);
				if (val.HasValue)
				{
					list.Add(val.Value);
				}
			}
			return list;
		}

		public static string Joined(this IReadOnlyList<string> source, string separator)
		{
			return string.Join(separator, source);
		}

		public static List<TSource> Filter<TSource>(this IReadOnlyList<TSource> source, Func<TSource, bool> predicate)
		{
			List<TSource> list = new List<TSource>(source.Count);
			for (int i = 0; i < source.Count; i++)
			{
				if (predicate(source[i]))
				{
					list.Add(source[i]);
				}
			}
			return list;
		}

		[Null]
		public static TSource FirstItem<TSource>(this IReadOnlyList<TSource> source) where TSource : class
		{
			if (source.Count > 0)
			{
				return source[0];
			}
			return null;
		}

		[Null]
		public static TSource FirstItem<TSource>(this IReadOnlyList<TSource> source, Func<TSource, bool> predicate) where TSource : class
		{
			for (int i = 0; i < source.Count; i++)
			{
				if (predicate(source[i]))
				{
					return source[i];
				}
			}
			return null;
		}

		public static TSource? FirstItemStruct<TSource>(this IReadOnlyList<TSource> source) where TSource : struct
		{
			if (source.Count > 0)
			{
				return source[0];
			}
			return null;
		}

		public static TSource? FirstItemStruct<TSource>(this IReadOnlyList<TSource> source, Func<TSource, bool> predicate) where TSource : struct
		{
			for (int i = 0; i < source.Count; i++)
			{
				if (predicate(source[i]))
				{
					return source[i];
				}
			}
			return null;
		}

		public static TResult[] Map<TInput, TResult>(this IReadOnlyList<TInput> source, Func<TInput, TResult> selector)
		{
			TResult[] array = new TResult[source.Count];
			for (int i = 0; i < source.Count; i++)
			{
				array[i] = selector(source[i]);
			}
			return array;
		}

		public static T[] Reversed<T>(this IReadOnlyList<T> source)
		{
			T[] array = new T[source.Count];
			for (int i = 0; i < source.Count; i++)
			{
				array[source.Count - i - 1] = source[i];
			}
			return array;
		}

		public static TSource[] SkipFirst<TSource>(this TSource[] source, int skip)
		{
			int num = Math.Min(skip, source.Length);
			int num2 = source.Length;
			TSource[] array = new TSource[num2 - num];
			int num3 = 0;
			int num4 = num;
			while (num4 < num2)
			{
				array[num3] = source[num4];
				num4++;
				num3++;
			}
			return array;
		}

		public static IEnumerable<(T1, T2)> Zip<T1, T2>(this IReadOnlyList<T1> target, IReadOnlyList<T2> other)
		{
			int count = Math.Min(target.Count, other.Count);
			for (int i = 0; i < count; i++)
			{
				yield return (target[i], other[i]);
			}
		}
	}
}
