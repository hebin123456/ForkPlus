using System.Collections.Generic;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	/// <summary>
	/// 阶段 3：CommitUserControl 的 ViewModel。
	/// 承载纯状态字段 + 纯计算属性 + 纯静态工具方法（零 WPF 依赖）。
	/// View 保留 WPF 事件处理与 UI 副作用（Dispatcher / TextBox 直访 / CheckBox 直访等），
	/// 通过本 VM 转发；公共属性签名保持不变，维持外部调用契约（Commands 层等）。
	/// 与 RepositoryUserControlViewModel 同模式：VM 持有纯数据属性，View 同步并转发。
	/// </summary>
	public class CommitUserControlViewModel
	{
		// --- provider 字段（迁移自 View 第 42/45 行）---
		// commit description 框的自动补全提供器（最近作者等）
		public CommitMessageAutocompleteProvider CommitMessageAutocompleteProvider { get; } = new CommitMessageAutocompleteProvider();

		// Gitmoji 自动补全：commit subject 输入 ":" 触发 emoji 选择器
		public GitmojiAutocompleteProvider GitmojiAutocompleteProvider { get; } = new GitmojiAutocompleteProvider();

		// --- 纯状态字段（迁移自 View 第 134/136/140 行，外部 Commands 层经 View 转发读写）---
		public bool CommittingInProgress { get; set; }

		public Job StageJob { get; set; }

		public bool ShowIgnoredFiles { get; set; }

		/// <summary>
		/// 当前仓库状态。由 View 在转发 getter 中同步自 RepositoryUserControl.RepositoryStatus?.RepositoryState，
		/// 供下方纯计算属性读取，使 VM 保持零 WPF 依赖（不直访 RepositoryUserControl）。
		/// </summary>
		[Null]
		public RepositoryState RepositoryState { get; set; }

		// --- 纯计算属性（迁移自 View，逻辑等价）---

		/// <summary>是否处于 squash 进行中（迁移自 View 第 87 行 SquashMode）。</summary>
		public bool SquashMode => RepositoryState is RepositoryState.SquashInProgress;

		/// <summary>是否处于 rebase 进行中（迁移自 View 第 222 行 RebaseInProgress）。</summary>
		public bool RebaseInProgress => RepositoryState is RepositoryState.RebaseInProgress;

		/// <summary>是否处于 am 进行中（迁移自 View 第 224 行 AmInProgress）。</summary>
		public bool AmInProgress => RepositoryState is RepositoryState.AmInProgress;

		/// <summary>
		/// commit 字段（subject/description）是否允许编辑（迁移自 View 第 180-207 行 AreCommitFieldsAllowed）。
		/// 依赖 RepositoryState / StageJob / CommittingInProgress（均为 VM 状态）。
		/// </summary>
		public bool AreCommitFieldsAllowed
		{
			get
			{
				RepositoryState repositoryState = RepositoryState;
				if (repositoryState == null)
				{
					return false;
				}
				if (StageJob != null)
				{
					return false;
				}
				if (CommittingInProgress)
				{
					return false;
				}
				if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress)
				{
					return rebaseInProgress.AmendSha != null;
				}
				if (repositoryState is RepositoryState.AmInProgress)
				{
					return false;
				}
				return true;
			}
		}

		/// <summary>
		/// Amend 是否允许（迁移自 View 第 1678-1702 行 isAmendAllowed()，VM 中规范命名为 IsAmendAllowed）。
		/// 依赖 RepositoryState / StageJob / CommittingInProgress（均为 VM 状态）。
		/// </summary>
		public bool IsAmendAllowed
		{
			get
			{
				RepositoryState repositoryState = RepositoryState;
				if (repositoryState == null)
				{
					return false;
				}
				if (StageJob != null)
				{
					return false;
				}
				if (CommittingInProgress)
				{
					return false;
				}
				if (repositoryState is RepositoryState.RebaseInProgress rebaseInProgress)
				{
					return rebaseInProgress.AmendSha != null;
				}
				if (repositoryState is RepositoryState.AmInProgress)
				{
					return false;
				}
				return true;
			}
		}

		// --- 纯静态工具方法（迁移自 View，零依赖）---

		/// <summary>
		/// 将完整 commit message 拆分为 subject / description（迁移自 View 第 2198-2210 行）。
		/// 规则：按首个换行切分；subject 去首尾换行，description 去首部换行。
		/// </summary>
		public static void SplitCommitMessageForFields(string fullMessage, out string subject, out string description)
		{
			string text = (fullMessage ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd(Consts.Chars.NewLines);
			int lineBreakIndex = text.IndexOf('\n');
			if (lineBreakIndex < 0)
			{
				subject = text.Trim(Consts.Chars.NewLines);
				description = string.Empty;
				return;
			}
			subject = text.Substring(0, lineBreakIndex).Trim(Consts.Chars.NewLines);
			description = text.Substring(lineBreakIndex + 1).TrimStart(Consts.Chars.NewLines);
		}

		/// <summary>统计去重后的变更文件数（迁移自 View 第 2305-2313 行）。</summary>
		public static int GetUniqueFilesCount(ChangedFile[] changedFiles)
		{
			HashSet<string> hashSet = new HashSet<string>();
			foreach (ChangedFile changedFile in changedFiles)
			{
				hashSet.Add(changedFile.Path);
			}
			return hashSet.Count;
		}

		/// <summary>
		/// 返回 target 中 string1 / string2 最早出现者的区间（迁移自 View 第 2315-2336 行）。
		/// 两者都未出现时返回 null。
		/// </summary>
		public static Range? RangeOfAny(string target, string string1, string string2)
		{
			int num = target.IndexOf(string1);
			int num2 = target.IndexOf(string2);
			if (num != -1)
			{
				if (num2 != -1)
				{
					if (num < num2)
					{
						return new Range(num, num + string1.Length);
					}
					return new Range(num2, num2 + string2.Length);
				}
				return new Range(num, num + string1.Length);
			}
			if (num2 != -1)
			{
				return new Range(num2, num2 + string2.Length);
			}
			return null;
		}
	}
}
