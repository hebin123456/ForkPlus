using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using ICSharpCode.AvalonEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Merge
{
	public class MergeCodeEditor : CodeEditor
	{
		private struct Block
		{
			public enum BlockKind
			{
				Resolved,
				Unresolved
			}

			public double Start { get; }

			public double Length { get; }

			public BlockKind Kind { get; }

			public Block(double start, double length, BlockKind kind)
			{
				Start = start;
				Length = length;
				Kind = kind;
			}
		}

		private const string SrcBlockPathName = "SrcBlockPath";

		private const string DstBlockPathName = "DstBlockPath";

		private MergeCodeEditorBackgroundColorizer _backgroundColorizer;

		private MergeLineNumberMargin _lineNumberMargin;

		private MergeChunkSelectionLayer _mergeChunkSelectionLayer;

		private bool _showScrollbarMap;

		private bool _refreshUI;

		private Block[] _blocks;

		private MergeConflictView _mergeConflictView;

		public ThemeType Theme { get; }

		public MergeConflictPart ViewMode { get; set; }

		public MergeConflictView MergeConflictView
		{
			get
			{
				return _mergeConflictView;
			}
			private set
			{
				if (_mergeConflictView != value)
				{
					_mergeConflictView = value;
				}
			}
		}

		public event EventHandler<EventArgs<int>> MergeLineAdded;

		public event EventHandler<EventArgs<int>> MergeLineRemoved;

		public event EventHandler<EventArgs<MergeConflictView.Chunk>> MergeChunkAdded;

		public event EventHandler<EventArgs<MergeConflictView.Chunk>> MergeChunkRemoved;

		public MergeCodeEditor()
		{
			SetResourceReference(FrameworkElement.StyleProperty, typeof(CodeEditor));
			Theme = ForkPlusSettings.Default.Theme;
			_mergeChunkSelectionLayer = new MergeChunkSelectionLayer(this);
			base.TextArea.TextView.InsertLayer(_mergeChunkSelectionLayer, KnownLayer.Selection, LayerInsertionPosition.Above);
			_backgroundColorizer = new MergeCodeEditorBackgroundColorizer(this);
			base.TextArea.TextView.BackgroundRenderers.Add(_backgroundColorizer);
			_lineNumberMargin = new MergeLineNumberMargin(this);
			base.TextArea.LeftMargins.Add(_lineNumberMargin);
		}

		public void SetMergeConflictView(MergeConflictView mergeConflictView, bool refreshUI, bool showScrollbarMap = false)
		{
			_refreshUI = refreshUI;
			_showScrollbarMap = showScrollbarMap;
			MergeConflictView = mergeConflictView;
			if (refreshUI)
			{
				_lineNumberMargin.UpdateLineNumbersData(_mergeConflictView);
				base.Text = _mergeConflictView?.StringValue ?? string.Empty;
				InvalidateVisual();
				if (showScrollbarMap)
				{
					_blocks = CreateBlocks(mergeConflictView.Chunks);
					RefreshScrollbarMap();
				}
			}
		}

		public void InvalidateMargin()
		{
			_lineNumberMargin.InvalidateVisual();
		}

		public void OnMergeLineAdded(int lineNumber)
		{
			this.MergeLineAdded?.Invoke(this, new EventArgs<int>(lineNumber));
		}

		public void OnMergeLineRemoved(int lineNumber)
		{
			this.MergeLineRemoved?.Invoke(this, new EventArgs<int>(lineNumber));
		}

		public void OnMergeChunkAdded(MergeConflictView.Chunk chunk)
		{
			this.MergeChunkAdded?.Invoke(this, new EventArgs<MergeConflictView.Chunk>(chunk));
		}

		public void OnMergeChunkRemoved(MergeConflictView.Chunk chunk)
		{
			this.MergeChunkRemoved?.Invoke(this, new EventArgs<MergeConflictView.Chunk>(chunk));
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			if (_refreshUI && _showScrollbarMap)
			{
				RefreshScrollbarMap();
			}
		}

		private void RefreshScrollbarMap()
		{
			if (_blocks != null && base.VerticalScrollBarVisibility != ScrollBarVisibility.Hidden && base.Template.TryFindName<Path>("SrcBlockPath", this, out var match) && base.Template.TryFindName<Path>("DstBlockPath", this, out var match2))
			{
				StreamGeometry streamGeometry = new StreamGeometry();
				streamGeometry.FillRule = FillRule.Nonzero;
				StreamGeometryContext streamGeometryContext = streamGeometry.Open();
				StreamGeometry streamGeometry2 = new StreamGeometry();
				streamGeometry2.FillRule = FillRule.Nonzero;
				StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open();
				int num = 6;
				int num2 = 1;
				double num3 = 12.0;
				double num4 = base.TextArea.ActualHeight - num3 * 2.0;
				Block[] blocks = _blocks;
				for (int i = 0; i < blocks.Length; i++)
				{
					Block block = blocks[i];
					double num5 = num3 + num4 * block.Start;
					double num6 = Math.Max(2.0, num4 * block.Length);
					StreamGeometryContext obj = ((block.Kind == Block.BlockKind.Resolved) ? streamGeometryContext2 : streamGeometryContext);
					obj.BeginFigure(new Point(num2, num5), isFilled: true, isClosed: true);
					obj.PolyLineTo(new Point[3]
					{
						new Point(num2 + num, num5),
						new Point(num2 + num, num5 + num6),
						new Point(num2, num5 + num6)
					}, isStroked: false, isSmoothJoin: false);
				}
				streamGeometryContext.Close();
				streamGeometry.Freeze();
				streamGeometryContext2.Close();
				streamGeometry2.Freeze();
				match.Data = streamGeometry;
				match2.Data = streamGeometry2;
			}
		}

		private static Block[] CreateBlocks(MergeConflictView.Chunk[] chunks)
		{
			List<Block> list = new List<Block>();
			int num = chunks.Map((MergeConflictView.Chunk x) => x.LineRange.Length).Sum();
			foreach (MergeConflictView.Chunk chunk in chunks)
			{
				if (chunk.Node is MergeConflict.ConflictChunk conflictChunk)
				{
					int start = chunk.LineRange.Start;
					int num2 = conflictChunk.RemoteLines.Filter((MergeConflict.SelectableLine x) => x.IsSelected).Count + conflictChunk.LocalLines.Filter((MergeConflict.SelectableLine x) => x.IsSelected).Count;
					int num3 = 0;
					int num4 = 0;
					Block.BlockKind kind;
					if (num2 > 0)
					{
						num3 = num2;
						num4 = num2;
						kind = Block.BlockKind.Resolved;
					}
					else
					{
						num3 = conflictChunk.RemoteLines.Length;
						num4 = conflictChunk.LocalLines.Length;
						kind = Block.BlockKind.Unresolved;
					}
					if (num3 > 0)
					{
						list.Add(new Block((double)start / (double)num, (double)num3 / (double)num, kind));
					}
					if (num4 > 0)
					{
						list.Add(new Block((double)start / (double)num, (double)num4 / (double)num, kind));
					}
				}
			}
			return list.ToArray();
		}
	}
}
