using System;
using System.Collections.Generic;

namespace ForkPlus.UI.Controls.Flattener
{
	internal static class TreeTraversal
	{
		public static IEnumerable<T> PreOrder<T>(T root, Func<T, IEnumerable<T>> recursion)
		{
			return PreOrder(new[] { root }, recursion);
		}

		public static IEnumerable<T> PreOrder<T>(IEnumerable<T> input, Func<T, IEnumerable<T>> recursion)
		{
			Stack<IEnumerator<T>> stack = new Stack<IEnumerator<T>>();
			try
			{
				stack.Push(input.GetEnumerator());
				while (stack.Count > 0)
				{
					IEnumerator<T> enumerator = stack.Peek();
					if (!enumerator.MoveNext())
					{
						stack.Pop().Dispose();
						continue;
					}
					T current = enumerator.Current;
					yield return current;
					IEnumerable<T> children = recursion(current);
					if (children != null)
					{
						stack.Push(children.GetEnumerator());
					}
				}
			}
			finally
			{
				while (stack.Count > 0)
				{
					stack.Pop().Dispose();
				}
			}
		}
	}
}
