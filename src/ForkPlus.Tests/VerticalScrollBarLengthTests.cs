// ScrollView 垂直滚动条长度诊断测试（2026-09-05）：
// 用户反馈"垂直滚动条有时候长度不对"，本测试系统验证三个根因：
//   1. ScrollBarThumbVertical 的 MinHeight=60 在长内容时强制 thumb 过大
//   2. Thumb 模板内部 Border 的 Height="{TemplateBinding Height}" 可能干扰布局
//   3. Track 行的 0.00001* 星值在精度边界场景下可能计算异常
// 对照官方 Avalonia Fluent 主题（MinHeight=NaN、不绑 Height、Track 行用 *）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class VerticalScrollBarLengthTests
	{
		// 场景 A：内容远大于视口（50 个 22px 行 = 1100px，视口 300px）
		// 期望 thumb 高度 ≈ 300/1100 × trackHeight ≈ 0.27 × trackHeight
		// 若 MinHeight=60，track 高度 ≈ 280（300-10-10），期望 thumb ≈ 76px
		// 但当内容 = 5000px（227 行）时，期望 thumb ≈ 0.06 × 280 ≈ 17px，
		// MinHeight=60 会把 17px 强制成 60px → thumb 看起来"太长"
		[Fact]
		public void VerticalScrollBar_ThumbRatio_LongContent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			double[] metrics = new double[8]; // [0]=thumbH, [1]=trackH, [2]=contentH, [3]=viewportH,
			                                  //  [4]=expectedRatio, [5]=actualRatio, [6]=minHeight, [7]=maxPossible

			Dispatcher.UIThread.InvokeAsync(delegate
			{
				// 200 行 × 22px = 4400px 内容，视口 300px
				var content = new StackPanel();
				for (int i = 0; i < 200; i++)
				{
					content.Children.Add(new Border
					{
						Height = 22,
						Background = i % 2 == 0 ? Brushes.LightGray : Brushes.White
					});
				}

				var sv = new ScrollViewer
				{
					Width = 400,
					Height = 300,
					Content = content,
					VerticalScrollBarVisibility = ScrollBarVisibility.Visible
				};

				var window = new Window { Width = 500, Height = 400, Content = sv };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Dispatcher.UIThread.RunJobs();

				ScrollBar vBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First(b => b.Orientation == Orientation.Vertical);
				Track track = vBar.GetVisualDescendants().OfType<Track>().First();
				Thumb thumb = track?.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();

				Assert.NotNull(track);
				Assert.NotNull(thumb);

				metrics[0] = thumb.Bounds.Height;           // 实际 thumb 高度
				metrics[1] = track.Bounds.Height;            // track 高度
				metrics[2] = 4400;                           // 内容高度
				metrics[3] = 300;                            // 视口高度
				metrics[4] = metrics[3] / metrics[2];       // 期望比例
				metrics[5] = metrics[0] / metrics[1];        // 实际比例
				metrics[6] = thumb.MinHeight;                // MinHeight 设置
				metrics[7] = metrics[1] * metrics[4];        // 理想 thumb 高度

				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			// 诊断输出
			string report = $"Thumb高度={metrics[0]:F1}px, Track高度={metrics[1]:F1}px, "
				+ $"内容={metrics[2]}px, 视口={metrics[3]}px\n"
				+ $"期望比例={metrics[4]:F4}, 实际比例={metrics[5]:F4}\n"
				+ $"理想Thumb高度={metrics[7]:F1}px, MinHeight={metrics[6]}px\n"
				+ $"差异: 实际({metrics[0]:F1}) vs 理想({metrics[7]:F1}) = "
				+ $"{(metrics[0] - metrics[7]):F1}px偏差";

			System.IO.File.WriteAllText(
				"/tmp/vscroll_diagnosis.txt", report);

			// 核心断言：thumb 高度应接近理想高度
			// 修复前 MinHeight=60 时，thumb 被强制拉到 60px（理想只有 ~19px）
			// 修复后 MinHeight=18，thumb 应接近理想值
			double idealThumb = metrics[7];
			double actualThumb = metrics[0];
			double tolerance = idealThumb * 0.2 + 2; // 20% + 2px 容差

			Assert.True(Math.Abs(actualThumb - idealThumb) < tolerance,
				$"Thumb 高度应接近理想值 {idealThumb:F1}px（实际 {actualThumb:F1}px，"
				+ $"MinHeight={metrics[6]}，差异 {actualThumb - idealThumb:F1}px）");
		}

		// 场景 B：短内容 → 内容恰好等于视口 → thumb 应占满 track
		[Fact]
		public void VerticalScrollBar_ThumbRatio_ShortContent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			double[] metrics = new double[4];

			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var content = new StackPanel();
				for (int i = 0; i < 5; i++)
				{
					content.Children.Add(new Border
					{
						Height = 22,
						Background = Brushes.LightGray
					});
				}

				var sv = new ScrollViewer
				{
					Width = 400,
					Height = 300,
					Content = content,
					VerticalScrollBarVisibility = ScrollBarVisibility.Visible
				};

				var window = new Window { Width = 500, Height = 400, Content = sv };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Dispatcher.UIThread.RunJobs();

				ScrollBar vBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First(b => b.Orientation == Orientation.Vertical);

				// 内容 110px < 视口 300px → 不需要滚动 → Maximum=0 → thumb 应占满 track
				metrics[0] = vBar.Maximum;
				metrics[1] = vBar.ViewportSize;
				metrics[2] = vBar.Bounds.Height;

				Track track = vBar.GetVisualDescendants().OfType<Track>().FirstOrDefault();
				Thumb thumb = track?.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
				if (thumb != null)
				{
					metrics[3] = thumb.Bounds.Height;
				}

				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			// 内容 < 视口 → Maximum=0 → 不应出现 thumb 过小问题
			Assert.True(metrics[0] == 0,
				$"短内容 Maximum 应为 0（实际 {metrics[0]}）");
		}

		// 场景 C：验证 MinHeight=60 是否对中等长度内容造成影响
		// 内容 = 15 行 × 22px = 330px，视口 = 300px
		// 期望 thumb = 300/330 × track ≈ 0.91 × track ≈ 255px（track ≈ 280）
		// MinHeight=60 不会影响 → thumb 应正确
		[Fact]
		public void VerticalScrollBar_ThumbRatio_MediumContent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			double[] metrics = new double[6];

			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var content = new StackPanel();
				for (int i = 0; i < 15; i++)
				{
					content.Children.Add(new Border
					{
						Height = 22,
						Background = Brushes.LightGray
					});
				}

				var sv = new ScrollViewer
				{
					Width = 400,
					Height = 300,
					Content = content,
					VerticalScrollBarVisibility = ScrollBarVisibility.Visible
				};

				var window = new Window { Width = 500, Height = 400, Content = sv };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				Dispatcher.UIThread.RunJobs();

				ScrollBar vBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First(b => b.Orientation == Orientation.Vertical);
				Track track = vBar.GetVisualDescendants().OfType<Track>().First();
				Thumb thumb = track?.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();

				Assert.NotNull(thumb);

				double contentH = 330;
				double viewportH = 300;
				double expectedRatio = viewportH / contentH;

				metrics[0] = thumb.Bounds.Height;
				metrics[1] = track.Bounds.Height;
				metrics[2] = expectedRatio;
				metrics[3] = metrics[1] * expectedRatio; // 理想 thumb
				metrics[4] = metrics[0] / metrics[1];     // 实际比例
				metrics[5] = thumb.MinHeight;

				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			// 中等内容 → MinHeight 不应生效 → thumb 应接近理想值
			double tolerance = metrics[3] * 0.15 + 2;
			Assert.True(Math.Abs(metrics[0] - metrics[3]) < tolerance,
				$"中等内容 thumb 应接近理想值 {metrics[3]:F1}px（实际 {metrics[0]:F1}px，"
				+ $"MinHeight={metrics[5]}，比例 期望={metrics[2]:F4} 实际={metrics[4]:F4}）");
		}
	}
}
