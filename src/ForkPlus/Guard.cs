using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ForkPlus
{
	public static class Guard
	{
		[Conditional("DEBUG")]
		public static void AssertAreEqual<T>(T[] lhs, T[] rhs, IComparer<T> comparer)
		{
			for (int i = 0; i < lhs.Length; i++)
			{
			}
		}

		[Conditional("DEBUG")]
		public static void AssertAreSorted<TSource>(IEnumerable<TSource> source, Comparison<TSource> comparer)
		{
			IEnumerator<TSource> enumerator = source.GetEnumerator();
			if (enumerator.MoveNext())
			{
				int num = 0;
				TSource y = enumerator.Current;
				while (enumerator.MoveNext())
				{
					TSource current = enumerator.Current;
					comparer(current, y);
					_ = 0;
					y = current;
					num++;
				}
			}
		}

		[Conditional("DEBUG")]
		public static void AssertIsSorted<TSource>(TSource[] source, IComparer<TSource> comparer)
		{
			for (int i = 0; i < source.Length - 1; i++)
			{
				TSource y = source[i];
				TSource x = source[i + 1];
				comparer.Compare(x, y);
				_ = 0;
			}
		}

		[Conditional("DEBUG")]
		[DebuggerStepThrough]
		public static void AssertIsMainThread()
		{
			if (Thread.CurrentThread.IsBackground)
			{
				throw new InvalidOperationException("Invalid thread");
			}
		}

		[Conditional("DEBUG")]
		[DebuggerStepThrough]
		public static void AssertIsBackgroundThread()
		{
			if (!Thread.CurrentThread.IsBackground)
			{
				throw new InvalidOperationException("Invalid thread");
			}
		}

		[Conditional("DEBUG")]
		[DebuggerStepThrough]
		public static void Assert(bool condition)
		{
		}

		[Conditional("DEBUG")]
		[DebuggerStepThrough]
		public static void Assert(bool condition, string message)
		{
		}

		[Conditional("DEBUG")]
		[DebuggerStepThrough]
		public static void Bug(string message = "Can not reach here")
		{
		}
	}
}
