using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Undo;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// v3.4.0：Reflog 视图窗口。
	///
	/// 展示 git reflog HEAD 的完整历史（最多 DefaultMaxCount=200 条），
	/// 与 .git/forkplus-undo-index.json 做 left-outer join：
	/// - 命中索引：显示 UI 友好操作名（如 "Commit 'fix: bug'"）
	/// - 未命中：降级显示 reflog 原生 subject（如 "commit: fix: bug"）
	///
	/// 双击条目：弹窗确认后走 AddUndoable("Jump to HEAD@{N}", reset --hard &lt;sha&gt;)，
	/// 让用户能 Undo 回到跳转前的状态。
	///
	/// 价值：让用户看到超栈深度（v3.3.0 LostCount）以外的完整历史，
	/// 并能从历史任意状态恢复（即使重启软件后 Undo/Redo 栈已清空）。
	/// </summary>
	public partial class ReflogWindow : CustomWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		public ReflogWindow(RepositoryUserControl repositoryUserControl)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			ApplyLocalization();
			LoadReflog();
		}

		private void ApplyLocalization()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			Title = PreferencesLocalization.Translate("Reflog", language);
			HeaderTitle.Text = PreferencesLocalization.Translate("Reflog History", language);
			RefreshButton.Content = PreferencesLocalization.Translate("Refresh", language);
			JumpButton.Content = PreferencesLocalization.Translate("Jump to...", language);
			StatusText.Text = PreferencesLocalization.Translate("Double-click an entry to jump to that state.", language);
			// v3.4.1：翻译 GridView 列头
			foreach (object col in ReflogListView.View.Columns)
			{
				if (col is GridViewColumn gvc && gvc.Header is string header)
				{
					gvc.Header = PreferencesLocalization.Translate(header, language);
				}
			}
		}

		/// <summary>读取 reflog 并填充 ListView。</summary>
		private void LoadReflog()
		{
			if (_repositoryUserControl?.GitModule == null)
			{
				StatusText.Text = PreferencesLocalization.Translate("No active repository.", ForkPlusSettings.Default.UiLanguage);
				ReflogListView.ItemsSource = null;
				return;
			}

			GitModule gitModule = _repositoryUserControl.GitModule;
			List<ReflogEntry> reflog = new ReflogHistoryProvider().ReadHeadReflog(gitModule);
			if (reflog.Count == 0)
			{
				StatusText.Text = PreferencesLocalization.Translate("Reflog is empty.", ForkPlusSettings.Default.UiLanguage);
				ReflogListView.ItemsSource = null;
				return;
			}

			// 加载 UndoIndexStore 一次性 join（避免每条 reflog 都查一次磁盘）
			Dictionary<string, UndoIndexEntry> index = new UndoIndexStore(gitModule).Load();

			List<ReflogViewItem> items = new List<ReflogViewItem>(reflog.Count);
			foreach (ReflogEntry entry in reflog)
			{
				string operationName = entry.ReflogSubject ?? "";
				if (index.TryGetValue(entry.Sha, out UndoIndexEntry indexed) && !string.IsNullOrEmpty(indexed.OperationName))
				{
					operationName = indexed.OperationName;
				}
				items.Add(new ReflogViewItem(entry, operationName));
			}
			ReflogListView.ItemsSource = items;
			StatusText.Text = string.Format(PreferencesLocalization.Translate("{0} entries loaded.", ForkPlusSettings.Default.UiLanguage), items.Count);
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			LoadReflog();
		}

		private void ReflogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			JumpButton.IsEnabled = ReflogListView.SelectedItem is ReflogViewItem;
		}

		private void ReflogListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			JumpToSelected();
		}

		private void JumpButton_Click(object sender, RoutedEventArgs e)
		{
			JumpToSelected();
		}

		/// <summary>双击或点 Jump to... 按钮时触发：弹窗确认后走 AddUndoable 跳转。</summary>
		private void JumpToSelected()
		{
			if (!(ReflogListView.SelectedItem is ReflogViewItem selected))
			{
				return;
			}
			string language = ForkPlusSettings.Default.UiLanguage;
			string message = string.Format(
				PreferencesLocalization.Translate("Jump to HEAD to {0} ({1})?\n\nThis will reset your current branch and working tree to that state. You can undo this afterwards.", language),
				selected.ShaDisplay, selected.OperationName);
			if (!new MessageBoxWindow(
				PreferencesLocalization.Translate("Jump to Reflog Entry", language),
				message,
				PreferencesLocalization.Translate("Jump", language),
				PreferencesLocalization.Translate("Cancel", language),
				showCancelButton: true,
				550.0).ShowDialog().GetValueOrDefault())
			{
				return;
			}

			GitModule gitModule = _repositoryUserControl?.GitModule;
			if (gitModule == null || string.IsNullOrEmpty(selected.Sha))
			{
				return;
			}
			string sha = selected.Sha;
			string opName = string.Format(PreferencesLocalization.Translate("Jump to HEAD@{{{0}}}", language), selected.Index);
			_repositoryUserControl.AddUndoable(opName, delegate(JobMonitor monitor)
			{
				GitCommand resetCmd = new GitCommand(App.OverrideCredentialHelperBt, "reset", "--hard", sha);
				monitor?.Append(null, resetCmd);
				ProcessOutputHandler handler = new ProcessOutputHandler(monitor);
				ExecuteWithCallbackResponse resp = new GitRequest(gitModule).Command(resetCmd).ExecuteWithCallbackBt(handler.StdoutHandler, handler.StderrHandler, monitor);
				if (monitor != null && monitor.IsCanceled)
				{
					return GitCommandResult.Failure(new GitCommandError.Cancelled());
				}
				ISpawnError error = resp.Error;
				if (error != null)
				{
					return GitCommandResult.Failure(error.ToGitCommandError());
				}
				if (!resp.Result.Success)
				{
					return GitCommandResult.Failure(new GitCommandError.GitError(handler.FullOutput(), handler.Stderr()));
				}
				return GitCommandResult.Success();
			}, JobFlags.SaveToLog | JobFlags.ShowOnToolbar);
		}
	}

	/// <summary>ReflogWindow 的 ListView 行视图模型。</summary>
	public sealed class ReflogViewItem
	{
		private readonly ReflogEntry _entry;

		public ReflogViewItem(ReflogEntry entry, string operationName)
		{
			_entry = entry;
			OperationName = operationName ?? "";
		}

		/// <summary>完整 40 字符 sha。JumpToSelected 用它构造 reset --hard 参数。</summary>
		public string Sha => _entry.Sha ?? "";

		/// <summary>reflog 索引（HEAD@{N} 的 N）。JumpToSelected 用它生成操作名。</summary>
		public int Index => _entry.Index;

		public string IndexDisplay => "HEAD@{" + _entry.Index + "}";

		public string ShaDisplay => string.IsNullOrEmpty(_entry.Sha) ? "" : _entry.Sha.Substring(0, Math.Min(8, _entry.Sha.Length));

		public string OperationName { get; }

		public string CommitSubject => _entry.CommitSubject ?? "";

		public string TimeDisplay => _entry.TimestampUtc.HasValue
			? _entry.TimestampUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
			: "";
	}
}
