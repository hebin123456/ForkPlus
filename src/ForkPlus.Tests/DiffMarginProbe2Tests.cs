// 探针2：验证 fallback 字体（Consolas 缺失时）数字宽度是否等宽
// 若非等宽，个别行号数字串会比测量用的 "9999" 宽 → 行号被代码区遮挡
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiffMarginProbe2Tests
	{
		[Fact]
		public void Probe_FontFallback_DigitWidths()
		{
			string report = HeadlessAppBootstrap.Run(delegate
			{
				var sb = new System.Text.StringBuilder();
				try
				{
					// 当前代码的 typeface（无 fallback 声明）
					var currentTf = new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.Normal);
					// 修复后：等宽 fallback 链
					var fixedTf = new Typeface(new FontFamily("Consolas, Courier New, monospace"), FontStyles.Normal, FontWeights.Normal);

					sb.AppendLine("== current (Consolas only) ==");
					Probe(sb, currentTf);
					sb.AppendLine("== fixed (Consolas, Courier New, monospace) ==");
					Probe(sb, fixedTf);
				}
				catch (Exception e)
				{
					sb.AppendLine("EXCEPTION: " + e);
				}
				return sb.ToString();
			});
			System.IO.File.WriteAllText("/tmp/diff_margin_probe2.txt", report);
			Assert.True(true, report);
		}

		private static void Probe(System.Text.StringBuilder sb, Typeface tf)
		{
			double measure = new FormattedText("9999", CultureInfo.InvariantCulture, FlowDirection.RightToLeft, tf, 11.0, Brushes.Black).Width;
			sb.AppendLine($"measure '9999' = {measure:F2}");
			string[] samples = { "1", "1234", "4444", "7777", "1000", "2026", "4406", "3123", "5999", "0189" };
			double maxDiff = 0;
			foreach (var s in samples)
			{
				double w = new FormattedText(s.PadLeft(4), CultureInfo.InvariantCulture, FlowDirection.RightToLeft, tf, 11.0, Brushes.Black).Width;
				double overflow = w - measure;
				maxDiff = Math.Max(maxDiff, overflow);
				sb.AppendLine($"  '{s.PadLeft(4)}' width = {w:F2} (overflow {overflow:+0.00;-0.00})");
			}
			sb.AppendLine($"max overflow vs measure = {maxDiff:F2}");
		}
	}
}
