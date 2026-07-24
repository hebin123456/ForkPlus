using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class TextDiffControl : Grid, DiffControlContainer.IFileDiffControlSubControl
	{
		private DiffLayoutMode _layoutMode;

		protected ITextDiffControl _child;

		private readonly FileDiffControlTarget _target;

		public DiffLayoutMode LayoutMode
		{
			get
			{
				return _layoutMode;
			}
			set
			{
				_layoutMode = value;
				RefreshLayout();
			}
		}

		public CodeEditorScrollPositionCache PositionCache
		{
			get
			{
				return _child?.PositionCache;
			}
			set
			{
				if (_child != null)
				{
					_child.PositionCache = value;
				}
			}
		}

		public ForkPlus.Git.Diff.Diff DIff => _child?.Diff;

		public ScrollBarVisibility VerticalScrollBarVisibility
		{
			get
			{
				return _child.VerticalScrollBarVisibility;
			}
			set
			{
				_child.VerticalScrollBarVisibility = value;
			}
		}

		public event EventHandler<ContextRequestedEventArgs> EditorContextMenuOpening;

		public void SetDiff([Null] ForkPlus.Git.Diff.Diff diff, int tabWidth, bool entireFile, DiffLocation location)
		{
			if (_child != null)
			{
				_child.SetDiff(diff, tabWidth, entireFile, location);
			}
		}

		public TextDiffControl(FileDiffControlTarget target)
		{
			_target = target;
			// 阶段 4 里程碑 4.7-a：WeakEventManager → 直接事件订阅（Avalonia 无 WeakEventManager）。
			// NotificationCenter 是单例，直接订阅会导致 TextDiffControl 不被 GC 回收。
			// 阶段 6 改用 Avalonia WeakEvent 或 IDisposable 模式。
			NotificationCenter.Current.DiffLayoutModeChanged += delegate { RefreshDiffLayoutMode(); };
			NotificationCenter.Current.DiffShowHiddenSymbolsChanged += delegate { RefreshDiffShowHiddenSymbols(); };
			NotificationCenter.Current.DiffWordWrapChanged += delegate { RefreshDiffWordWrap(); };
			NotificationCenter.Current.CodeEditorFontSizeChanged += delegate { RefreshDiffFontSize(); };
			RefreshDiffLayoutMode();
			RefreshDiffWordWrap();
			RefreshDiffShowHiddenSymbols();
			RefreshDiffFontSize();
		}

		public void ScrollToNextCustomHunk()
		{
			_child.ScrollToNextCustomHunk();
		}

		public void ScrollToPreviousCustomHunk()
		{
			_child.ScrollToPreviousCustomHunk();
		}

		public void ControlWillBeRemovedFromFileDiffControl()
		{
			_child?.ControlWillBeRemovedFromFileDiffControl();
		}

		protected virtual void RefreshLayout()
		{
			_child?.ControlWillBeRemovedFromFileDiffControl();
			base.Children.Clear();
			ITextDiffControl child = _child;
			if (LayoutMode == DiffLayoutMode.Split)
			{
				_child = new SplitTextDiffControl();
			}
			else if (LayoutMode == DiffLayoutMode.SideBySide)
			{
				_child = new SideBySideTextDiffControl();
			}
			_child.RefreshDiffShowHiddenSymbols(ForkPlusSettings.Default.DiffShowHiddenSymbols);
			_child.RefreshDiffWordWrap(ForkPlusSettings.Default.DiffWordWrap);
			_child.RefreshDiffFont(ForkPlusSettings.Default.CodeEditorFontSize);
			if (child != null && child.Diff != null)
			{
				_child.PositionCache = child.PositionCache;
				_child.SetDiff(child.Diff, child.TabWidth, child.EntireFile, child.Location);
			}
			_child.EditorContextMenuOpening += delegate(object s, ContextRequestedEventArgs e)
			{
				RaiseEditorContextMenuOpening(this, e);
			};
			// TODO(4.7-a): VisualTreeAttachmentHelper.TryAddChild 会先从旧 parent detach 再 add。
			// Avalonia Panel.Children.Add 在已有 parent 时抛异常，暂直接 Add。
			base.Children.Add(_child as Grid);
		}

		protected void RaiseEditorContextMenuOpening(object sender, ContextRequestedEventArgs e)
		{
			this.EditorContextMenuOpening?.Invoke(this, e);
		}

		private void RefreshDiffLayoutMode()
		{
			LayoutMode = GetSettingsLayoutMode();
		}

		private void RefreshDiffShowHiddenSymbols()
		{
			_child.RefreshDiffShowHiddenSymbols(ForkPlusSettings.Default.DiffShowHiddenSymbols);
		}

		private void RefreshDiffWordWrap()
		{
			_child.RefreshDiffWordWrap(ForkPlusSettings.Default.DiffWordWrap);
		}

		private void RefreshDiffFontSize()
		{
			_child.RefreshDiffFont(ForkPlusSettings.Default.CodeEditorFontSize);
		}

		private DiffLayoutMode GetSettingsLayoutMode()
		{
			switch (_target)
			{
			case FileDiffControlTarget.Commit:
				return ForkPlusSettings.Default.CommitDiffLayoutMode;
			case FileDiffControlTarget.History:
			case FileDiffControlTarget.HunkHistory:
				return ForkPlusSettings.Default.HistoryDiffLayoutMode;
			case FileDiffControlTarget.Popup:
				return ForkPlusSettings.Default.PopupDiffLayoutMode;
			case FileDiffControlTarget.Revision:
				return ForkPlusSettings.Default.RevisionDiffLayoutMode;
			case FileDiffControlTarget.RevisionWindow:
				return ForkPlusSettings.Default.RevisionWindowDiffLayoutMode;
			default:
				return DiffLayoutMode.Split;
			}
		}
	}
}
