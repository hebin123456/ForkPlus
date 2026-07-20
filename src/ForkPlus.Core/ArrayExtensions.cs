using System;
using System.Collections.Generic;

namespace ForkPlus
{
	public static class ArrayExtensions
	{
		public static TSource[] CopyArray<TSource>(this TSource[] source)
		{
			TSource[] array = new TSource[source.Length];
			Array.Copy(source, array, source.Length);
			return array;
		}

		public static int? IndexOfItem<TSource>(this TSource[] source, Func<TSource, bool> predicate)
		{
			int num = 0;
			for (int i = 0; i < source.Length; i++)
			{
				if (predicate(source[i]))
				{
					return num;
				}
				num++;
			}
			return null;
		}

		public static bool ContainsItem<TSource>(this TSource[] source, TSource value)
		{
			for (int i = 0; i < source.Length; i++)
			{
				if (source[i].Equals(value))
				{
					return true;
				}
			}
			return false;
		}

		public static bool ContainsItem<TSource>(this TSource[] source, Func<TSource, bool> predicate)
		{
			for (int i = 0; i < source.Length; i++)
			{
				if (predicate(source[i]))
				{
					return true;
				}
			}
			return false;
		}

		[Null]
		public static TSource FirstItem<TSource, TArg>(this TSource[] source, TArg arg1, Func<TSource, TArg, bool> predicate) where TSource : class
		{
			for (int i = 0; i < source.Length; i++)
			{
				if (predicate(source[i], arg1))
				{
					return source[i];
				}
			}
			return null;
		}

		public static Dictionary<Key, int[]> GroupIndexes<T, Key>(this T[] source, Func<T, Key> keyForValue) where Key : IComparable<Key>, IEquatable<Key>
		{
			int[] array = new int[source.Length];
			for (int i = 0; i < source.Length; i++)
			{
				array[i] = i;
			}
			Array.Sort(array, (int x, int y) => keyForValue(source[x]).CompareTo(keyForValue(source[y])));
			Dictionary<Key, int[]> dictionary = new Dictionary<Key, int[]>();
			int num = 0;
			for (int j = 0; j < array.Length; j++)
			{
				Key key = keyForValue(source[array[j]]);
				if (j + 1 < array.Length)
				{
					if (EqualityComparer<Key>.Default.Equals(key, keyForValue(source[array[j + 1]])))
					{
						continue;
					}
				}
				int num2 = j + 1 - num;
				int[] array2 = new int[num2];
				for (int k = 0; k < num2; k++)
				{
					array2[k] = array[num + k];
				}
				dictionary[key] = array2;
				num = j + 1;
			}
			return dictionary;
		}

		public static TSource SingleItem<TSource>(this TSource[] source) where TSource : class
		{
			if (source.Length == 1)
			{
				return source[0];
			}
			return null;
		}

		public static TSource LastItem<TSource>(this TSource[] source) where TSource : class
		{
			if (source.Length != 0)
			{
				return source[source.Length - 1];
			}
			return null;
		}

		public static TResult[] CompactMap<TSource, TResult>(this TSource[] source, Func<TSource, TResult> selector) where TSource : class
		{
			List<TResult> list = new List<TResult>(source.Length);
			for (int i = 0; i < source.Length; i++)
			{
				TResult val = selector(source[i]);
				if (val != null)
				{
					list.Add(val);
				}
			}
			return list.ToArray();
		}

		public static bool AnyItem<TSource>(this TSource[] source, Func<TSource, bool> selector)
		{
			for (int i = 0; i < source.Length; i++)
			{
				if (selector(source[i]))
				{
					return true;
				}
			}
			return false;
		}

		public static bool AllItems<TSource>(this TSource[] source, Func<TSource, bool> condition)
		{
			for (int i = 0; i < source.Length; i++)
			{
				if (!condition(source[i]))
				{
					return false;
				}
			}
			return true;
		}

		public static TSource[] Subsequence<TSource>(this TSource[] source, int skip, int take)
		{
			int num = Math.Min(skip + take, source.Length);
			if (skip >= source.Length)
			{
				return new TSource[0];
			}
			TSource[] array = new TSource[num - skip];
			int num2 = 0;
			int num3 = skip;
			while (num3 < num)
			{
				array[num2] = source[num3];
				num3++;
				num2++;
			}
			return array;
		}

		public static TSource[] ToSortedArray<TSource>(this TSource[] source, Comparison<TSource> compare)
		{
			TSource[] array = source.CopyArray();
			Array.Sort(array, compare);
			return array;
		}

		public static TSource[] ToSortedArray<TSource>(this TSource[] source, IComparer<TSource> compare)
		{
			TSource[] array = source.CopyArray();
			Array.Sort(array, compare);
			return array;
		}

		public static int[] CreateIndex<TSource>(this TSource[] source, Comparison<TSource> comparison)
		{
			int[] array = new int[source.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = i;
			}
			Array.Sort(array, (int x, int y) => comparison(source[x], source[y]));
			return array;
		}
	}
}
