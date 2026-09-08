// 探针3：带真实 VisualPatch（4 位行号）测量 margin 宽度是否正确增长，
// 以及行号绘制右边界 vs margin 实际宽度 vs TextView 起点
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiffMarginProbe3Tests
	{
		[Fact]
		public void Probe_RealVisualPatch_MarginWidth()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					// 构造带 4 位行号的 diff：FromStart=1000, ToStart=2000
					// lines: 3 context + 1 deleted + 1 added + 3 context
					// SubChunk ranges 基于 lines 数组下标
					var lines = new[]
					{
						"context line 1",   // 0
						"context line 2",   // 1
						"context line 3",   // 2
						"deleted line",     // 3
						"added line",       // 4
						"context line 4",   // 5
						"context line 5",   // 6
						"context line 6"    // 7
					};
					// Split 视图的行结构：ctx,ctx,ctx,del,add,ctx,ctx,ctx → 8 行
					var subChunk = new SubChunk(
						new Range(0, 3),      // preContext: lines 0-2
						new Range(3, 1),      // deleted: line 3
						new Range(4, 1),      // added: line 4
						new Range(5, 3),      // postContext: lines 5-7
						NoNewLineAtEndOfFile.None);
					var chunk = new Chunk(1000, 5, 2000, 5, null, new[] { subChunk });
					var diff = new Diff("a.txt", "a.txt", null, null, "1111111", "2222222", lines, new[] { chunk }, null, Diff.FileType.Text, false);

					var control = new SplitTextDiffControl();
					var window = new Window { Width = 900, Height = 400, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					control.SetDiff(diff, 4, false, DiffLocation.Unstaged);
					Dispatcher.UIThread.RunJobs();

					var editor = (CodeEditor)control.GetType().GetField("_editor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(control);
					var textView = editor.TextArea.TextView;
					var textArea = editor.TextArea as Visual;

					foreach (var m in editor.TextArea.LeftMargins)
					{
						sb.AppendLine($"margin {m.GetType().Name}: Bounds={m.Bounds}, Desired={m.DesiredSize}");
					}
					var tvPos = textView.TranslatePoint(new Point(0, 0), textArea);
					sb.AppendLine($"TextView x-in-TextArea = {tvPos?.X}");

					// margin 内部公式需要的宽度（4 位行号 Split：digits(8) + 21 + 0 + 6）
					var tf = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal);
					double digits8 = new FormattedText(new string('9', 8), CultureInfo.InvariantCulture, FlowDirection.RightToLeft, tf, 11.0, Brushes.Black).Width;
					double digits4 = new FormattedText("9999", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, tf, 11.0, Brushes.Black).Width;
					sb.AppendLine($"expected Split width(4-digit) = {digits8 + 21 + 0 + 6:F2} (digits8={digits8:F2})");
					sb.AppendLine($"one 4-digit group width = {digits4:F2}");

					// 行号绘制右边界（to 侧）应 = W - 7，需要能容纳 4 位数字
					var margin = editor.TextArea.LeftMargins[0] as Visual;
					double W = margin.Bounds.Width;
					sb.AppendLine($"to-linenumber right edge = {W - 7:F2}, digits4={digits4:F2} → 占 [{W - 7 - digits4:F2}, {W - 7:F2}]，margin W={W:F2}");

					// 文本第一行绘制位置：TextView 内偏移 0 → 编辑器坐标 tvPos.X
					sb.AppendLine($"first code char starts at editor-x = {tvPos?.X}");
					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			});
			System.IO.File.WriteAllText("/tmp/diff_margin_probe3.txt", report);
			Assert.True(true, report);
		}
	}
}
