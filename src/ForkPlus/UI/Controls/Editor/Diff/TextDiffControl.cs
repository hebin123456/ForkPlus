using System.Windows;
using System.Windows.Controls;
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

		public event ContextMenuEventHandler EditorContextMenuOpening;

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
			WeakEventManager<NotificationCenter, EventArgs<DiffLayoutMode>>.AddHandler(NotificationCenter.Current, "DiffLayoutModeChanged", delegate
			{
				RefreshDiffLayoutMode();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffShowHiddenSymbolsChanged", delegate
			{
				RefreshDiffShowHiddenSymbols();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current, "DiffWordWrapChanged", delegate
			{
				RefreshDiffWordWrap();
			});
			WeakEventManager<NotificationCenter, EventArgs<double>>.AddHandler(NotificationCenter.Current, "CodeEditorFontSizeChanged", delegate
			{
				RefreshDiffFontSize();
			});
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
			_child.EditorContextMenuOpening += delegate(object s, ContextMenuEventArgs e)
			{
				RaiseEditorContextMenuOpening(this, e);
			};
			if (!VisualTreeAttachmentHelper.TryAddChild(this, _child as Grid, GetType().Name + ".Child"))
			{
				_child = null;
			}
		}

		protected void RaiseEditorContextMenuOpening(object sender, ContextMenuEventArgs e)
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
