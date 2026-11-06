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

		// 回归测试（2026-11-07，v5.3.2 修复"滚动条与上下/左右箭头没完全对齐"）：
		// 根因：ScrollBarButton 模板的 ContentPresenter 只绑定了 Presenter 自身 Alignment，
		// 未设内容对齐 → Avalonia 默认 Stretch：Path 被排列到整个 11px 内容区、自身 Center
		// 对齐不生效，~9px 几何按原点绘制落在左上角：竖直箭头左偏 1px（Thumb 居中 → 看起来
		// 滑块右偏）、水平箭头上偏 1px（滑块下偏）。
		// 修复：内容对齐 Center + 箭头几何原点归一化（y/x 减 0.5，去掉 Shape DesiredSize
		// 取 Right/Bottom 造成的 0.5px 偏移）。本用例用几何视觉中心（Path 原点 + Data.Bounds
		// 中心）对比 Thumb 模板 Border 中心，错 1px 即红。
		[Fact]
		public void ScrollBarArrows_AlignWithThumbCrossAxis()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string report = Dispatcher.UIThread.InvokeAsync(delegate
			{
				var sv = MakeScrollViewerWithWideContent();
				var window = new Window { Width = 500, Height = 400, Content = sv };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				ScrollBar vBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First((ScrollBar b) => b.Orientation == Orientation.Vertical);
				ScrollBar hBar = sv.GetVisualDescendants().OfType<ScrollBar>()
					.First((ScrollBar b) => b.Orientation == Orientation.Horizontal);

				// 竖直滚动条：上下箭头的 X 中心必须与 Thumb 的 X 中心一致。
				RepeatButton vUp = vBar.GetVisualDescendants().OfType<RepeatButton>()
					.First((RepeatButton b) => b.Name == "PART_LineUpButton");
				global::Avalonia.Controls.Shapes.Path vArrow = vUp.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Shapes.Path>().First();
				Thumb vThumb = vBar.GetVisualDescendants().OfType<Thumb>().First();
				Border vThumbRect = vThumb.GetVisualDescendants().OfType<Border>()
					.First((Border b) => b.Name == "rectangle");
				Point vArrowCenter = vArrow.TranslatePoint(vArrow.Data!.Bounds.Center, vBar) ?? default(Point);
				// Bounds.Center 是相对父容器的坐标；取 Border 自身局部中心再翻译，否则会把
				// Border 自身的 Margin 偏移（竖向 2px）重复计入。
				Point vThumbCenter = vThumbRect.TranslatePoint(new Point(vThumbRect.Bounds.Width / 2, vThumbRect.Bounds.Height / 2), vBar) ?? default(Point);

				// 水平滚动条：左右箭头的 Y 中心必须与 Thumb 的 Y 中心一致。
				RepeatButton hLeft = hBar.GetVisualDescendants().OfType<RepeatButton>()
					.First((RepeatButton b) => b.Name == "PART_LineUpButton");
				global::Avalonia.Controls.Shapes.Path hArrow = hLeft.GetVisualDescendants()
					.OfType<global::Avalonia.Controls.Shapes.Path>().First();
				Thumb hThumb = hBar.GetVisualDescendants().OfType<Thumb>().First();
				Border hThumbRect = hThumb.GetVisualDescendants().OfType<Border>()
					.First((Border b) => b.Name == "rectangle");
				Point hArrowCenter = hArrow.TranslatePoint(hArrow.Data!.Bounds.Center, hBar) ?? default(Point);
				Point hThumbCenter = hThumbRect.TranslatePoint(new Point(hThumbRect.Bounds.Width / 2, hThumbRect.Bounds.Height / 2), hBar) ?? default(Point);

				string result = "vArrowX=" + vArrowCenter.X.ToString("F2") + ", vThumbX=" + vThumbCenter.X.ToString("F2")
					+ ", hArrowY=" + hArrowCenter.Y.ToString("F2") + ", hThumbY=" + hThumbCenter.Y.ToString("F2");

				window.Close();
				Assert.True(Math.Abs(vArrowCenter.X - vThumbCenter.X) < 0.51,
					"竖直滚动条箭头与滑块横向错位：" + result);
				Assert.True(Math.Abs(hArrowCenter.Y - hThumbCenter.Y) < 0.51,
					"水平滚动条箭头与滑块纵向错位：" + result);
				return result;
			}).GetAwaiter().GetResult();
			System.IO.File.WriteAllText("/tmp/scrollbar_arrow_align.txt", report);
		}
	}
}
