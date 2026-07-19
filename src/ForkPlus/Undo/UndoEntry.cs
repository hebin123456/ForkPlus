using System;

namespace ForkPlus.Undo
{
	/// <summary>
	/// Undo/Redo 栈条目。轻量结构，只保存恢复仓库到某状态所需的最小信息。
	///
	/// 设计原则：
	/// - 不再保存完整仓库快照（旧 RepositorySnapshot 有 11 字段，每次抓 7 次 git 进程）
	/// - HEAD sha 是恢复真相源（git reset --hard 即可）
	/// - OperationName 通过 UndoIndexStore 持久化到 .git/forkplus-undo-index.json
	/// - 当前分支名用于 Undo 后切回原分支（避免进入 detached HEAD）
	///
	/// v3.4.0 Layer 2 扩展：
	/// - 新增 PreOperationStashSha：操作前用 git stash create 抓的工作区快照 sha
	/// - 用于 undo discard/stage/unstage/delete branch 等工作区级操作
	/// - HEAD 移动类操作（commit/checkout/reset 等）此字段通常为 null（工作区干净或无关）
	/// - 恢复时：先 checkout+reset 恢复 HEAD，再 stash apply --index 恢复工作区
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

		/// <summary>
		/// v3.4.0：操作前用 `git stash create --include-untracked` 抓的工作区快照 sha。
		/// 为 null 表示工作区干净（无未提交变更）或抓取失败。
		/// Undo 时用 `git stash apply --index &lt;sha&gt;` 恢复工作区 + index 状态。
		/// </summary>
		public string PreOperationStashSha { get; }

		public UndoEntry(string headSha, string currentBranchName, string operationName, DateTime timestampUtc, string preOperationStashSha = null)
		{
			HeadSha = headSha;
			CurrentBranchName = currentBranchName;
			OperationName = operationName ?? "";
			TimestampUtc = timestampUtc;
			PreOperationStashSha = preOperationStashSha;
		}

		/// <summary>创建一个副本，仅替换 OperationName（用于先抓快照、后赋名场景）。</summary>
		public UndoEntry WithOperationName(string operationName)
		{
			return new UndoEntry(HeadSha, CurrentBranchName, operationName, TimestampUtc, PreOperationStashSha);
		}
	}
}
