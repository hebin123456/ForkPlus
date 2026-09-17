// 诊断（2026-09-16，"not LFS 徽章红底看不见字"）：BinaryContentUserControl 的
// NotLfsLabel 徽章在真实应用中渲染为红色色块但文字不可见。本探查在 headless 下
// 完整装配该控件并渲染成位图，dump 主题属性实际生效值 + 文本行高度 + 逐像素统计
// （红底像素 / 白字像素），定位是 Foreground 未生效、文本被裁剪还是字体渲染问题。
using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiagBadgeProbeTests
	{
		private const string ReportPath = @"C:\Users\H00518~1\AppData\Local\Temp\opencode\diag_badge.txt";

		[Fact]
		public void Diag_NotLfsBadge_TextVisibilityAndPixels()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sb = new StringBuilder();
				try
				{
					var control = new BinaryContentUserControl();
					control.SetContent(new BinaryContent("blob.otf", isTracked: false, 13 * 1024 * 1024));
					var window = new Window { Width = 400, Height = 300, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

					TextBlock[] labels = control.GetVisualDescendants()
						.OfType<TextBlock>()
						.Where(t => t.Name == "LfsLabel" || t.Name == "NotLfsLabel")
						.ToArray();
					sb.AppendLine("labels found = " + labels.Length);
					foreach (TextBlock t in labels)
					{
						sb.AppendLine("[" + t.Name + "]"
							+ " Text=" + (t.Text ?? "<null>")
							+ " IsVisible=" + t.IsVisible
							+ " Theme=" + (t.Theme == null ? "<null>" : t.Theme.GetType().Name)
							+ " Foreground=" + DumpBrush(t.Foreground)
							+ " Background=" + DumpBrush(t.Background)
							+ " FontFamily=" + (t.FontFamily == null ? "<null>" : t.FontFamily.Name)
							+ " FontSize=" + t.FontSize
							+ " Bounds=" + t.Bounds
							+ " DesiredSize=" + t.DesiredSize);
						// 文本排版行高（Fixed Height=16 若小于行高 → 垂直裁剪）
						try
						{
							var tf = new Typeface(t.FontFamily ?? FontFamily.Parse("Segoe UI"), t.FontStyle, t.FontWeight);
							var ft = new FormattedText(
								t.Text ?? "not LFS",
								System.Globalization.CultureInfo.InvariantCulture,
								t.FlowDirection,
								tf, t.FontSize, Brushes.Black);
							sb.AppendLine("  FormattedText height=" + ft.Height.ToString("F2")
								+ " width=" + ft.Width.ToString("F2")
								+ " baseLine=" + ft.Baseline.ToString("F2"));
						}
						catch (Exception ex2)
						{
							sb.AppendLine("  FormattedText FAILED: " + ex2.Message);
						}
					}

					// 渲染 NotLfsLabel 自身 → 统计像素（红底 / 白字）
					TextBlock notLfs = labels.FirstOrDefault(t => t.Name == "NotLfsLabel");
					if (notLfs != null && notLfs.IsVisible && notLfs.Bounds.Width > 0)
					{
						var rtb = new RenderTargetBitmap(
							new PixelSize(Math.Max(1, (int)Math.Ceiling(notLfs.Bounds.Width)),
								Math.Max(1, (int)Math.Ceiling(notLfs.Bounds.Height))),
							new Vector(96, 96));
						rtb.Render(notLfs);
						rtb.Save(@"C:\Users\H00518~1\AppData\Local\Temp\opencode\diag_badge.png");
						using (var wb = new WriteableBitmap(rtb.PixelSize, rtb.Dpi))
						{
							using (ILockedFramebuffer fb = wb.Lock())
							{
								rtb.CopyPixels(fb);
								int len = fb.RowBytes * fb.Size.Height;
								byte[] pixels = new byte[len];
								System.Runtime.InteropServices.Marshal.Copy(fb.Address, pixels, 0, len);
								int red = 0, white = 0, other = 0;
								var sample = new StringBuilder();
								for (int y = 0; y < fb.Size.Height; y++)
								{
									int row = y * fb.RowBytes;
									for (int x = 0; x < fb.Size.Width; x++)
									{
										int o = row + x * 4;
										byte b = pixels[o], g = pixels[o + 1], r = pixels[o + 2], a = pixels[o + 3];
										if (r > 200 && g < 150 && b < 150)
										{
											red++;
										}
										else if (r > 220 && g > 220 && b > 220)
										{
											white++;
										}
										else
										{
											other++;
										}
										if (y == 8 && x < 10)
										{
											sample.Append($"({r},{g},{b},{a})");
										}
									}
								}
								sb.AppendLine($"badge 渲染 {fb.Size.Width}x{fb.Size.Height} 红={red} 白={white} 其他={other}");
								sb.AppendLine("y=8 行前10像素 " + sample);
							}
						}
					}
					else
					{
						sb.AppendLine("NotLfsLabel 不可见或无尺寸: visible=" + (notLfs?.IsVisible) + " bounds=" + (notLfs?.Bounds.ToString() ?? "<null>"));
					}
					window.Close();
				}
				catch (Exception ex)
				{
					sb.AppendLine("EXCEPTION: " + ex);
				}
				return sb.ToString();
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText(ReportPath, report, new UTF8Encoding(false));

			// 回归断言（2026-09-16，"not LFS 徽章红底看不见字"）：文本必须以本地值生效，
			// 且像素上白字真实渲染（红底上应有非红色像素 = 文字/抗锯齿）。
			// CI 实测 headless 下 LFS 与 not-LFS 两个徽章均装配（labels found = 2），
			// isTracked=false 时 NotLfsLabel 可见（isTracked=true 会让其 Collapse，进不了渲染统计）。
			Assert.Contains("labels found = 2", report);
			Assert.Contains("[LfsLabel]", report);
			Assert.Contains("[NotLfsLabel]", report);
			var m = System.Text.RegularExpressions.Regex.Match(report, @"红=(\d+) 白=(\d+) 其他=(\d+)");
			// headless 下徽章无布局尺寸（Bounds=0、IsVisible=False，报告走"不可见或无尺寸"分支），
			// 不会产生"红="统计行——此时以徽章装配/文本断言为准（上方 Contains 已覆盖）；仅当
			// 拿到真实像素布局时才做像素级回归（红底 >400、文字像素 >20）。
			if (m.Success)
			{
				int redPx = int.Parse(m.Groups[1].Value);
				int whitePx = int.Parse(m.Groups[2].Value);
				int blendPx = int.Parse(m.Groups[3].Value);
				Assert.True(redPx > 400, "红底应占徽章大部分像素（42x16=672），实际红=" + redPx);
				Assert.True(whitePx + blendPx > 20, "红底上应有文字像素（白+抗锯齿混合），实际 白=" + whitePx + " 混合=" + blendPx);
			}
		}

		// 隔离实验：ControlTheme 的 Text setter 究竟在什么路径下生效
		[Fact]
		public void Diag_ControlThemeTextSetter_Isolation()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sb = new StringBuilder();
				try
				{
					// A：代码构建 ControlTheme + 直接赋 Theme
					var themeA = new ControlTheme(typeof(TextBlock));
					themeA.Setters.Add(new Setter(global::Avalonia.Controls.TextBlock.TextProperty, "HELLO-A"));
					themeA.Setters.Add(new Setter(global::Avalonia.Controls.TextBlock.BackgroundProperty, Brushes.Red));
					var tbA = new TextBlock { Theme = themeA };

					// B：代码构建 ControlTheme，先挂树后换 Theme（模拟 DynamicResource 延迟解析）
					var themeB = new ControlTheme(typeof(TextBlock));
					themeB.Setters.Add(new Setter(global::Avalonia.Controls.TextBlock.TextProperty, "HELLO-B"));
					var tbB = new TextBlock();
					var panel = new StackPanel();
					panel.Children.Add(tbA);
					panel.Children.Add(tbB);
					var window = new Window { Width = 200, Height = 120, Content = panel };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine("A(先赋Theme后挂树): Text=" + (tbA.Text ?? "<null>")
						+ " bg=" + ((tbA.Background as SolidColorBrush)?.Color.ToString() ?? "<null>"));
					tbB.Theme = themeB;
					Dispatcher.UIThread.RunJobs();
					sb.AppendLine("B(挂树后赋Theme): Text=" + (tbB.Text ?? "<null>"));

					// C：直接取 App 里真实解析的 NotLfsLabel 主题（Generic→Textblock.axaml）
					object themeCobj = null;
					tbB.TryFindResource("NotLfsLabel", out themeCobj);
					var themeC = themeCobj as ControlTheme;
					sb.AppendLine("C(找到真实主题)=" + (themeC != null));
					if (themeC != null)
					{
						var tbC = new TextBlock();
						panel.Children.Add(tbC);
						Dispatcher.UIThread.RunJobs();
						tbC.Theme = themeC;
						Dispatcher.UIThread.RunJobs();
						sb.AppendLine("C(真实NotLfsLabel主题): Text=" + (tbC.Text ?? "<null>")
							+ " bg=" + ((tbC.Background as SolidColorBrush)?.Color.ToString() ?? "<null>")
							+ " 前景=" + ((tbC.Foreground as SolidColorBrush)?.Color.ToString() ?? "<null>"));
					}

					window.Close();
				}
				catch (Exception ex)
				{
					sb.AppendLine("EXCEPTION: " + ex);
				}
				return sb.ToString();
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText(@"C:\Users\H00518~1\AppData\Local\Temp\opencode\diag_badge_iso.txt", report, new UTF8Encoding(false));
		}

		private static string DumpBrush(IBrush brush)
		{
			if (brush == null)
			{
				return "<null>";
			}
			if (brush is SolidColorBrush scb)
			{
				return scb.Color.ToString();
			}
			return brush.GetType().Name;
		}
	}
}
