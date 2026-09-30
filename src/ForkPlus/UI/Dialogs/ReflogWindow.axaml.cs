using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.WpfCompat;
using ForkPlus.Undo;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// v3.4.0：Reflog 视图窗口。WS9 升级为「操作历史时间线」。
	///
	/// 展示 git reflog HEAD 的完整历史（最多 DefaultMaxCount=200 条），
	/// 与 .git/forkplus-undo-index.json 做 left-outer join：
	/// - 命中索引：左侧时间线节点显示为 Accent 高亮大圆点，行内显示 UI 友好操作名（加粗，
	///   如 "Commit 'fix: bug'"），悬停提示附带原始 reflog 消息
	/// - 未命中：灰色小圆点，降级显示 reflog 原生 subject（如 "commit: fix: bug"）
	///
	/// 时间列显示相对时间（DateTimeHelper.ToRelativeString，如 "3 minutes ago"）；
	/// 时间戳缺失或时钟偏斜（未来时间）时回退为绝对本地时间。
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
			// 注意：此处不能调用 PreferencesLocalization.Apply——基类 ForkPlusDialogWindow
			// 在 Loaded 时已自动本地化。构造函数里 LoadReflog() 会把 StatusText.Text 动态
			// 设置为状态文案（"Reflog is empty." / "{0} entries loaded." 等），构造期先 Apply
			// 会把 XAML 默认提示文本缓存为 Original，Loaded 时基类的二次 Apply 将从缓存恢复，
			// 覆盖真实状态文案（表现为状态栏恒显示"双击条目可跳转"提示）。
			_repositoryUserControl = repositoryUserControl;
			ApplyLocalization();
			LoadReflog();
			// 列头与行对齐同步（2026-09-30 修复）：行内容区相对 ListBox 有 ListBoxItem
			// Padding=6 的左右内缩 + 竖滚动条占位，表头 Grid 需按已实现行容器的真实
			// 几何补 Margin，否则表头与下方内容错位（详见 ReflogListView_LayoutUpdated）。
			ReflogListView.LayoutUpdated += ReflogListView_LayoutUpdated;
		}

		/// <summary>
		/// 表头列与行内容对齐：从首个已实现行容器反推行内容区在 ListBox 内的原点与右缘
		/// （自动覆盖 ListBox 模板内边距与竖滚动条占宽），把表头 Grid 的 Margin 校准为
		/// [origin+6, 右缘+6]；目标值与当前值相等时短路，避免重入布局风暴。
		/// LayoutUpdated 在容器首次实现/滚动条出现消失/窗口缩放时都会触发，无需单独挂事件。
		/// </summary>
		private void ReflogListView_LayoutUpdated(object sender, EventArgs e)
		{
			global::Avalonia.Controls.Control container = ReflogListView.GetRealizedContainers().FirstOrDefault();
			if (container == null)
			{
				return;
			}
			global::Avalonia.Matrix? matrix = container.TransformToVisual(ReflogListView);
			global::Avalonia.Point origin = matrix.HasValue ? new global::Avalonia.Point(matrix.Value.M31, matrix.Value.M32) : new global::Avalonia.Point();
			global::Avalonia.Thickness target = new global::Avalonia.Thickness(
				origin.X + 6.0,
				0.0,
				ReflogListView.Bounds.Width - (origin.X + container.Bounds.Width) + 6.0,
				6.0);
			if (HeaderColumns.Margin != target)
			{
				HeaderColumns.Margin = target;
			}
		}

		private void ApplyLocalization()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			Title = PreferencesLocalization.Translate("Reflog", language);
			HeaderTitle.Text = PreferencesLocalization.Translate("Reflog History", language);
			IndexHeader.Text = PreferencesLocalization.Translate("Index", language);
			ShaHeader.Text = PreferencesLocalization.Translate("SHA", language);
			OperationHeader.Text = PreferencesLocalization.Translate("Operation", language);
			CommitSubjectHeader.Text = PreferencesLocalization.Translate("Commit Subject", language);
			TimeHeader.Text = PreferencesLocalization.Translate("Time", language);
			RefreshButton.Content = PreferencesLocalization.Translate("Refresh", language);
			JumpButton.Content = PreferencesLocalization.Translate("Jump to...", language);
			StatusText.Text = PreferencesLocalization.Translate("Double-click an entry to jump to that state.", language);
			TimelineLegendText.Text = PreferencesLocalization.Translate("Accent dots are operations performed in ForkPlus; gray dots are other reflog entries.", language);
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

			List<ReflogViewItem> items = BuildViewItems(reflog, index);
			ReflogListView.ItemsSource = items;
			StatusText.Text = string.Format(PreferencesLocalization.Translate("{0} entries loaded.", ForkPlusSettings.Default.UiLanguage), items.Count);
		}

		/// <summary>
		/// WS9：reflog × undo-index 的 left-outer join 装配行视图模型。
		/// internal 供单元测试直接断言命中/未命中行为，UI 装配与纯逻辑共用同一条路径。
		/// 时间线首/末标记在此计算：首条上方不画线、末条下方不画线。
		/// </summary>
		internal static List<ReflogViewItem> BuildViewItems(List<ReflogEntry> reflog, Dictionary<string, UndoIndexEntry> index)
		{
			List<ReflogViewItem> items = new List<ReflogViewItem>(reflog.Count);
			for (int i = 0; i < reflog.Count; i++)
			{
			ReflogEntry entry = reflog[i];
			// 命中判定：sha 在索引中且操作名非空（空名索引条目视为未命中，降级到原生 subject）
			bool isIndexed = false;
			string operationName = entry.ReflogSubject ?? "";
			if (index != null
				&& index.TryGetValue(entry.Sha, out UndoIndexEntry indexed)
				&& !string.IsNullOrEmpty(indexed.OperationName))
			{
				isIndexed = true;
				operationName = indexed.OperationName;
			}
				items.Add(new ReflogViewItem(entry, operationName, isIndexed, isFirst: i == 0, isLast: i == reflog.Count - 1));
			}
			return items;
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			LoadReflog();
		}

		private void ReflogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			JumpButton.IsEnabled = ReflogListView.SelectedItem is ReflogViewItem;
		}

		private void ReflogListView_MouseDoubleClick(object sender, global::Avalonia.Input.TappedEventArgs e)
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
			MessageBoxWindow confirmDialog = new MessageBoxWindow(
				PreferencesLocalization.Translate("Jump to Reflog Entry", language),
				message,
				PreferencesLocalization.Translate("Jump", language),
				PreferencesLocalization.Translate("Cancel", language),
				showCancelButton: true,
				550.0);
			// 修复（2026-09-30，"跳转确认弹窗取消后主窗口压到 Reflog 窗口上面"）：
			// ForkPlusDialogWindow 构造自动 SetOwnerCompat(MainWindow.Instance)，本弹窗却从
			// ReflogWindow（非模态独立窗口）弹出——ShowDialog shim 以 MainWindow 做 Avalonia
			// owner，关闭时 Avalonia 重新激活 owner，主窗口跳到最前把 Reflog 窗口压到下面。
			// 显式改挂本窗口（同 InteractiveRebaseWindow 确认弹窗先例），关闭后焦点回到 Reflog。
			confirmDialog.SetOwnerCompat(this);
			if (!confirmDialog.ShowDialog().GetValueOrDefault())
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

	/// <summary>ReflogWindow 的 ListView 行视图模型。WS9：附加时间线标记（命中索引/首末条）与相对时间。</summary>
	public sealed class ReflogViewItem
	{
		private readonly ReflogEntry _entry;

		/// <summary>
		/// isIndexedOperation/isFirst/isLast 带默认值：两参构造保持 v3.4.0 既有语义
		/// （未命中索引的中间时间线条目），既有调用方与单测无需改动。
		/// </summary>
		public ReflogViewItem(ReflogEntry entry, string operationName, bool isIndexedOperation = false, bool isFirst = false, bool isLast = false)
		{
			_entry = entry;
			OperationName = operationName ?? "";
			IsIndexedOperation = isIndexedOperation;
			IsFirst = isFirst;
			IsLast = isLast;
		}

		/// <summary>完整 40 字符 sha。JumpToSelected 用它构造 reset --hard 参数。</summary>
		public string Sha => _entry.Sha ?? "";

		/// <summary>reflog 索引（HEAD@{N} 的 N）。JumpToSelected 用它生成操作名。</summary>
		public int Index => _entry.Index;

		/// <summary>WS9：sha 命中 UndoIndexStore（ForkPlus 记录过的操作）。
		/// true = 时间线 Accent 高亮节点 + 操作名加粗；false = 灰色节点 + 原生 reflog 消息。</summary>
		public bool IsIndexedOperation { get; }

		/// <summary>WS9：是否为时间线首条（最近一次操作）。首条上方不画竖线。</summary>
		public bool IsFirst { get; }

		/// <summary>WS9：是否为时间线末条（最旧加载到的操作）。末条下方不画竖线。</summary>
		public bool IsLast { get; }

		/// <summary>时间线竖线上半段可见性（首条为 false，避免线超出时间线顶端）。</summary>
		public bool TimelineShowTopLine => !IsFirst;

		/// <summary>时间线竖线下半段可见性（末条为 false，避免线超出时间线底端）。</summary>
		public bool TimelineShowBottomLine => !IsLast;

		public string IndexDisplay => "HEAD@{" + _entry.Index + "}";

		public string ShaDisplay => string.IsNullOrEmpty(_entry.Sha) ? "" : _entry.Sha.Substring(0, Math.Min(8, _entry.Sha.Length));

		/// <summary>行内主文案。命中索引时为 UI 友好操作名（XAML 中加粗显示）；未命中为原始 reflog 动作。</summary>
		public string OperationName { get; }

		/// <summary>原始 reflog 消息（如 "commit: fix: bug" / "reset: moving to HEAD~1"）。命中索引时仍保留在悬停提示中。</summary>
		public string RawReflogSubject => _entry.ReflogSubject ?? "";

		public string CommitSubject => _entry.CommitSubject ?? "";

		/// <summary>绝对本地时间（悬停提示用；也是无时间戳/时钟偏斜时的回退显示）。</summary>
		public string TimeDisplay => _entry.TimestampUtc.HasValue
			? _entry.TimestampUtc.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
			: "";

		/// <summary>
		/// WS9：相对时间（如 "3 minutes ago"，复用 DateTimeHelper.ToRelativeString 的本地化）。
		/// 时间戳缺失返回空；时间戳在未来（时钟偏斜，ToRelativeString 会产生负数）时回退绝对时间。
		/// </summary>
		public string RelativeTimeDisplay
		{
			get
			{
				if (!_entry.TimestampUtc.HasValue)
				{
					return "";
				}
				DateTime local = _entry.TimestampUtc.Value.ToLocalTime();
				if (local > DateTime.Now)
				{
					return TimeDisplay;
				}
				return DateTimeHelper.ToRelativeString(local);
			}
		}

		/// <summary>行悬停提示：操作名 +（命中时的）原始 reflog 消息 + 绝对本地时间。</summary>
		public string TooltipText
		{
			get
			{
				string text = OperationName;
				if (IsIndexedOperation && !string.IsNullOrEmpty(RawReflogSubject))
				{
					text += "\n" + RawReflogSubject;
				}
				if (!string.IsNullOrEmpty(TimeDisplay))
				{
					text += "\n" + TimeDisplay;
				}
				return text;
			}
		}
	}
}
