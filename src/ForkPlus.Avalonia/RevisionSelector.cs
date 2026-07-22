using System.Collections.Generic;
using ForkPlus.Git;

// Avalonia spike 版 RevisionSelector（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/RevisionSelector.cs（26 行）：
//   - WPF: public class RevisionSelector
//   - 嵌套 Head（空标记类）/ Sha（持有 IReadOnlyList<ForkPlus.Git.Sha>）
//   - Sha 提供集合构造与单值构造（单值包成 1 元素数组）
//   - 无 WPF 依赖，纯 POCO
//
// Avalonia 版差异：
//   1. 无 WPF 依赖，零改动复用
//   2. 命名空间 ForkPlus.UI → ForkPlus.Avalonia
//   3. ForkPlus.Git.Sha 来自 ForkPlus.Core（本工程已 ProjectReference 引用）
//
// spike 简化（task spec：简化为 POCO）：与 WPF 一致的 Head/Sha 嵌套类结构。
namespace ForkPlus.Avalonia
{
	public class RevisionSelector
	{
		public class Head : RevisionSelector
		{
		}

		public class Sha : RevisionSelector
		{
			public IReadOnlyList<ForkPlus.Git.Sha> Shas { get; }

			public Sha(IReadOnlyList<ForkPlus.Git.Sha> shas)
			{
				Shas = shas;
			}

			public Sha(ForkPlus.Git.Sha sha)
				: this(new ForkPlus.Git.Sha[1] { sha })
			{
			}
		}
	}
}
