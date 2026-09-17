// 临时探针（用后即删）：CJK 行末光标 x 位置随 U+0020 替换字形 advance 的变化。
using System;
using System.Text;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class FontCaretProbeTests
	{
		[Fact]
		public void Probe_CjkLineEnd_CaretPositions()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sb = new StringBuilder();
				const string text = "中文测试\r\nqweqwe";
				var layout = new TextLayout(text,
					new Typeface("Consolas"),
					13,
					Brushes.Black,
					Avalonia.Media.TextAlignment.Left,
					TextWrapping.NoWrap);
				Dispatcher.UIThread.RunJobs();
				sb.AppendLine("text=[" + text.Replace("\r", "\\r").Replace("\n", "\\n") + "] len=" + text.Length
					+ " lines=" + layout.TextLines.Count);
				for (int i = 0; i <= text.Length; i++)
				{
					var r = layout.HitTestTextPosition(i);
					sb.AppendLine("hit(" + i + ") char=" + (i < text.Length ? "U+" + ((int)text[i]).ToString("X4") : "END")
						+ " rect=" + r.X.ToString("F2") + "," + r.Y.ToString("F2") + " " + r.Width.ToString("F2") + "x" + r.Height.ToString("F2"));
				}
				return sb.ToString();
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText(@"C:\Users\H00518~1\AppData\Local\Temp\opencode\caret_probe.txt", report, System.Text.Encoding.UTF8);
		}
	}
}
