using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using ForkPlus.UI.Controls.Editor;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiagFontProbeTests
	{
		private const string ReportPath = @"C:\Users\H00518~1\AppData\Local\Temp\opencode\diag_font.txt";

		[Fact]
		public void Diag_CjkTypefaceProbe()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sb = new StringBuilder();
				try
				{
					// 1) Avalonia TextLayout 直接排版，看 CJK / ASCII 行高
					var layoutCjk = new TextLayout("中", new Typeface(FontFamily.Parse("Consolas")), 13.0);
					sb.AppendLine("TextLayout(Consolas,中) height=" + layoutCjk.TextLines[0].Height.ToString("F3")
						+ " baseline=" + layoutCjk.TextLines[0].Baseline.ToString("F3"));
					foreach (var tr0 in layoutCjk.TextLines[0].TextRuns)
					{
						sb.AppendLine("  [TL run] type=" + tr0.GetType().Name
							+ " font=" + (tr0.Properties != null && tr0.Properties.Typeface != default(Typeface) ? tr0.Properties.Typeface.FontFamily.Name : "(default tf)")
							+ " em=" + (tr0.Properties != null ? tr0.Properties.FontRenderingEmSize.ToString() : "?"));
						if (tr0 is ShapedTextRun s0)
						{
							sb.AppendLine("    TextMetrics: ascent=" + s0.TextMetrics.Ascent.ToString("F3")
								+ " descent=" + s0.TextMetrics.Descent.ToString("F3")
								+ " lineGap=" + s0.TextMetrics.LineGap.ToString("F3")
								+ " lineHeight=" + s0.TextMetrics.LineHeight.ToString("F3"));
						}
					}
					var layoutX = new TextLayout("x", new Typeface(FontFamily.Parse("Consolas")), 13.0);
					sb.AppendLine("TextLayout(Consolas,x) height=" + layoutX.TextLines[0].Height.ToString("F3")
						+ " baseline=" + layoutX.TextLines[0].Baseline.ToString("F3"));

					// 2) 内嵌字体本身
					try
					{
						var embedded = new Typeface(FontFamily.Parse(ForkPlus.FontSetup.CjkFallbackFontUri));
						sb.AppendLine("embedded family=" + embedded.FontFamily.Name
							+ " metrics: " + DumpMetrics(embedded));
						var layoutE = new TextLayout("中", embedded, 13.0);
						sb.AppendLine("TextLayout(embedded,中) height=" + layoutE.TextLines[0].Height.ToString("F3"));
					}
					catch (Exception ex)
					{
						sb.AppendLine("embedded load FAILED: " + ex.GetType().Name + ": " + ex.Message);
					}

					// 3) CodeEditor 真实排版 CJK
					var probe = new CodeEditor();
					probe.FontSize = 13.0;
					var window = new Window { Width = 400, Height = 200, Content = probe };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					probe.Text = "中\n";
					Dispatcher.UIThread.RunJobs();
					var vl = probe.TextArea.TextView.VisualLines[0];
					sb.AppendLine("CodeEditor CJK line height=" + vl.TextLines[0].Height.ToString("F3")
						+ " DefaultLineHeight=" + probe.TextArea.TextView.DefaultLineHeight.ToString("F3"));
					foreach (var tl in vl.TextLines)
					{
						foreach (var tr in tl.TextRuns)
						{
							var props = tr.Properties;
							if (props?.Typeface != null)
							{
								sb.AppendLine("run font=" + props.Typeface.FontFamily.Name
									+ " " + DumpMetrics(props.Typeface));
							}
							if (tr is ShapedTextRun str)
							{
								sb.AppendLine("  ShapedTextRun.TextMetrics: ascent=" + str.TextMetrics.Ascent.ToString("F3")
									+ " descent=" + str.TextMetrics.Descent.ToString("F3")
									+ " lineGap=" + str.TextMetrics.LineGap.ToString("F3")
									+ " lineHeight=" + str.TextMetrics.LineHeight.ToString("F3")
									+ " baseline=" + str.TextMetrics.Baseline.ToString("F3"));
								var cached = props?.GetType().GetProperty("CachedGlyphTypeface")?.GetValue(props);
								if (cached != null)
								{
									var cm = cached.GetType().GetProperty("Metrics")?.GetValue(cached);
									if (cm != null)
									{
										var t = cm.GetType();
										sb.AppendLine("  CachedGlyphTypeface.Metrics: ascent=" + t.GetProperty("Ascent")?.GetValue(cm)
											+ " descent=" + t.GetProperty("Descent")?.GetValue(cm)
											+ " lineGap=" + t.GetProperty("LineGap")?.GetValue(cm)
											+ " designEmHeight=" + t.GetProperty("DesignEmHeight")?.GetValue(cm));
									}
									var fam = cached.GetType().GetProperty("FamilyName")?.GetValue(cached);
									sb.AppendLine("  CachedGlyphTypeface.FamilyName=" + fam);
								}
							}
							else
							{
								sb.AppendLine("run: (not shaped) type=" + tr.GetType().Name
									+ " baseline=" + ((tr as DrawableTextRun)?.Baseline.ToString("F3") ?? "?")
									+ " size=" + ((tr as DrawableTextRun)?.Size.ToString() ?? "?"));
							}
						}
					}
					window.Close();
				}
				catch (Exception ex)
				{
					sb.AppendLine("EXCEPTION: " + ex);
				}
				return sb.ToString();
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText(ReportPath, report);
		}

		// 光标 bug 修复验证（2026-09-16）：内嵌 Noto 子集应已补零宽 U+200C(ZWNJ) 字形
		[Fact]
		public void Diag_ZwnjGlyphProbe()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sb = new StringBuilder();
				var embedded = new Typeface(FontFamily.Parse(ForkPlus.FontSetup.CjkFallbackFontUri));
				sb.AppendLine("embedded family=" + embedded.FontFamily.Name);
				var gt = embedded.GlyphTypeface;
				var cmap = gt.CharacterToGlyphMap;
				sb.AppendLine("GlyphCount=" + gt.GlyphCount);
				sb.AppendLine("cmap[0x200C ZWNJ]=" + (cmap.TryGetGlyph(0x200C, out var gz) ? "YES gid=" + gz : "NO"));
				sb.AppendLine("cmap[0xFF1A fullwidth-colon]=" + (cmap.TryGetGlyph(0xFF1A, out var gz2) ? "YES gid=" + gz2 : "NO"));
				sb.AppendLine("cmap[0x4E2D 中]=" + (cmap.TryGetGlyph(0x4E2D, out var gz3) ? "YES gid=" + gz3 : "NO"));
				sb.AppendLine("cmap[0x0020 space]=" + (cmap.TryGetGlyph(0x0020, out var gz4) ? "YES gid=" + gz4 : "NO"));
				return sb.ToString();
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText(@"C:\Users\H00518~1\AppData\Local\Temp\opencode\zwnj_probe.txt", report);
		}

		private static string DumpMetrics(Typeface tf)
		{
			try
			{
				var gt = tf.GlyphTypeface;
				var m = gt.Metrics;
				return "[GlyphTypeface.Metrics] ascent=" + m.Ascent.ToString("F3")
					+ " descent=" + m.Descent.ToString("F3")
					+ " lineGap=" + m.LineGap.ToString("F3")
					+ " family=" + gt.FamilyName;
			}
			catch (Exception ex)
			{
				return "metrics FAILED: " + ex.Message;
			}
		}
	}
}
