using System;
using Avalonia;
using Avalonia.Input;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using AvaloniaEdit.Rendering;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class CommitCodeEditor : DiffCodeEditor
	{
		private readonly ICommitDiffSelectionLayer _diffSelectionLayer;

		public CommitDiffSelectedRange ActiveChunk => _diffSelectionLayer.ActiveChunk;

		public bool IsStaged { get; set; }

		public bool IsNewOrUntracked { get; set; }

		[Null]
		public CommitCodeEditor Sibling { get; set; }

		public event EventHandler<CommitCodeEditor> ToggleStage;

		public event EventHandler<CommitCodeEditor> Stage;

		public event EventHandler<CommitCodeEditor> UnStage;

		public event EventHandler<CommitCodeEditor> Discard;

		public CommitCodeEditor(DiffViewMode diffViewMode)
			: base(diffViewMode)
		{
			if (diffViewMode == DiffViewMode.Split)
			{
				_diffSelectionLayer = new DiffSelectionLayer(this);
			}
			else
			{
				_diffSelectionLayer = new SideBySideCommitDiffSelectionLayer(this);
			}
			base.TextArea.TextView.InsertLayer(_diffSelectionLayer as Avalonia.Controls.Control, KnownLayer.Selection, LayerInsertionPosition.Above);
			_diffSelectionLayer.Stage += delegate
			{
				this.Stage?.Invoke(this, this);
			};
			_diffSelectionLayer.UnStage += delegate
			{
				this.UnStage?.Invoke(this, this);
			};
			_diffSelectionLayer.Discard += delegate
			{
				this.Discard?.Invoke(this, this);
			};
		}

		[Null]
		public Patch CreatePatchForSelection(ExtractPatchType type)
		{
			VisualPatch visualPatch = base.VisualPatch;
			if (visualPatch != null)
			{
				int[] selectedPatchLines = GetSelectedPatchLines();
				if (selectedPatchLines != null)
				{
					ForkPlus.Git.Diff.Diff diff = visualPatch.VisualDiff.ConvertTo(type, selectedPatchLines);
					if (diff != null)
					{
						return new Patch(new ForkPlus.Git.Diff.Diff[1] { diff });
					}
				}
			}
			return null;
		}

		[Null]
		public int[] GetSelectedPatchLines()
		{
			VisualDiff visualDiff = base.VisualPatch?.VisualDiff;
			if (visualDiff == null)
			{
				return null;
			}
			if (base.SelectionLength > 0)
			{
				return visualDiff.GetPatchLines(new Range(base.SelectionStart, base.SelectionStart + base.SelectionLength));
			}
			CommitDiffSelectedRange activeBlock = ActiveChunk;
			if (activeBlock != null)
			{
				if (base.DiffViewMode == DiffViewMode.SideBySideNew)
				{
					VisualPatch visualPatch = Sibling.VisualPatch;
					return visualDiff.GetPatchLines(activeBlock.VisualChunk, ActiveChunk.CustomHunkIndex, visualPatch.VisualDiff);
				}
				if (base.DiffViewMode == DiffViewMode.SideBySideOld)
				{
					VisualDiff visualDiff2 = Sibling.VisualPatch.VisualDiff;
					VisualChunk activeDstVisualChunk = IReadOnlyListExtensions.FirstItem(visualDiff2.VisualChunks, (VisualChunk x) => x.Node == activeBlock.VisualChunk.Node);
					int customHunkIndex = activeBlock.CustomHunkIndex;
					return visualDiff2.GetPatchLines(activeDstVisualChunk, customHunkIndex, visualDiff);
				}
				Range range = activeBlock.VisualChunk.CustomHunks[activeBlock.CustomHunkIndex];
				int start = activeBlock.VisualChunk.VisualLines[range.Start].Range.Start;
				int end = activeBlock.VisualChunk.VisualLines[range.End - 1].Range.End;
				return visualDiff.GetPatchLines(new Range(start, end));
			}
			return null;
		}

		public void Sync(CommitCodeEditor rightDiffCodeEditor)
		{
			Sibling = rightDiffCodeEditor;
			rightDiffCodeEditor.Sibling = this;
			if (rightDiffCodeEditor._diffSelectionLayer is SideBySideCommitDiffSelectionLayer sideBySideCommitDiffSelectionLayer)
			{
				sideBySideCommitDiffSelectionLayer.Sibling = _diffSelectionLayer as SideBySideCommitDiffSelectionLayer;
			}
			if (_diffSelectionLayer is SideBySideCommitDiffSelectionLayer sideBySideCommitDiffSelectionLayer2)
			{
				sideBySideCommitDiffSelectionLayer2.Sibling = rightDiffCodeEditor._diffSelectionLayer as SideBySideCommitDiffSelectionLayer;
			}
		}

		// 阶段 4 里程碑 4.7-a：WPF OnPreviewKeyDown → Avalonia OnKeyDown（Avalonia 无 Preview 前缀，
		// 隧道事件用 AddHandler(RoutingStrategies.Tunnel) 注册）。KeyEventArgs.KeyModifiers 替代
		// WPF Keyboard.IsKeyDown 静态查询。
		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (base.IsSearchBarFocused)
			{
				base.OnKeyDown(e);
				return;
			}
			bool flag = e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift);
			if (base.SelectionLength > 0 && (e.Key == Key.Return || (flag && e.Key == Key.S) || (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)))
			{
				if (!e.KeyModifiers.HasFlag(KeyModifiers.Alt))
				{
					this.ToggleStage?.Invoke(this, this);
					e.Handled = true;
				}
				return;
			}
			if (base.SelectionLength > 0 && (e.Key == Key.Delete || (flag && e.Key == Key.D)))
			{
				this.Discard?.Invoke(this, this);
				e.Handled = true;
			}
			base.OnKeyDown(e);
		}
	}
}
