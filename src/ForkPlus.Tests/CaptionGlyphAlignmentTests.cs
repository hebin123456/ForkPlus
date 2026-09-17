// 回归（2026-09-17，"最小化按钮的矩形位置靠上"）：根因是 WindowPathStyle 的
// Stretch="Uniform"——Avalonia 的 Uniform 拉伸会把几何 bounds 归一到原点后缩放（不居中），
// 条形画在画布底部也会被贴到顶部，只改几何位置无效（实测与原版无区别）。
// 修复 = 条形放 10×10 画布底部（y=9..10）+ 最小化 Path 本地 Stretch="None"（自然坐标渲染）。
// 验证（代码直设属性，不经 ControlTheme——代码赋值 Theme 的部分 setter 不生效）：
//   1) 资源 MinimizeGeometry 的 Bounds.Y≈9（条形在底部，资源内容正确）；
//   2) Stretch=None 渲染：条形行带在 y≈9（底部），与 MaximizeGeometry(Uniform) 环形底边(9)对齐；
//   3) Stretch=Uniform（旧行为）：条形被居中到 y≈4..5——回归证据，防止有人删掉 Stretch="None"。
using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CaptionGlyphAlignmentTests
	{
		[Fact]
		public void MinimizeGlyph_StretchNone_BarAtBottomAlignedWithMaximize()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sb = new StringBuilder();
				var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
				var window = new Window { Width = 160, Height = 60, Content = panel };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				object minGeoObj = null;
				window.TryFindResource("MinimizeGeometry", out minGeoObj);
				object maxGeoObj = null;
				window.TryFindResource("MaximizeGeometry", out maxGeoObj);
				Assert.NotNull(minGeoObj);
				Assert.NotNull(maxGeoObj);
				var minGeo = (StreamGeometry)minGeoObj;
				var maxGeo = (StreamGeometry)maxGeoObj;

				// 修复路径：Stretch=None（本地值，压过 theme 的 Uniform）
				Path minFixed = MakePath(panel, minGeo, Stretch.None);
				// 旧行为对照：Stretch=Uniform（居中）
				Path minOld = MakePath(panel, minGeo, Stretch.Uniform);
				// 最大化对照：Stretch=Uniform（未改）
				Path max = MakePath(panel, maxGeo, Stretch.Uniform);

				int[] fixedBand = ScanFilledRows(minFixed);
				int[] oldBand = ScanFilledRows(minOld);
				int[] maxBand = ScanFilledRows(max);
				sb.Append("minGeo.Bounds=" + minGeo.Bounds)
					.Append("; 修复(Stretch=None)行带=[" + string.Join(",", fixedBand) + "]")
					.Append("; 旧(Uniform)行带=[" + string.Join(",", oldBand) + "]")
					.Append("; max(Uniform)行带=[" + string.Join(",", maxBand) + "]");
				window.Close();
				return sb.ToString();
			}).GetAwaiter().GetResult();

			Assert.True(report.Contains("minGeo.Bounds=0, 9, 10, 1"), "MinimizeGeometry 资源应为底部条形（Bounds.Y=9），实际: " + report);
			// 修复：条形在底部行 9（8 为抗锯齿）
			Assert.True(report.Contains("修复(Stretch=None)行带=[9]") || report.Contains("修复(Stretch=None)行带=[8,9]"),
				"Stretch=None 应把条形渲染在底部 y=9，实际: " + report);
			// 旧行为证据：Uniform 把几何 bounds 归一到原点后缩放（Avalonia 不居中），
			// 条形被贴到顶部 y=0——即用户报告的"矩形位置靠上"，且证明只改几何位置无效
			Assert.True(report.Contains("旧(Uniform)行带=[0]") || report.Contains("旧(Uniform)行带=[0,1]"),
				"Stretch=Uniform 会把条形贴到顶部 y=0（修复前的缺陷），实际: " + report);
			// 最大化环形底边在行 9，与修复后的最小化条形对齐
			Assert.True(report.Contains("max(Uniform)行带=") && ContainsBottomRow(report, "max(Uniform)行带=", 9),
				"最大化环形底边应在 y=9 与最小化条形对齐，实际: " + report);
		}

		/// <summary>检查报告里指定前缀的行带是否以指定底行收尾。</summary>
		private static bool ContainsBottomRow(string report, string prefix, int bottomRow)
		{
			int i = report.IndexOf(prefix);
			if (i < 0)
				return false;
			int j = report.IndexOf("]", i);
			string band = report.Substring(i + prefix.Length, j - (i + prefix.Length));
			string[] parts = band.Split(',');
			return parts.Length > 0 && parts[parts.Length - 1].Trim() == bottomRow.ToString();
		}

		private static Path MakePath(Panel parent, StreamGeometry geo, Stretch stretch)
		{
			var path = new Path
			{
				Data = geo,
				Width = 10,
				Height = 10,
				Stretch = stretch,
				Fill = Brushes.Black
			};
			parent.Children.Add(path);
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			return path;
		}

		/// <summary>渲染控件并返回有非透明像素的行号集合（升序）。</summary>
		private static int[] ScanFilledRows(Control control)
		{
			int w = Math.Max(1, (int)Math.Ceiling(control.Bounds.Width));
			int h = Math.Max(1, (int)Math.Ceiling(control.Bounds.Height));
			var rtb = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
			rtb.Render(control);
			using (var wb = new WriteableBitmap(rtb.PixelSize, rtb.Dpi))
			{
				using (ILockedFramebuffer fb = wb.Lock())
				{
					rtb.CopyPixels(fb);
					byte[] pixels = new byte[fb.RowBytes * fb.Size.Height];
					System.Runtime.InteropServices.Marshal.Copy(fb.Address, pixels, 0, pixels.Length);
					var rows = new System.Collections.Generic.List<int>();
					for (int y = 0; y < fb.Size.Height; y++)
					{
						int row = y * fb.RowBytes;
						for (int x = 0; x < fb.Size.Width; x++)
						{
							int o = row + x * 4;
							if (pixels[o + 3] > 40)
							{
								rows.Add(y);
								break;
							}
						}
					}
					return rows.ToArray();
				}
			}
		}
	}
}
