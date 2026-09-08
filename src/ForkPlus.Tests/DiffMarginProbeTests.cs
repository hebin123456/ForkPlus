// 临时探针：验证 DiffLineNumberMargin 行号空间不足的根因假设
// 1) RTL FormattedText 宽度 vs LTR 宽度
// 2) DiffCodeEditor 渲染后 margin 实际 Bounds.Width vs 行号绘制所需空间
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiffMarginProbeTests
	{
		[Fact]
		public void Probe_RtlFormattedTextWidth_And_MarginLayout()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					var typeface = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal);
					double ltr4 = new FormattedText("9999", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, 11.0, Brushes.Black).Width;
					double rtl4 = new FormattedText("9999", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, 11.0, Brushes.Black).Width;
					sb.AppendLine($"LTR '9999' width = {ltr4:F2}, RTL '9999' width = {rtl4:F2}");

					// 实际布局：SplitTextDiffControl + 简单文本（无 VisualPatch 时 _lineNumberLength=2）
					var control = new SplitTextDiffControl();
					var window = new Window { Width = 800, Height = 300, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					var editor = control.GetType().GetField("_editor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(control) as ForkPlus.UI.Controls.Editor.CodeEditor;
					editor.Text = "line1\nline2\nline3\nline4\nline5";
					Dispatcher.UIThread.RunJobs();

					// 找 margin
					var margins = editor.TextArea.LeftMargins;
					sb.AppendLine($"LeftMargins count = {margins.Count}");
					foreach (var m in margins)
					{
						sb.AppendLine($"  margin {m.GetType().Name}: Bounds={m.Bounds}, Desired={m.DesiredSize}");
					}

					// TextView 起点（相对 TextArea）
					var textView = editor.TextArea.TextView;
					var textAreaView = editor.TextArea as Visual;
					var tvPos = textView.TranslatePoint(new Point(0, 0), textAreaView);
					sb.AppendLine($"TextView pos in TextArea = {tvPos}");

					// 各 margin 右边界
					double marginRight = 0;
					foreach (var m in margins)
					{
						var p = (m as Visual).TranslatePoint(new Point(m.Bounds.Width, 0), textAreaView);
						sb.AppendLine($"  margin {m.GetType().Name} right-edge in TextArea = {p}");
					}

					// 测量数字宽度（margin 内部用 11pt）：4 位行号
					double digit2 = new FormattedText("99", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, typeface, 11.0, Brushes.Black).Width;
					sb.AppendLine($"digit width '99' = {digit2:F2}");

					window.Close();
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			});
			System.IO.File.WriteAllText("/tmp/diff_margin_probe.txt", report);
			// 输出报告，任何断言失败都会显示 report
			Assert.True(true, report);
		}
	}
}
