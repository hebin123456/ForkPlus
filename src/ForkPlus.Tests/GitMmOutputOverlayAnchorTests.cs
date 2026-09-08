// 回归测试（2026-09-07 v3.15，"git mm 命令输出弹窗在很下面，应该要在命令输出按钮点下去
// 的位置"）：GitMmOutputButton 在主窗口顶部工具栏（ToolbarUserControl Row0 内
// StatusUserControl Column1），而 2026-09-04 那轮定位修复误设 VerticalAlignment=Bottom——
// 把 720x360 覆盖层钉死在 git mm 内容区底部，与顶部按钮相距整个窗口高度（用户看到的
// "在很下面"）。原版 WPF OutputPopup 为 Placement="Mouse" VerticalOffset="-4"（弹在鼠标
// 点击点=按钮处）。修复：弹窗顶边锚定按钮底缘 +2px（VerticalAlignment=Top + 动态 Top
// margin；按钮在内容区上方时 y 为负，覆盖层上浮遮挡标签头——宿主链 ClosableTabControlTheme
// 模板无 ClipToBounds，越界渲染不裁剪）。
// 测试两层：1) 直调真实生产静态方法 ComputeOutputOverlayAnchor（锚定数学：贴按钮下方/
// 右收窄/底不越界——修复前该形态恒 Bottom 锚定，顶边=内容区底-360）；2) 窗口级接线复刻
// （与 GitMmPopupDismissTests 同风格：GitMmUserControl 构造依赖真实 git 工作区无法 headless
// 直建，TranslatePoint→真实锚定函数→Left/Top+margin 的接线逐行对应生产代码）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using ForkPlus.UI.UserControls;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class GitMmOutputOverlayAnchorTests
	{
		// ===== 1) 真实生产锚定数学：按钮在内容区上方（顶部工具栏）→ 弹窗贴按钮下方 =====

		[Fact]
		public void AnchorComputation_ButtonAboveContent_PopupTopJustBelowButton()
		{
			HeadlessAppBootstrap.EnsureStarted();
			// 按钮在 RootGrid 坐标 (100,-40)（顶部工具栏内，不触发右收窄），高 14；RootGrid 1000x600；弹窗 720x360。
			Thickness margin = GitMmUserControl.ComputeOutputOverlayAnchor(
				new Point(100, -40), 14, 1000, 600, 720, 360);
			// 顶边 = 按钮底缘 +2 = -40+14+2 = -24（负值=上浮遮挡标签头，WPF Placement=Mouse 语义）。
			Assert.Equal(-24.0, margin.Top, 0.01);
			// 左边对齐按钮。
			Assert.Equal(100.0, margin.Left, 0.01);
			// 修复前形态守卫：Bottom 锚定会把顶边放到 内容区高-弹窗高=240 处（"在很下面"），
			// 弹窗顶边必须远离该值（负值上浮）。
			Assert.True(margin.Top < 0.0, "弹窗顶边应在内容区上方（贴顶部按钮），而不是底部（实测 top=" + margin.Top + "）");
		}

		[Fact]
		public void AnchorComputation_RightEdgeClamp_AndBottomClamp()
		{
			HeadlessAppBootstrap.EnsureStarted();
			// 右收窄：按钮 X=900 越过 maxX=1000-720-16=264 → x=264。
			Thickness right = GitMmUserControl.ComputeOutputOverlayAnchor(
				new Point(900, -40), 14, 1000, 600, 720, 360);
			Assert.Equal(264.0, right.Left, 0.01);
			// 底不越界：按钮底缘 516 > maxTop=600-360-8=232 → y=232（弹窗底贴内容区底-8）。
			Thickness clamped = GitMmUserControl.ComputeOutputOverlayAnchor(
				new Point(100, 500), 14, 1000, 600, 720, 360);
			Assert.Equal(232.0, clamped.Top, 0.01);
			// 矮内容区（600→300）：maxTop=-68 → 弹窗整体上移贴底，不越过 RootGrid 底部。
			Thickness shortRoot = GitMmUserControl.ComputeOutputOverlayAnchor(
				new Point(100, 250), 14, 1000, 300, 720, 360);
			Assert.Equal(-68.0, shortRoot.Top, 0.01);
		}

		// ===== 2) 窗口级接线复刻：按钮在顶部工具栏，覆盖层锚定按钮下方而非窗口底部 =====

		[Fact]
		public void WindowWiring_OverlayAnchorsBelowTopToolbarButton_NotAtWindowBottom()
		{
			HeadlessAppBootstrap.EnsureStarted();
			bool pass = HeadlessAppBootstrap.Run(delegate
			{
				// 复刻主窗口结构：Row0=工具栏（含 GitMmOutputButton 14x14），Row1=git mm 内容
				//（RootGrid，内含 720x360 OutputOverlayBorder，XAML 默认 Right/Bottom+Margin16）。
				var window = new Window { Width = 1000, Height = 600 };
				var mainGrid = new Grid();
				mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(46) });
				mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
				var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
				var outputButton = new Button { Name = "GitMmOutputButton", Width = 14, Height = 14, Content = "o" };
				toolbar.Children.Add(outputButton);
				Grid.SetRow(toolbar, 0);
				var rootGrid = new Grid { Margin = new Thickness(10) };
				var overlay = new Border
				{
					Name = "OutputOverlayBorder", Width = 720, Height = 360, ZIndex = 10,
					HorizontalAlignment = HorizontalAlignment.Right,
					VerticalAlignment = VerticalAlignment.Bottom,
					Margin = new Thickness(0, 0, 16, 16)
				};
				rootGrid.Children.Add(overlay);
				Grid.SetRow(rootGrid, 1);
				mainGrid.Children.Add(toolbar);
				mainGrid.Children.Add(rootGrid);
				window.Content = mainGrid;
				window.Show();
				window.UpdateLayout();

				// —— 生产接线（PositionOutputOverlayAtCommandOutputButton 修复后逐行对应）——
				global::Avalonia.Point? p = outputButton.TranslatePoint(new Point(0, 0), rootGrid);
				Assert.True(p.HasValue, "按钮与 RootGrid 应在同一视觉树");
				overlay.Margin = GitMmUserControl.ComputeOutputOverlayAnchor(
					p.Value, outputButton.Bounds.Height, rootGrid.Bounds.Width, rootGrid.Bounds.Height,
					overlay.Width, overlay.Height);
				overlay.HorizontalAlignment = HorizontalAlignment.Left;
				overlay.VerticalAlignment = VerticalAlignment.Top;
				window.UpdateLayout();

				// 换算回窗口坐标验证。
				Point? overlayTopLeft = overlay.TranslatePoint(new Point(0, 0), window);
				Assert.True(overlayTopLeft.HasValue, "覆盖层应已挂树布局");
				double buttonBottomInWindow = outputButton.TranslatePoint(new Point(0, outputButton.Bounds.Height), window)!.Value.Y;
				// 1) 弹窗顶边在按钮底缘下方（间隙 +2，容差 8）——"在按钮点下去的位置"。
				bool topNearButton = overlayTopLeft.Value.Y >= buttonBottomInWindow - 2.0
					&& overlayTopLeft.Value.Y <= buttonBottomInWindow + 8.0;
				// 2) 修复前形态：Bottom 锚定顶边 = 窗口高-360=240；顶边在 46px 工具栏附近即通过，
				//    远离窗口底部（240±40 拒绝）。
				bool notAtWindowBottom = overlayTopLeft.Value.Y < 100.0;
				// 3) 水平对齐按钮左缘（容差 2）。
				bool leftAligned = global::System.Math.Abs(overlayTopLeft.Value.X
					- outputButton.TranslatePoint(new Point(0, 0), window)!.Value.X) <= 2.0;
				window.Close();
				return topNearButton && notAtWindowBottom && leftAligned;
			});
			Assert.True(pass, "输出覆盖层未锚定到顶部命令输出按钮下方（仍钉在内容区底部=旧缺陷'在很下面'）");
		}
	}
}
