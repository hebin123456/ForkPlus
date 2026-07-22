using System;
using System.Collections.Generic;

namespace ForkPlus.Avalonia.Controls.Flattener
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Flattener/TreeTraversal.cs（45 行）：
    //   - internal static class TreeTraversal
    //   - PreOrder<T>(T root, Func<T, IEnumerable<T>> recursion)：从单个根节点前序遍历
    //   - PreOrder<T>(IEnumerable<T> input, Func<T, IEnumerable<T>> recursion)：从节点集合前序遍历
    //   - 用 Stack<IEnumerator<T>> 实现深度优先前序遍历（非递归，避免栈溢出）
    //
    // Avalonia 版差异：
    //   1. 纯 C# 静态工具类，无 WPF 依赖，零改动迁移
    //   2. namespace 改为 ForkPlus.Avalonia.Controls.Flattener
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
