using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.Settings;
using AvaloniaEdit.Document;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	public class DiffCodeEditor : CodeEditor
	{
		private const string SrcBlockPathName = "SrcBlockPath";

		private const string DstBlockPathName = "DstBlockPath";

		[Null]
		private VisualPatch _visualPatch;

		private readonly DiffBackgroundColorizer _backgroundColorizer;

		private readonly DiffTextColorizer _textColorizer;

		private readonly SyntaxHighlighting _syntaxHighlighting;

		private readonly DiffLineNumberMargin _diffLineNumberMargin;

		public DiffViewMode DiffViewMode { get; }

		[Null]
		public VisualPatch VisualPatch
		{
			get
			{
				return _visualPatch;
			}
			set
			{
				if (_visualPatch == value)
				{
					return;
				}
				_visualPatch = value;
				_diffLineNumberMargin.UpdateLineNumbersData(_visualPatch);
				_textColorizer.HunkHeaderLines = null;
				_backgroundColorizer.HighlightingSource = null;
				base.Text = _visualPatch?.StringValue ?? string.Empty;
				VisualPatch visualPatch = _visualPatch;
				if (visualPatch != null)
				{
					_textColorizer.HunkHeaderLines = visualPatch.HighlightingScheme.ServiceRegions.Map((Range r) => base.Document.GetLineByOffset(r.Start).LineNumber);
					_backgroundColorizer.HighlightingSource = CreateBackgroundHighlightingSource(visualPatch.HighlightingScheme);
				}
				else
				{
					_textColorizer.HunkHeaderLines = null;
					_backgroundColorizer.HighlightingSource = null;
				}
				RefreshSyntaxHighlighting();
				RefreshScrollbarMap();
				InvalidateVisual();
			}
		}

		public DiffCodeEditor()
			: this(DiffViewMode.Split)
		{
		}

		public DiffCodeEditor(DiffViewMode diffViewMode)
		{
			// 阶段 4 里程碑 4.7-a：WPF SetResourceReference(StyleProperty, typeof(CodeEditor)) →
			// 移除。Avalonia 通过 App.Styles 的类型选择器（Selector="controls:CodeEditor"）自动应用，
			// 无需运行时资源引用。CodeEditor 的样式由 ControlTheme 在 Generic.axaml 中定义。
			DiffViewMode = diffViewMode;
			_backgroundColorizer = new DiffBackgroundColorizer();
			base.TextArea.TextView.BackgroundRenderers.Add(_backgroundColorizer);
			_textColorizer = new DiffTextColorizer();
			base.TextArea.TextView.LineTransformers.Add(_textColorizer);
			_syntaxHighlighting = new SyntaxHighlighting();
			base.TextArea.TextView.LineTransformers.Add(_syntaxHighlighting);
			_diffLineNumberMargin = new DiffLineNumberMargin(diffViewMode);
			base.TextArea.LeftMargins.Add(_diffLineNumberMargin);
			// 阶段 4 里程碑 4.7-a：WeakEventManager → 直接事件订阅。阶段 6 改用 WeakEvent。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
			NotificationCenter.Current.DisableSyntaxHighlightingChanged += DisableSyntaxHighlightingChanged;
		}

		// 阶段 4 里程碑 4.7-a：WPF OnRenderSizeChanged(SizeChangedInfo) → Avalonia Layoutable.OnSizeChanged。
		// Avalonia 无 OnRenderSizeChanged 虚方法；Layoutable.OnSizeChanged 在 Bounds 改变时触发。
		protected override void OnSizeChanged(SizeChangedEventArgs e)
		{
			base.OnSizeChanged(e);
			RefreshScrollbarMap();
		}

		private void RefreshScrollbarMap()
		{
			if (base.VerticalScrollBarVisibility == ScrollBarVisibility.Hidden || (DiffViewMode != DiffViewMode.SideBySideNew && DiffViewMode != DiffViewMode.Split) || !base.Template.TryFindName<Path>("SrcBlockPath", this, out var match) || !base.Template.TryFindName<Path>("DstBlockPath", this, out var match2))
			{
				return;
			}
			double defaultLineHeight = base.TextArea.TextView.DefaultLineHeight;
			if (defaultLineHeight > 0.0 && base.TextArea.ActualHeight > 0.0)
			{
				double num = base.TextArea.ActualHeight / defaultLineHeight;
				if ((double)base.Document.LineCount <= num)
				{
					match.Data = null;
					match2.Data = null;
					return;
				}
			}
			StreamGeometry streamGeometry = new StreamGeometry();
			streamGeometry.FillRule = FillRule.Nonzero;
			// 阶段 4 里程碑 4.7-a：WPF StreamGeometryContext.Close() → Avalonia using 声明（IDisposable）。
			// WPF StreamGeometry.Freeze() → 移除（Avalonia 几何体在 context dispose 后即不可变）。
			using StreamGeometryContext streamGeometryContext = streamGeometry.Open();
			StreamGeometry streamGeometry2 = new StreamGeometry();
			streamGeometry2.FillRule = FillRule.Nonzero;
			using StreamGeometryContext streamGeometryContext2 = streamGeometry2.Open();
			int width = ((DiffViewMode == DiffViewMode.Split) ? 6 : 4);
			int x = ((DiffViewMode == DiffViewMode.Split) ? 1 : 0);
			int x2 = ((DiffViewMode == DiffViewMode.Split) ? 1 : 4);
			VisualPatch visualPatch = VisualPatch;
			if (visualPatch != null)
			{
				int lineCount = visualPatch.VisualDiff.LineCount;
				VisualChunk[] visualChunks = visualPatch.VisualDiff.VisualChunks;
				foreach (VisualChunk visualChunk in visualChunks)
				{
					VisualSubChunk[] visualSubChunks = visualChunk.VisualSubChunks;
					foreach (VisualSubChunk visualSubChunk in visualSubChunks)
					{
						int num2 = GetChangeLineNumber(visualChunk.VisualLines, visualSubChunk);
						int length = visualSubChunk.Node.Deleted.Length;
						if (length > 0)
						{
							AddLine(streamGeometryContext, num2, length, lineCount, x, width);
							if (DiffViewMode == DiffViewMode.Split)
							{
								num2 += length;
							}
						}
						int length2 = visualSubChunk.Node.Added.Length;
						if (length2 > 0)
						{
							AddLine(streamGeometryContext2, num2, length2, lineCount, x2, width);
						}
					}
				}
			}
			match.Data = streamGeometry;
			match2.Data = streamGeometry2;
		}

		private int GetChangeLineNumber(VisualLine[] visualLines, VisualSubChunk visualSubChunk)
		{
			return visualLines[visualSubChunk.DeletedLines.Start].LineNumber;
		}

		private void AddLine(StreamGeometryContext ctx, int startLine, int blockLength, int totalLines, int x, int width)
		{
			double num = 12.0;
			double num2 = base.TextArea.ActualHeight - num * 2.0;
			double num3 = num + num2 * ((double)startLine / (double)totalLines);
			double num4 = Math.Max(2.0, num2 * ((double)blockLength / (double)totalLines));
			// 阶段 4 里程碑 4.7-a：WPF BeginFigure(p, isFilled, isClosed) / PolyLineTo(pts, isStroked, isSmoothJoin) →
			// Avalonia BeginFigure(p, isFilled) / PolyLineTo(pts, isStroked) + EndFigure(isClosed)。
			ctx.BeginFigure(new Point(x, num3), isFilled: true);
			ctx.PolyLineTo(new Point[3]
			{
				new Point(x + width, num3),
				new Point(x + width, num3 + num4),
				new Point(x, num3 + num4)
			}, isStroked: false);
			ctx.EndFigure(isClosed: true);
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			base.TextArea.TextView.Redraw();
		}

		private void DisableSyntaxHighlightingChanged(object sender, EventArgs<bool> e)
		{
			RefreshSyntaxHighlighting();
			base.TextArea.TextView.Redraw();
		}

		private void RefreshSyntaxHighlighting()
		{
			if (!ForkPlusSettings.Default.DisableSyntaxHighlighting)
			{
				VisualPatch visualPatch = VisualPatch;
				if (visualPatch != null && !visualPatch.VisualDiff.Node.IsMinified)
				{
					string filepath = visualPatch.VisualDiff.Node.OldFilepath ?? visualPatch.VisualDiff.Node.NewFilepath;
					Range[] ranges = visualPatch.VisualDiff.VisualChunks.Map((VisualChunk x) => x.InnerRange);
					_syntaxHighlighting.Highlight(filepath, base.Text, ranges);
					return;
				}
			}
			_syntaxHighlighting.Clear();
		}

		private static HighlightingSource[] CreateBackgroundHighlightingSource(HighlightingScheme scheme)
		{
			List<HighlightingSource> list = new List<HighlightingSource>();
			list.AddRange(GetBackgroundHighlightingSource(scheme.AddRegions, HighlightingType.Add));
			list.AddRange(GetBackgroundHighlightingSource(scheme.RemoveRegions, HighlightingType.Remove));
			list.AddRange(GetBackgroundHighlightingSource(scheme.AlignmentRegions, HighlightingType.Alignment));
			list.AddRange(GetBackgroundHighlightingSource(scheme.ExtraAddRegions, HighlightingType.ExactAdd));
			list.AddRange(GetBackgroundHighlightingSource(scheme.ExtraRemoveRegions, HighlightingType.ExactRemove));
			return list.ToArray();
		}

		private static List<HighlightingSource> GetBackgroundHighlightingSource(Range[] regions, HighlightingType highlightingType)
		{
			List<HighlightingSource> list = new List<HighlightingSource>();
			for (int i = 0; i < regions.Length; i++)
			{
				Range range = regions[i];
				if (range.Length >= 0)
				{
					TextSegment segment = new TextSegment
					{
						StartOffset = range.Start,
						EndOffset = range.Start + range.Length
					};
					list.Add(new HighlightingSource(segment, highlightingType));
				}
			}
			return list;
		}
	}
}
