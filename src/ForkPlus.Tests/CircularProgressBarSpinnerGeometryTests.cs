// 真机 bug 复现（2026-10-22，"loading 转圈动画：先是扁的，修完后又只剩上下左右四块"）：
// CircularProgressBar spinner 是 16x16 设计面上 12 根 ~1x4.2 矩形按 0.001px 精度布点、
// 各自绕中心旋转 + 平移到半径 ~5.2 圆周上的追逐动画。WPF→Avalonia 迁移引入两处几何缺陷：
//   1) RenderTransformOrigin="0.5,0.5" 原样保留——WPF 比例语义（元素中心），Avalonia 的
//      RelativePoint 把无单位坐标解析为 Absolute（左上角 0.5px），各矩形绕左上角旋转，
//      圆环塌成歪扁形状；正确写法是 "50%,50%"。
//   2) Avalonia UseLayoutRounding 默认开启（WPF 默认关闭），把 12 个矩形的亚像素布点量化
//      成整数网格（Left 7.498→7、Top 0.7→1、高 4.198→5），落点半径 4.53~5.94 不等、
//      角距 17°~44° 跳变——圆环变成"上下左右四块"的方形轮廓；模板需显式
//      UseLayoutRounding="False"。
// 本测试两层回归：
//   1) XAML 结构断言——防"0.5,0.5"复活、防 UseLayoutRounding 丢失；
//   2) headless 几何验证——用生产 ControlTheme 实例化 spinner，把 12 个矩形的渲染中心
//      映射回 Canvas 坐标系，断言半径一致（正圆）且角距均匀 30°（两点缺陷任一复发
//      都会破坏均匀性：缺陷① 半径 2.4~7.1，缺陷② 半径 4.5~5.9/角距 17°~44°）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Shapes = Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CircularProgressBarSpinnerGeometryTests
	{
		// ===== 1) XAML 结构防回归 =====

		[Fact]
		public void ProgressbarXaml_UsesAvaloniaRelativeOrigin_AndDisablesLayoutRounding()
		{
			string root = FindRepositoryRoot();
			string xaml = File.ReadAllText(Path.Combine(root,
				"src", "ForkPlus", "Theme", "Styles", "Progressbar.axaml"));

			// 缺陷①：WPF 的比例写法在 Avalonia 被解析为 Absolute(0.5px,0.5px)（绕左上角旋转）。
			Assert.DoesNotContain("RenderTransformOrigin=\"0.5,0.5\"", xaml);
			Assert.Contains("RenderTransformOrigin=\"50%,50%\"", xaml);

			// 缺陷②：布局圆整默认开启会把亚像素布点量化成整数网格，必须显式关闭
			//（Canvas + 12 个 Rectangle 共 13 处）。
			Assert.Contains("<Canvas Height=\"16\" Width=\"16\" UseLayoutRounding=\"False\">", xaml);
			Assert.Equal(13, CountOccurrences(xaml, "UseLayoutRounding=\"False\""));
		}

		// ===== 2) headless 几何验证：生产模板 → 12 点均匀圆 =====

		[Fact]
		public void SpinnerRectangles_FormUniformCircle_AroundCanvasCenter()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var progressBar = new ProgressBar { Width = 20, Height = 20 };
				Assert.True(Application.Current.TryFindResource("CircularProgressBar", out object themeObject),
					"App 资源中找不到 CircularProgressBar ControlTheme");
				progressBar.Theme = Assert.IsType<ControlTheme>(themeObject);

				var window = new Window { Width = 100, Height = 100, Content = progressBar };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				Canvas canvas = progressBar.GetVisualDescendants().OfType<Canvas>().FirstOrDefault();
				Assert.True(canvas != null, "模板中未找到 Canvas");
				Shapes.Rectangle[] rectangles = canvas.Children.OfType<Shapes.Rectangle>().ToArray();
				Assert.Equal(12, rectangles.Length);

				var centers = new List<Point>();
				int transformedCount = 0;
				foreach (Shapes.Rectangle rectangle in rectangles)
				{
					// 缺陷①的运行时形态：旋转原点必须是元素中心的 Relative 语义。
					if (rectangle.RenderTransform != null)
					{
						transformedCount++;
						Assert.True(rectangle.RenderTransformOrigin.Unit == RelativeUnit.Relative,
							rectangle.Name + " RenderTransformOrigin 应为 Relative，实际 "
							+ rectangle.RenderTransformOrigin.Unit + " " + rectangle.RenderTransformOrigin.Point);
					}
					// 缺陷②的运行时形态：布局圆整必须关闭。
					Assert.False(rectangle.UseLayoutRounding, rectangle.Name + " UseLayoutRounding 应为 False");

					// 渲染中心映射回 Canvas 16x16 坐标系（含 RenderTransform + Canvas.Left/Top）。
					Point localCenter = new Point(rectangle.Bounds.Width / 2, rectangle.Bounds.Height / 2);
					Point? mapped = rectangle.TranslatePoint(localCenter, canvas);
					Assert.True(mapped.HasValue, rectangle.Name + " 无法映射到 Canvas 坐标系");
					centers.Add(mapped.Value);
				}
				Assert.Equal(10, transformedCount);

				// 正圆：12 个落点到圆心 (8,8) 的半径一致（设计值 ~5.2，XAML 舍入残留 ±0.1）。
				double[] radii = centers.Select(c => Math.Sqrt(Math.Pow(c.X - 8.0, 2) + Math.Pow(c.Y - 8.0, 2))).ToArray();
				string radiiText = string.Join(", ", radii.Select(r => r.ToString("F2")));
				Assert.True(radii.Min() > 4.9 && radii.Max() < 5.6,
					"落点半径越界（圆环变形）: " + radiiText);
				Assert.True(radii.Max() - radii.Min() < 0.4,
					"落点半径离散过大（圆环不均匀）: " + radiiText);

				// 均匀分布：相邻落点角距 ~30°（0°=正右，逆时针为正）。
				double[] angles = centers.Select(c => Math.Atan2(8.0 - c.Y, c.X - 8.0) * 180.0 / Math.PI).ToArray();
				Array.Sort(angles);
				for (int i = 0; i < angles.Length; i++)
				{
					double gap = (i + 1 < angles.Length ? angles[i + 1] : angles[0] + 360.0) - angles[i];
					Assert.True(Math.Abs(gap - 30.0) < 10.0,
						"相邻落点角距异常（角 " + i + "，gap=" + gap.ToString("F1") + "°）: "
						+ string.Join(", ", angles.Select(a => a.ToString("F1"))));
				}
			});
		}

		private static int CountOccurrences(string text, string pattern)
		{
			int count = 0, index = 0;
			while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
			{
				count++;
				index += pattern.Length;
			}
			return count;
		}

		private static string FindRepositoryRoot()
		{
			// 与 ClassCoverageManifestTests 相同的仓库根定位（找 .git，两仓布局都成立）。
			string directory = AppContext.BaseDirectory;
			while (!string.IsNullOrWhiteSpace(directory))
			{
				if (Directory.Exists(Path.Combine(directory, ".git")) || File.Exists(Path.Combine(directory, ".git")))
				{
					return directory;
				}
				directory = Path.GetDirectoryName(directory);
			}
			throw new DirectoryNotFoundException("Could not find repository root.");
		}
	}
}
