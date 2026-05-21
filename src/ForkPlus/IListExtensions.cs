using System;
using System.Collections;
using System.Collections.Generic;

namespace ForkPlus
{
	public static class IListExtensions
	{
		public static bool AnyItem<TSource>(this IList<TSource> source, Func<TSource, bool> selector)
		{
			for (int i = 0; i < source.Count; i++)
			{
				if (selector(source[i]))
				{
					return true;
				}
			}
			return false;
		}

		public static int IndexOf<TSource>(this IList<TSource> source, Func<TSource, bool> predicate)
		{
			int num = 0;
			for (int i = 0; i < source.Count; i++)
			{
				if (predicate(source[i]))
				{
					return num;
				}
				num++;
			}
			return -1;
		}

		public static bool ContainsItem<T>(this IList<T> source, Func<T, bool> selector)
		{
			for (int i = 0; i < source.Count; i++)
			{
				if (selector(source[i]))
				{
					return true;
				}
			}
			return false;
		}

		public static T UnstableRemove<T>(this List<T> source, Func<T, bool> predicate) where T : class
		{
			int num = source.IndexOf(predicate);
			if (num == -1)
			{
				return null;
			}
			return source.UnstableRemoveAt(num);
		}

		public static T? UnstableRemoveStruct<T>(this List<T> source, Func<T, bool> predicate) where T : struct
		{
			int num = source.IndexOf(predicate);
			if (num == -1)
			{
				return null;
			}
			return source.UnstableRemoveAt(num);
		}

		public static T UnstableRemoveAt<T>(this List<T> source, int index)
		{
			T result = source[index];
			int num = source.Count - 1;
			if (index != num)
			{
				source[index] = source[num];
			}
			source.RemoveAt(num);
			return result;
		}

		public static TSource FirstItem<TSource>(this IList source)
		{
			if (source.Count > 0)
			{
				object obj = source[0];
				if (obj is TSource)
				{
					return (TSource)obj;
				}
			}
			return default(TSource);
		}

		[Null]
		public static TSource FirstItem<TSource>(this IList source, Func<TSource, bool> predicate) where TSource : class
		{
			foreach (TSource item in source)
			{
				if (predicate(item))
				{
					return item;
				}
			}
			return null;
		}

		public static TResult[] CompactMap<TResult>(this IList source, Func<object, TResult> selector)
		{
			List<TResult> list = new List<TResult>(source.Count);
			for (int i = 0; i < source.Count; i++)
			{
				TResult val = selector(source[i]);
				if (val != null)
				{
					list.Add(val);
				}
			}
			return list.ToArray();
		}
	}
}
