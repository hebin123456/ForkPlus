using System;

namespace ForkPlus.Undo
{
	/// <summary>
	/// v3.3.0：Undo/Redo 栈条目。轻量结构，只保存恢复仓库到某状态所需的最小信息。
	///
	/// 设计原则：
	/// - 不再保存完整仓库快照（旧 RepositorySnapshot 有 11 字段，每次抓 7 次 git 进程）
	/// - HEAD sha 是恢复真相源（git reset --hard 即可）
	/// - OperationName 通过 UndoIndexStore 持久化到 .git/forkplus-undo-index.json
	/// - 当前分支名用于 Undo 后切回原分支（避免进入 detached HEAD）
	/// </summary>
	public sealed class UndoEntry
	{
		/// <summary>操作前的 HEAD commit sha（40 字符）。可能为 null（空仓库）。</summary>
		public string HeadSha { get; }

		/// <summary>操作前的当前分支名。可能为 null（detached HEAD 或空仓库）。</summary>
		public string CurrentBranchName { get; }

		/// <summary>UI 友好的操作名，例如 "Commit 'fix: bug'" / "Checkout 'feature/x'"。</summary>
		public string OperationName { get; }

		/// <summary>操作前抓取时间（UTC）。</summary>
		public DateTime TimestampUtc { get; }

		public UndoEntry(string headSha, string currentBranchName, string operationName, DateTime timestampUtc)
		{
			HeadSha = headSha;
			CurrentBranchName = currentBranchName;
			OperationName = operationName ?? "";
			TimestampUtc = timestampUtc;
		}

		/// <summary>创建一个副本，仅替换 OperationName（用于先抓快照、后赋名场景）。</summary>
		public UndoEntry WithOperationName(string operationName)
		{
			return new UndoEntry(HeadSha, CurrentBranchName, operationName, TimestampUtc);
		}
	}
}
