using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.Undo;

namespace ForkPlus.UI.UserControls
{
	/// <summary>
	/// 阶段 3 里程碑 3.15：RepositoryUserControl 的 ViewModel。
	/// 承载纯业务状态字段 + 纯逻辑方法（零 WPF 依赖）。
	/// View 保留 WPF 事件处理与 UI 副作用，通过本 VM 转发。
	/// 本 VM 是解锁 Commands 层最后 5 处 ActiveRepositoryUserControl() 直访的钥匙。
	/// </summary>
	public class RepositoryUserControlViewModel
	{
		private bool _isDirty;

		private SubDomain _invalidatedSubdomains = SubDomain.All;

		private RepositoryViewMode _viewMode;

		private Job _activeFetchRevisionsUntilShaJob;

		[Null]
		private Job _activeFetchRevisionsNextPageJob;

		/// <summary>
		/// 视图模式变更事件。View 订阅后调用 Content/Sidebar.SetRepositoryViewMode + NotificationBar.Refresh。
		/// 把 ViewMode setter 的 UI 副作用从 VM 中剥离，保持 VM 零 WPF。
		/// </summary>
		public event Action<RepositoryViewMode> ViewModeChanged;

		/// <summary>Undo/Redo 状态变化时触发，UI 工具栏订阅以刷新按钮可用性。</summary>
		public event EventHandler UndoRedoStateChanged;

		public TempFileManager TempFileManager { get; } = new TempFileManager();

		public JobQueue JobQueue { get; } = new JobQueue();

		/// <summary>本仓库的 Undo/Redo 历史栈。v3.0.0 新增。</summary>
		public UndoRedoStack UndoRedoStack { get; } = new UndoRedoStack();

		public RefreshRepositoryCommand RefreshRepositoryCommand { get; } = new RefreshRepositoryCommand();

		public RepositoryData RepositoryData { get; set; }

		public RepositoryStatus RepositoryStatus { get; set; }

		public GitModule GitModule { get; set; }

		public CommitGraphCache CommitGraphCache { get; set; }

		public string RepositoryName { get; set; }

		public string ParentRepositoryName { get; set; }

		public string RepositoryTitle { get; set; }

		public RepositoryColor RepositoryColor { get; set; }

		/// <summary>
		/// 仓库是否脏。getter 兼顾 AutomaticStatusUpdateInterval 设置（与原 View 行为一致）。
		/// </summary>
		public bool IsDirty
		{
			get
			{
				if (_isDirty)
				{
					return ForkPlusSettings.Default.AutomaticStatusUpdateInterval > 0;
				}
				return false;
			}
			set
			{
				_isDirty = value;
			}
		}

		public SubDomain InvalidatedSubdomains => _invalidatedSubdomains;

		/// <summary>
		/// 视图模式。setter 仅更新内部状态并触发 <see cref="ViewModeChanged"/> 事件，
		/// UI 副作用（Content/Sidebar.SetRepositoryViewMode + NotificationBar.Refresh）由 View 订阅事件执行。
		/// </summary>
		public RepositoryViewMode ViewMode
		{
			get
			{
				return _viewMode;
			}
			set
			{
				if (_viewMode != value)
				{
					_viewMode = value;
					ViewModeChanged?.Invoke(_viewMode);
				}
			}
		}

		public bool ShowReflogInRevisionList { get; set; }

		public Job ActiveFetchRevisionsUntilShaJob
		{
			get { return _activeFetchRevisionsUntilShaJob; }
			set { _activeFetchRevisionsUntilShaJob = value; }
		}

		[Null]
		public Job ActiveFetchRevisionsNextPageJob
		{
			get { return _activeFetchRevisionsNextPageJob; }
			set { _activeFetchRevisionsNextPageJob = value; }
		}

		/// <summary>激活 Revision 视图（纯状态变更，UI 副作用由 ViewModeChanged 事件驱动）。</summary>
		public void ActivateRevisionView()
		{
			ViewMode = RepositoryViewMode.RevisionViewMode;
		}

		/// <summary>标记指定子域为失效（位运算，纯逻辑）。</summary>
		public void Invalidate(SubDomain subdomains)
		{
			_invalidatedSubdomains |= subdomains;
		}

		/// <summary>清除指定子域的失效标记（位运算，纯逻辑）。</summary>
		public void ResetSubdomains(SubDomain subdomains)
		{
			_invalidatedSubdomains &= ~subdomains;
		}

		/// <summary>取消当前活跃的分页抓取任务（纯 Job 取消逻辑）。</summary>
		public void CancelActiveFetchRevisionsJobs()
		{
			_activeFetchRevisionsUntilShaJob?.Monitor.Cancel();
			_activeFetchRevisionsUntilShaJob = null;
			_activeFetchRevisionsNextPageJob?.Monitor.Cancel();
			_activeFetchRevisionsNextPageJob = null;
		}

		/// <summary>触发 Undo/Redo 状态变更事件（供工具栏刷新按钮可用性）。</summary>
		public void RaiseUndoRedoStateChanged()
		{
			UndoRedoStateChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>
		/// 查找父仓库名（submodule/worktree 判断）。纯逻辑，仅依赖 GitModule + RepositoryManager + Path。
		/// 原方法在 View 第 137-163 行，OpenRepository 第 308-355 行有重复实现，本 VM 统一入口。
		/// </summary>
		[Null]
		public static string FindParentRepositoryName(GitModule gitModule)
		{
			if (gitModule == null)
			{
				return null;
			}
			if (gitModule.Type == ModuleType.Submodule)
			{
				string parentRepoPath = gitModule.ParentRepoPath;
				if (parentRepoPath == null)
				{
					return null;
				}
				return RepositoryManager.Instance.FindRepositoryName(parentRepoPath) ?? Path.GetFileName(parentRepoPath);
			}
			if (gitModule.Type == ModuleType.Worktree)
			{
				string commonGitDir = gitModule.CommonGitDir;
				if (commonGitDir == null)
				{
					return null;
				}
				if (Path.GetFileName(commonGitDir) != ".git")
				{
					return Path.GetFileName(commonGitDir);
				}
				string directoryName = Path.GetDirectoryName(commonGitDir);
				return RepositoryManager.Instance.FindRepositoryName(directoryName) ?? Path.GetFileName(directoryName);
			}
			return null;
		}

		/// <summary>统计去重后的变更文件数（纯 static，与原 View 实现一致用 Ordinal 比较）。</summary>
		public static int CountDistinctChangedFiles(ChangedFile[] files)
		{
			if (files == null || files.Length == 0)
			{
				return 0;
			}
			HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
			foreach (ChangedFile changedFile in files)
			{
				paths.Add(changedFile.Path);
			}
			return paths.Count;
		}
	}
}
