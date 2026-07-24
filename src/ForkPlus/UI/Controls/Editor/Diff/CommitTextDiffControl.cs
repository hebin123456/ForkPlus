using System;
using Avalonia.Controls;
using ForkPlus.Settings;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class CommitTextDiffControl : TextDiffControl
	{
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
			_child.EditorContextMenuOpening += delegate(object s, ContextRequestedEventArgs e)
			{
				RaiseEditorContextMenuOpening(this, e);
			};
			// TODO(4.7-a): VisualTreeAttachmentHelper.TryAddChild 会先从旧 parent detach 再 add。
			// Avalonia Panel.Children.Add 在已有 parent 时抛异常，暂直接 Add；若 _child 从旧控件移入新控件时可能需手动 detach。
			base.Children.Add(_child as Grid);
		}
	}
}
