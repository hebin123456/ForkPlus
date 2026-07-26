using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using AvaloniaEdit.Rendering;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.VisualTree;

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

	// 阶段 6：缓存模板内的 SrcBlockPath / DstBlockPath（替代 GetTemplateChildren 占位）。
	// 在 OnApplyTemplate 中通过 e.NameScope.Find 查找，RefreshScrollbarMap 直接使用缓存。
	[Null]
	private Path _srcBlockPath;
	[Null]
	private Path _dstBlockPath;

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
			// 阶段 4 里程碑 4.7-a：WPF SetResourceReference(StyleProperty, typeof(CodeEditor)) →
			// 移除。Avalonia 通过 App.Styles 的类型选择器自动应用 ControlTheme。
			Theme = ForkPlusSettings.Default.Theme;
			_mergeChunkSelectionLayer = new MergeChunkSelectionLayer(this);
			base.TextArea.TextView.InsertLayer(_mergeChunkSelectionLayer, KnownLayer.Selection, LayerInsertionPosition.Above);
			_backgroundColorizer = new MergeCodeEditorBackgroundColorizer(this);
			base.TextArea.TextView.BackgroundRenderers.Add(_backgroundColorizer);
			_lineNumberMargin = new MergeLineNumberMargin(this);
			base.TextArea.LeftMargins.Add(_lineNumberMargin);
		}

		// 阶段 6：override OnApplyTemplate 缓存模板内的 SrcBlockPath / DstBlockPath。
		// 替代阶段 5 的 GetTemplateChildren() 占位（该方法返回空集合，导致 scrollbar map 永不刷新）。
		// Avalonia 标准做法：在 OnApplyTemplate 中用 e.NameScope.Find(name) 查找 PART_* 元素。
		protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_srcBlockPath = e.NameScope?.Find(SrcBlockPathName) as Path;
			_dstBlockPath = e.NameScope?.Find(DstBlockPathName) as Path;
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

		// 阶段 4 里程碑 4.7-a：WPF OnRenderSizeChanged(SizeChangedInfo) → Avalonia Layoutable.OnSizeChanged。
		protected override void OnSizeChanged(SizeChangedEventArgs e)
		{
			base.OnSizeChanged(e);
			if (_refreshUI && _showScrollbarMap)
			{
				RefreshScrollbarMap();
			}
		}

		private void RefreshScrollbarMap()
		{
			// 阶段 6：使用 OnApplyTemplate 中缓存的 SrcBlockPath / DstBlockPath（替代阶段 5 的 GetTemplateChildren 占位）。
			// 模板未应用或缺失时直接返回（与原 match == null / match2 == null 检查等价）。
			Path match = _srcBlockPath;
			Path match2 = _dstBlockPath;
			if (_blocks == null || base.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || match == null || match2 == null)
			{
				return;
			}
			StreamGeometry streamGeometry = new StreamGeometry();
			// NOTE(4.5): Avalonia StreamGeometry 不直接暴露 FillRule。
			// 原 WPF streamGeometry.FillRule = FillRule.NonZero 已注释，路径填充规则退化为默认 EvenOdd。
			// 当前 scrollbar map 用 BeginFigure + LineTo + EndFigure(isClosed:true) 构造矩形，
			// 矩形无自相交，EvenOdd 与 NonZero 渲染一致，无视觉差异。
			// 阶段 4 里程碑 4.7-a：WPF StreamGeometryContext.Close() → Avalonia using 声明（IDisposable）。
			// WPF StreamGeometry.Freeze() → 移除（Avalonia 几何体在 context dispose 后即不可变）。
			using StreamGeometryContext streamGeometryContext = streamGeometry.Open();
			StreamGeometry streamGeometry2 = new StreamGeometry();
			using StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open();
			int num = 6;
			int num2 = 1;
			double num3 = 12.0;
			double num4 = base.TextArea.Bounds.Height - num3 * 2.0;
			Block[] blocks = _blocks;
			for (int i = 0; i < blocks.Length; i++)
			{
				Block block = blocks[i];
				double num5 = num3 + num4 * block.Start;
				double num6 = Math.Max(2.0, num4 * block.Length);
				StreamGeometryContext obj = ((block.Kind == Block.BlockKind.Resolved) ? streamGeometryContext2 : streamGeometryContext);
				// 阶段 4 里程碑 4.7-a：WPF PolyLineTo(pts, isStroked, isSmoothJoin) → Avalonia 循环 LineTo(pt)（LineTo 无 isStroked 参数）。
				obj.BeginFigure(new Point(num2, num5), isFilled: true);
				Point[] pts = new Point[3]
				{
					new Point(num2 + num, num5),
					new Point(num2 + num, num5 + num6),
					new Point(num2, num5 + num6)
				};
				foreach (Point pt in pts)
				{
					obj.LineTo(pt);
				}
				obj.EndFigure(isClosed: true);
			}
			match.Data = streamGeometry;
			match2.Data = streamGeometry2;
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
