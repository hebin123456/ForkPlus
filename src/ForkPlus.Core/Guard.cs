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
        }

        [Conditional("DEBUG")]
        public static void AssertAreSorted<TSource>(IEnumerable<TSource> source, Comparison<TSource> comparer)
        {
        }

        [Conditional("DEBUG")]
        public static void AssertIsSorted<TSource>(TSource[] source, IComparer<TSource> comparer)
        {
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
