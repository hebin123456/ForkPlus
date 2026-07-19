using System;
using System.Collections.Generic;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 仓库状态快照：用于 Undo/Redo 功能。在每次"会改仓库"的操作执行前抓取一份，
	/// Undo 时通过 RestoreSnapshotGitCommand 把仓库恢复到这个快照描述的状态。
	/// v3.0.0 P1 阶段仅使用 HeadSha / CurrentBranchName / HeadReflog 三个字段，
	/// P2 阶段会扩展 LocalBranches / Tags / StashShas 等字段的恢复逻辑。
	/// </summary>
	public class RepositorySnapshot
	{
		/// <summary>操作名称，用于 Undo/Redo 历史列表显示，例如 "Commit 'fix: bug'" / "Checkout 'feature/x'" / "Reset to abc123"。</summary>
		public string OperationName { get; private set; }

		/// <summary>快照抓取时间（UTC）。</summary>
		public DateTime TimestampUtc { get; private set; }

		/// <summary>操作前的 HEAD commit sha（40 字符）。可能为 null（空仓库）。</summary>
		public string HeadSha { get; private set; }

		/// <summary>操作前的当前分支名。可能为 null（detached HEAD 或空仓库）。</summary>
		public string CurrentBranchName { get; private set; }

		/// <summary>操作前 HEAD reflog 最近 50 条 sha，作为深栈兜底（栈深度超出时仍可恢复）。</summary>
		public string[] HeadReflog { get; private set; }

		/// <summary>操作前 .git/ORIG_HEAD 内容，如果存在。</summary>
		public string OrigHead { get; private set; }

		// ===== P2 扩展字段（P1 阶段先留空，RestoreSnapshotGitCommand 暂不消费） =====

		/// <summary>操作前所有本地分支的 name -> sha 映射（P2 启用）。</summary>
		public Dictionary<string, string> LocalBranches { get; private set; }

		/// <summary>操作前所有 tag 的 name -> sha 映射（P2 启用）。</summary>
		public Dictionary<string, string> Tags { get; private set; }

		/// <summary>操作前 stash 列表的 sha（按 stash@{0}, stash@{1}... 顺序，P2 启用）。</summary>
		public List<string> StashShas { get; private set; }

		/// <summary>操作前工作区是否 dirty（P3 用于决定是否需要先 stash）。</summary>
		public bool IsWorkingTreeDirty { get; private set; }

		/// <summary>操作前 staged + unstaged 文件数（P3 用于提示用户）。</summary>
		public int ChangedFilesCount { get; private set; }

		public RepositorySnapshot(
			string operationName,
			DateTime timestampUtc,
			string headSha,
			string currentBranchName,
			string[] headReflog,
			string origHead,
			Dictionary<string, string> localBranches = null,
			Dictionary<string, string> tags = null,
			List<string> stashShas = null,
			bool isWorkingTreeDirty = false,
			int changedFilesCount = 0)
		{
			OperationName = operationName ?? "";
			TimestampUtc = timestampUtc;
			HeadSha = headSha;
			CurrentBranchName = currentBranchName;
			HeadReflog = headReflog ?? new string[0];
			OrigHead = origHead;
			LocalBranches = localBranches ?? new Dictionary<string, string>();
			Tags = tags ?? new Dictionary<string, string>();
			StashShas = stashShas ?? new List<string>();
			IsWorkingTreeDirty = isWorkingTreeDirty;
			ChangedFilesCount = changedFilesCount;
		}

		/// <summary>创建一个副本，仅替换 OperationName（用于先抓快照、后赋名场景）。</summary>
		public RepositorySnapshot WithOperationName(string operationName)
		{
			return new RepositorySnapshot(
				operationName,
				TimestampUtc,
				HeadSha,
				CurrentBranchName,
				HeadReflog,
				OrigHead,
				LocalBranches,
				Tags,
				StashShas,
				IsWorkingTreeDirty,
				ChangedFilesCount);
		}
	}
}
