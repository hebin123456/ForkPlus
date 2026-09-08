// 回归测试（2026-09-05，"有些 button，里面的字和外面的框不适配，字的下面有部分会被遮挡"）：
// 根因：默认 Button 主题（Theme/Styles/Button.axaml）固定 Height=24 + BorderThickness=1
// → 内容槽 22px；13px 字行高 ≈17.3px（Windows Segoe UI）/18px（headless 默认字体）。
// 使用点垂直 Padding ≥3px（20,4 / 12,4 / 8,4 等，共 7 个文件 16 个按钮）把
// ContentPresenter 槽挤到 14px < 行高，Avalonia 按槽位裁剪渲染 → 文字上下被切
// （实测像素：红字"gjpqy"墨水行数 10→9、像素 63→57）。WPF 原版同模板下文字
// 溢出槽位但 WPF 不裁剪渲染（缺陷不可见），迁移到 Avalonia 后显形。
// 修复：受影响使用点垂直 Padding 归零/收敛（按钮高度 24、水平 Padding 均不变，
// 与 WPF 原版视觉一致），主题 Height setter 处加护栏注释（垂直 Padding 合计 ≤4px）。
// 三层回归：
//   1) 几何不变量：应用内全部真实 padding/字号组合，presenter 排列高度 ≥ 文字行高；
//   2) 不变量自证：旧坏组合 (20,4) 确实违反不变量（证明测试能抓到该缺陷）；
//   3) 像素级：修复组合的红字墨水行数/像素数 == 无裁剪参考按钮（真实渲染无裁剪）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ButtonTextClippingTests
	{
		// 应用内默认 Button 主题（固定 Height=24）按钮的全部真实 padding/字号组合
		//（修复后）。最后一个元素带 MinHeight=28（AiDevelopmentWindow 的 Clear/Send）。
		private static readonly (Thickness padding, double fontSize, double minHeight, string label)[] RealCombos =
		{
			(new Thickness(10, 0, 10, 0), 13, 0, "主题默认(10,0)"),
			(new Thickness(20, 0, 20, 0), 13, 0, "CustomColorsDialog(20,0)"),
			(new Thickness(12, 0, 12, 0), 13, 0, "Reflog/AiCommitComposer/ImportExport(12,0)"),
			(new Thickness(8, 0, 8, 0), 12, 0, "AiReviewPreferences(8,0) fs12"),
			(new Thickness(10, 0, 10, 0), 13, 28, "AiDevelopment(10,0) MinHeight28"),
			(new Thickness(10, 2, 10, 2), 13, 0, "Commit/AiCodeReview(10,2)"),
			(new Thickness(12, 2), 13, 0, "CustomColorsDialog ResetItem(12,2)"),
			(new Thickness(4, 2, 4, 2), 13, 0, "CustomCommands(4,2,4,2)"),
			(new Thickness(8, 1), 11, 0, "AiDevelopment Stop(8,1) fs11")
		};

		private static double MeasureLineHeight(double fontSize)
		{
			// 独立无约束测量同字号行高（与按钮内隐式 TextBlock 同默认字体解析）
			var ruler = new TextBlock { Text = "Import Colors 提交 gjpqy", FontSize = fontSize };
			ruler.Measure(Size.Infinity);
			return ruler.DesiredSize.Height;
		}

		// presenter 排列高度 = min(文字期望高度, 槽位)。对齐为 Center 时若槽位不足，
		// presenter 被压到槽位高 → 排列高度 < 行高 ⟺ 裁切。
		private static double MeasurePresenterArrangedHeight(Thickness padding, double fontSize, double minHeight)
		{
			var button = new Button
			{
				Content = "Import Colors 提交 gjpqy",
				Padding = padding,
				FontSize = fontSize,
				Width = 220
			};
			if (minHeight > 0)
			{
				button.MinHeight = minHeight;
			}
			var window = new Window { Width = 320, Height = 120, Content = button };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			ContentPresenter presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
			double height = presenter.Bounds.Height;
			window.Close();
			return height;
		}

		[Fact]
		public void RealWorldPaddingCombos_TextFitsContentSlot()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string[] violations = Dispatcher.UIThread.InvokeAsync(delegate
			{
				return RealCombos
					.Select(c => new
					{
						Label = c.label,
						Arranged = MeasurePresenterArrangedHeight(c.padding, c.fontSize, c.minHeight),
						Line = MeasureLineHeight(c.fontSize)
					})
					.Where(x => x.Arranged + 0.01 < x.Line)
					.Select(x => x.Label + ": presenter=" + x.Arranged.ToString("F1") + " < 行高=" + x.Line.ToString("F1"))
					.ToArray();
			}).GetAwaiter().GetResult();
			Assert.True(violations.Length == 0,
				"按钮文字超出内容槽（垂直 Padding 挤压槽位 → 文字被裁）：\n" + string.Join("\n", violations)
				+ "\n主题固定 Height=24（内槽 22px），垂直 Padding 合计必须 ≤4px；需要更厚的按钮用 MinHeight。");
		}

		[Fact]
		public void OldBadCombo_ViolatesInvariant()
		{
			HeadlessAppBootstrap.EnsureStarted();
			// 自证不变量有效：修复前的坏组合 (20,4)（槽 14px）必须被上面那条不变量抓到。
			// 若此断言失败说明测试环境字体行高 ≤14px（13px 字），不变量对该缺陷失效，需重估。
			double arranged = Dispatcher.UIThread.InvokeAsync(delegate
			{
				return MeasurePresenterArrangedHeight(new Thickness(20, 4, 20, 4), 13, 0);
			}).GetAwaiter().GetResult();
			double line = MeasureLineHeight(13);
			Assert.True(arranged + 0.01 < line,
				"坏组合 (20,4) 未违反不变量（presenter=" + arranged.ToString("F1") + " ≥ 行高=" + line.ToString("F1")
				+ "）——测试环境字体变化使不变量失效，需重估阈值");
		}

		// ===== 像素级：真实渲染无裁剪 =====

		private static (int rows, int pixels) MeasureRedInk(Thickness padding)
		{
			var button = new Button
			{
				Content = "gjpqy",
				Padding = padding,
				FontSize = 13,
				Width = 100,
				Foreground = Avalonia.Media.Brushes.Red,
				Background = Avalonia.Media.Brushes.White,
				BorderBrush = Avalonia.Media.Brushes.Black
			};
			var window = new Window { Width = 140, Height = 80, Background = Avalonia.Media.Brushes.White, Content = button };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			using (var frame = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window))
			{
				int minRow = int.MaxValue, maxRow = -1, redPixels = 0;
				using (var l = frame.Lock())
				{
					for (int row = 0; row < frame.PixelSize.Height; row++)
					{
						IntPtr rowPtr = l.Address + row * l.RowBytes;
						for (int x = 0; x < frame.PixelSize.Width; x++)
						{
							byte b0 = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4);
							byte b1 = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 1);
							byte b2 = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 2);
							// 与字节序无关的红字检测：恰有一个通道高（R），其余低（G/B）
							bool red = (b0 > 140 && b1 < 110 && b2 < 110) || (b2 > 140 && b1 < 110 && b0 < 110);
							if (red)
							{
								redPixels++;
								if (row < minRow) minRow = row;
								if (row > maxRow) maxRow = row;
							}
						}
					}
				}
				window.Close();
				return (maxRow >= 0 ? maxRow - minRow + 1 : 0, redPixels);
			}
		}

		[Fact]
		public void FixedCombos_RedInkRowsMatchUnclippedReference()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(int rows, int pixels)[] results = Dispatcher.UIThread.InvokeAsync(delegate
			{
				return new[]
				{
					MeasureRedInk(new Thickness(10, 0, 10, 0)), // 主题默认（无裁剪参考）
					MeasureRedInk(new Thickness(20, 0, 20, 0)), // CustomColorsDialog（修复后）
					MeasureRedInk(new Thickness(12, 0, 12, 0))  // Reflog 等（修复后）
				};
			}).GetAwaiter().GetResult();

			int refRows = results[0].rows;
			int refPixels = results[0].pixels;
			Assert.True(refRows > 0 && refPixels > 20, "参考按钮红字未渲染（rows=" + refRows + "）——像素探针失效");

			// 修复后的组合：墨水行数与参考一致（±1 行容忍亚像素基线偏移），像素数 ≥90%
			Assert.True(Math.Abs(results[1].rows - refRows) <= 1,
				"(20,0) 红字墨水行数 " + results[1].rows + " ≠ 参考 " + refRows + "（仍被裁切）");
			Assert.True(Math.Abs(results[2].rows - refRows) <= 1,
				"(12,0) 红字墨水行数 " + results[2].rows + " ≠ 参考 " + refRows + "（仍被裁切）");
			Assert.True(results[1].pixels >= refPixels * 0.9,
				"(20,0) 红字像素 " + results[1].pixels + " < 参考 90%（" + refPixels + "）");
			Assert.True(results[2].pixels >= refPixels * 0.9,
				"(12,0) 红字像素 " + results[2].pixels + " < 参考 90%（" + refPixels + "）");
		}

		// ===== 真实对话框实证：CustomColorsDialog 修复后的按钮不再带裁切性垂直 Padding =====

		[Fact]
		public void CustomColorsDialog_FooterButtons_NoClippingPadding()
		{
			HeadlessAppBootstrap.EnsureStarted();
			// (name, padTop, padBottom, presenterH, lineH) 一次性收集
			(string, double, double, double, double)[] data = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				string[] names = { "ImportColorsButton", "ExportColorsButton", "RandomPaletteButton", "ResetAllButton" };
				double lineHeight = MeasureLineHeight(13);
				var result = names.Select(delegate(string n)
				{
					Button b = dialog.GetControl<Button>(n);
					ContentPresenter p = b.GetVisualDescendants().OfType<ContentPresenter>().First();
					return (n, b.Padding.Top, b.Padding.Bottom, p.Bounds.Height, lineHeight);
				}).ToArray();
				dialog.Close();
				return result;
			}).GetAwaiter().GetResult();

			foreach (var (name, padTop, padBottom, presenterH, lineH) in data)
			{
				Assert.True(padTop <= 2 && padBottom <= 2,
					name + " 垂直 Padding 回归（Top=" + padTop + ", Bottom=" + padBottom
					+ "）——固定 Height=24 主题下 ≥3 会裁切文字");
				Assert.True(presenterH + 0.01 >= lineH,
					name + " 文字超出内容槽（presenter=" + presenterH.ToString("F1") + " < 行高=" + lineH.ToString("F1") + "）");
			}
		}
	}
}
