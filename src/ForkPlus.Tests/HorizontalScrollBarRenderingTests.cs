// 回归测试（2026-09-03，"左右滚动的滚动条绘制得有问题"修复产物）：
// 根因（两个，均源自 Scrollviewer.axaml 的 WPF→Avalonia 迁移）：
//   1) ScrollBar 主题基础样式 Width="13"，WPF 原版 :horizontal 触发器里的
//      <Setter Width="Auto"/> 被丢——横向滚动条被硬约束成 13px 宽的小方块
//      （纵向 13px 恰好正确，所以"上下没问题、左右有问题"）。
//   2) 横向模板的 Track 未绑 Orientation，经 AddOwner 默认继承 Vertical——
//      thumb 按纵向语义排列/拖动。
// 修复：:horizontal 分支补 Width=NaN（Avalonia 的 Auto）；Track 补
// Orientation="{TemplateBinding Orientation}"（对齐官方 Fluent 主题）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class HorizontalScrollBarRenderingTests
	{
		// 宽内容窄视口 → 横向滚动条出现（默认 HorizontalScrollBarVisibility=Hidden
		// 须显式开 Auto，与真实业务控件如 CodeEditor 的行为一致）。
		private static ScrollViewer MakeScrollViewerWithWideContent()
		{
			return new ScrollViewer
			{
				Width = 400,
				Height = 300,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Content = new Border { Width = 2000, Height = 2000, Background = Brushes.Red }
			};
		}

		[Fact]
		public void HorizontalScrollBar_SpansViewportWidth()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sv = MakeScrollViewerWithWideContent();
				var window = new Window { Width = 500, Height = 400, Content = sv };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				ScrollBar hBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First((ScrollBar b) => b.Orientation == Orientation.Horizontal);
				ScrollBar vBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First((ScrollBar b) => b.Orientation == Orientation.Vertical);
				string result = "hBar=" + hBar.Bounds.Width.ToString("F0") + "x" + hBar.Bounds.Height.ToString("F0")
					+ ", vBar=" + vBar.Bounds.Width.ToString("F0") + "x" + vBar.Bounds.Height.ToString("F0")
					+ ", viewport=" + sv.Bounds.Width.ToString("F0");

				window.Close();
				// 横向滚动条必须横贯底部：宽度应接近视口宽（~400，扣除纵向条），
				// 修复前被基础样式 Width=13 硬约束成 13px 小方块。
				Assert.True(hBar.Bounds.Width > 300,
					"横向滚动条未铺满视口：宽=" + hBar.Bounds.Width.ToString("F0") + "（期望 ~387，Width=13 约束未重置即红）");
				// 纵向滚动条保持 13px 宽（不受横向修复影响，双向回归防线）。
				Assert.True(Math.Abs(vBar.Bounds.Width - 13.0) < 1.5,
					"纵向滚动条宽度异常：" + vBar.Bounds.Width.ToString("F0") + "（期望 13）");
				return result;
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText("/tmp/hbar_span.txt", report);
		}

		[Fact]
		public void HorizontalScrollBar_ThumbLaysOutHorizontally()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sv = MakeScrollViewerWithWideContent();
				var window = new Window { Width = 500, Height = 400, Content = sv };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				ScrollBar hBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First((ScrollBar b) => b.Orientation == Orientation.Horizontal);
				Thumb thumb = hBar.GetVisualDescendants().OfType<Thumb>().FirstOrDefault();
				Assert.NotNull(thumb);

				// 视口 400 / 内容 2000 = 20% → 横向 thumb 宽度应约等于 track 的 20%
				//（且远小于 track 全宽）。Track 未绑 Orientation 时默认纵向排列：
				// thumb 宽 = track 全宽 → 必红。
				double trackWidth = hBar.Bounds.Width - 20; // 扣除两端 10px 箭头
				string result = "thumb=" + thumb.Bounds.Width.ToString("F0") + "x" + thumb.Bounds.Height.ToString("F0")
					+ ", hBar=" + hBar.Bounds.Width.ToString("F0");

				// offset 滚到中部，thumb 应沿 X 右移（纵向错误排列时 thumb X 不变）。
				double xBefore = thumb.Bounds.X;
				sv.Offset = new Vector(800, 0);
				Dispatcher.UIThread.RunJobs();
				double xAfter = thumb.Bounds.X;
				result += ", thumbX: " + xBefore.ToString("F0") + " -> " + xAfter.ToString("F0");

				window.Close();
				Assert.True(thumb.Bounds.Width < trackWidth * 0.5,
					"横向 thumb 占满/超出半个 track（Track.Orientation 未生效，按纵向排列）：" +
					thumb.Bounds.Width.ToString("F0") + " vs track " + trackWidth.ToString("F0"));
				Assert.True(thumb.Bounds.Width > 10,
					"横向 thumb 宽度过小：" + thumb.Bounds.Width.ToString("F0"));
				Assert.True(xAfter - xBefore > 50,
					"横向 offset 增加后 thumb 未沿 X 右移（" + xBefore.ToString("F0") + " -> " +
					xAfter.ToString("F0") + "），Track 方向错误");
				return result;
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText("/tmp/hbar_thumb.txt", report);
		}
	}
}
