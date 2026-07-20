using System.Collections.Generic;

namespace ForkPlus
{
	public static class ArrayDiff
	{
		public static (T[], T[]) Diff<T>([Null] T[] oldItems, T[] newItems, IComparer<T> comparer)
		{
			if (oldItems == null)
			{
				return (new T[0], newItems.CopyArray());
			}
			List<T> list = new List<T>(oldItems.Length);
			List<T> list2 = new List<T>(newItems.Length);
			int i = 0;
			int j = 0;
			while (i < oldItems.Length && j < newItems.Length)
			{
				int num = comparer.Compare(oldItems[i], newItems[j]);
				if (num == 0)
				{
					i++;
					j++;
				}
				else if (num < 0)
				{
					list.Add(oldItems[i]);
					i++;
				}
				else
				{
					list2.Add(newItems[j]);
					j++;
				}
			}
			for (; i < oldItems.Length; i++)
			{
				list.Add(oldItems[i]);
			}
			for (; j < newItems.Length; j++)
			{
				list2.Add(newItems[j]);
			}
			return (list.ToArray(), list2.ToArray());
		}
	}
}
