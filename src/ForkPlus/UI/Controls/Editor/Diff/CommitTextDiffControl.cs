using System;
using Avalonia.Controls;
using Avalonia.Layout;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class CommitTextDiffControl : TextDiffControl
	{
		// 修复（2026-09-16，"AI 代码检视页面屏蔽 暂存/丢弃 浮窗"）：
		// 本地存值：RefreshLayout 换子控件（Split ↔ SideBySide）后向新子控件重放，
		// 避免布局切换后浮窗重新出现。
		private bool _showStageDiscardButtons = true;

		public bool ShowStageDiscardButtons
		{
			get
			{
				return _showStageDiscardButtons;
			}
			set
			{
				_showStageDiscardButtons = value;
				if (_child is ICommitTextDiffControl commitChild)
				{
					commitChild.ShowStageDiscardButtons = value;
				}
			}
		}

		public bool IsStaged
		{
			get
			{
				return (_child as ICommitTextDiffControl).IsStaged;
			}
			set
			{
				(_child as ICommitTextDiffControl).IsStaged = value;
			}
		}

		public bool IsNewOrUntracked
		{
			get
			{
				return (_child as ICommitTextDiffControl).IsNewOrUntracked;
			}
			set
			{
				(_child as ICommitTextDiffControl).IsNewOrUntracked = value;
			}
		}

		public event EventHandler<CommitCodeEditor> ToggleStage;

		public event EventHandler<CommitCodeEditor> Stage;

		public event EventHandler<CommitCodeEditor> Unstage;

		public event EventHandler<CommitCodeEditor> Discard;

		public CommitTextDiffControl(FileDiffControlTarget target)
			: base(target)
		{
		}

		protected override void RefreshLayout()
		{
			_child?.ControlWillBeRemovedFromFileDiffControl();
			base.Children.Clear();
			ITextDiffControl child = _child;
			if (base.LayoutMode == DiffLayoutMode.Split)
			{
				_child = new SplitCommitTextDiffControl();
			}
			else if (base.LayoutMode == DiffLayoutMode.SideBySide)
			{
				_child = new SideBySideCommitTextDiffControl();
			}
			_child.RefreshDiffShowHiddenSymbols(ForkPlusSettings.Default.DiffShowHiddenSymbols);
			_child.RefreshDiffWordWrap(ForkPlusSettings.Default.DiffWordWrap);
			_child.RefreshDiffFont(ForkPlusSettings.Default.CodeEditorFontSize);
			(_child as ICommitTextDiffControl).ShowStageDiscardButtons = _showStageDiscardButtons;
			if (child != null && child.Diff != null)
			{
				_child.PositionCache = child.PositionCache;
				_child.SetDiff(child.Diff, child.TabWidth, child.EntireFile, child.Location);
			}
			(_child as ICommitTextDiffControl).ToggleStage += delegate(object s, CommitCodeEditor e)
			{
				this.ToggleStage?.Invoke(this, e);
			};
			(_child as ICommitTextDiffControl).Stage += delegate(object s, CommitCodeEditor e)
			{
				this.Stage?.Invoke(this, e);
			};
			(_child as ICommitTextDiffControl).Unstage += delegate(object s, CommitCodeEditor e)
			{
				this.Unstage?.Invoke(this, e);
			};
			(_child as ICommitTextDiffControl).Discard += delegate(object s, CommitCodeEditor e)
			{
				this.Discard?.Invoke(this, e);
			};
			_child.EditorContextMenuOpening += delegate(object s, global::Avalonia.Input.ContextRequestedEventArgs e)
			{
				RaiseEditorContextMenuOpening(this, e);
			};
			if (_child is Control childControl)
			{
				childControl.HorizontalAlignment = HorizontalAlignment.Stretch;
				childControl.VerticalAlignment = VerticalAlignment.Stretch;
			}
			if (!VisualTreeAttachmentHelper.TryAddChild(this, _child as Grid, GetType().Name + ".Child"))
			{
				_child = null;
			}
		}
	}
}
