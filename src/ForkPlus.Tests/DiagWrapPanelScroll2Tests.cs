// 回归测试（修复2"引用徽章不换行"）：自定义 ScrollViewer 主题曾硬编码
// CanHorizontallyScroll="True"，阻止 SCP.AttachToScrollViewer() 的自动绑定，
// 导致 HorizontalScrollBarVisibility=Disabled 时内容仍按无限宽测量、WrapPanel 不换行。
// 期望与 WPF/Avalonia 语义一致：CanHorizontallyScroll = (HSBV != Disabled)。
//   Disabled → 有限宽约束（WrapPanel 换行）；Hidden/Auto → 无限宽（可横向滚动）。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DiagWrapPanelScroll2Tests
	{
		private sealed class ProbeWrapPanel : WrapPanel
		{
			public Size LastConstraint;

			protected override Size MeasureOverride(Size constraint)
			{
				LastConstraint = constraint;
				Console.WriteLine("[diag-constraint] " + constraint);
				return base.MeasureOverride(constraint);
			}
		}

		[Theory]
		[InlineData(ScrollBarVisibility.Disabled, true)]
		[InlineData(ScrollBarVisibility.Hidden, false)]
		[InlineData(ScrollBarVisibility.Auto, false)]
		[InlineData(ScrollBarVisibility.Visible, false)]
		public void ScrollViewer_MeasureConstraint(ScrollBarVisibility hsbv, bool expectFiniteWidth)
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 1000, Height = 600 };
				var wrap = new ProbeWrapPanel();
				for (int i = 0; i < 6; i++)
				{
					wrap.Children.Add(new Border { Width = 400, Height = 20, Background = Brushes.CadetBlue, Margin = new Thickness(0, 0, 4, 4) });
				}
				var scroll = new ScrollViewer
				{
					HorizontalScrollBarVisibility = hsbv,
					Content = wrap
				};
				window.Content = scroll;
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);

				bool infiniteWidth = double.IsInfinity(wrap.LastConstraint.Width);
				string diag = $"hsbv={hsbv} constraint={wrap.LastConstraint} infiniteW={infiniteWidth} wrapH={wrap.Bounds.Height} ext={scroll.Extent}";
				Console.WriteLine("[diag-scp] " + diag);
				window.Close();
				Assert.Equal(expectFiniteWidth, !infiniteWidth);
				if (expectFiniteWidth)
				{
					// Disabled：宽度约束应等于视口（6×400 宽的项必须换行成 3 行）
					Assert.True(wrap.Bounds.Height > 50, "Disabled 时应换行。实际 h=" + wrap.Bounds.Height + " " + diag);
				}
			});
		}
	}
}
