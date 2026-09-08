// 诊断（修复2 续4）：最小复现——纯 WrapPanel + 固定宽子项，对比“直接放窗口”与
// “放进 ScrollViewer(H=Disabled)”两种情况，确认 Avalonia ScrollViewer 是否把
// 可用宽度变成了无限宽（WPF 中 Disabled 是有限宽）。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiagWrapPanelScrollTests
	{
		private static WrapPanel MakeWrap()
		{
			var wrap = new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
			for (int i = 0; i < 6; i++)
			{
				wrap.Children.Add(new Border { Width = 400, Height = 20, Background = Brushes.CadetBlue, Margin = new Thickness(0, 0, 4, 4) });
			}
			return wrap;
		}

		[Fact]
		public void WrapPanel_DirectInWindow_Wraps()
		{
			var (w, h) = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 1000, Height = 600 };
				var wrap = MakeWrap();
				window.Content = wrap;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				Console.WriteLine($"[diag-wrap-direct] wrap.Bounds={wrap.Bounds} desired={wrap.DesiredSize}");
				window.Close();
				return (wrap.Bounds.Width, wrap.Bounds.Height);
			});
			Assert.True(h > 50, "直接放窗口：6×400 宽的项在 1000 宽里应换 3 行。实际 h=" + h);
		}

		[Fact]
		public void WrapPanel_InsideScrollViewerHDisabled_ShouldWrap()
		{
			var (w, h) = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 1000, Height = 600 };
				var wrap = MakeWrap();
				var scroll = new ScrollViewer
				{
					HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
					VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
					Content = wrap
				};
				window.Content = scroll;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				Console.WriteLine($"[diag-wrap-scroll] wrap.Bounds={wrap.Bounds} desired={wrap.DesiredSize} ext={scroll.Extent} view={scroll.Viewport}");
				window.Close();
				return (wrap.Bounds.Width, wrap.Bounds.Height);
			});
			Assert.True(h > 50, "ScrollViewer(H=Disabled) 内：应换行。实际 h=" + h + " w=" + w);
		}
	}
}
