// 回归测试（2026-09-07，"自定义命令界面，目标那个下拉框，样式和原生差很多，这种下拉框
// 还在比如编辑动作的动作里面"）：此类下拉框 = ComboBoxItem 内容为定宽 DockPanel（左标签 +
// 右描述，目标 Width=280 / 编辑动作 Width=370）。根因是三处 WPF→Avalonia 框架差异：
//   1) WPF Control.HorizontalContentAlignment 默认 Left（dotnet/wpf Control.cs Register
//      默认值实证），Avalonia 默认 Stretch → 关闭态 presenter/弹层项继承链拿到 Stretch。
//      修复：ComboBox ControlTheme 补 Setter Left（显式设 Stretch 的实例局部值优先不受影响）。
//   2) Avalonia Layoutable.ArrangeCore 对"Stretch+显式宽超槽位"与 Center 同样居中
//      （originX += (available-size)/2），WPF ArrangeCore 退化为 Top-Left 左对齐 →
//      280 定宽内容在 ~74 宽槽位被居中成 X=-103，可见区只剩描述文字中段（用户截图）。
//   3) 关闭态内容 = 框架运行时生成的 VisualBrush 快照 Rectangle（ComboBox.
//      UpdateSelectionBoxItem），无 TemplatedParent——StyledElement.ApplyTemplatedParent
//      ControlTheme 只对模板部件应用 ControlTheme 嵌套样式，ControlTheme 里 ^/template/…>
//      Rectangle 选择器永远够不到它。修复：App 级样式（Theme/GlobalFrameworkCompatStyles
//      .axaml，经 App.Styles 引入）走 InheritanceParent 链，快照 attach 时可收到。
//   4) 弹层 ComboBoxItem.HorizontalContentAlignment 绑 ancestor ItemsControl 在 Avalonia
//      下永远失败（弹层内容挂独立 PopupRoot=ILogicalRoot，逻辑链断开；WPF Popup 逻辑连通）
//      → 修复：绑定加 FallbackValue=Left 回退 WPF ItemsControl 默认值。
// 本测试守卫：1) 主题默认值 Left；2) 关闭态快照左对齐（X=0 而非 -103 居中偏移）+
// App 级样式确实落到快照上；3) 弹层不被定宽项撑爆（dropDownBorder=组件宽）+ 项对齐
// FallbackValue 生效 + 项内容左对齐非居中。
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ComboBoxPanelContentAlignmentTests
	{
		/// <summary>精确复刻 CustomCommandsUserControl.axaml TargetsComboBox 的项结构：
		/// ComboBoxItem.Content = DockPanel Width=280（左标签 80 + 右描述填充）。</summary>
		private static ComboBox CreateTargetStyleComboBox()
		{
			var comboBox = new ComboBox { Width = 100, Height = 24 };
			comboBox.Items.Add(new ComboBoxItem { Content = CreateItemPanel("Commit", "visible in commit context menu") });
			comboBox.Items.Add(new ComboBoxItem { Content = CreateItemPanel("Branch", "visible in branch context menu") });
			return comboBox;
		}

		private static DockPanel CreateItemPanel(string label, string description)
		{
			return new DockPanel
			{
				Width = 280,
				Children =
				{
					new TextBlock { Text = label, Width = 80, FontSize = 13, [DockPanel.DockProperty] = Dock.Left },
					new TextBlock { Text = description, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 }
				}
			};
		}

		/// <summary>关闭态选择区 presenter（模板内 x:Name="contentPresenter"）。</summary>
		private static ContentPresenter FindSelectionPresenter(ComboBox comboBox)
		{
			return comboBox.GetVisualDescendants().OfType<ContentPresenter>()
				.First((ContentPresenter p) => p.Name == "contentPresenter");
		}

		[Fact]
		public void ComboBoxTheme_DefaultHorizontalContentAlignment_IsLeft()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var comboBox = new ComboBox { Width = 120 };
				// 必须挂树应用主题后断言（未挂树控件不应用 ControlTheme）。
				var window = new Window { Width = 300, Height = 100, Content = comboBox };
				window.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				// 修复前：Avalonia 框架默认 Stretch（WPF 默认 Left）——回归即这里变回 Stretch。
				Assert.Equal(HorizontalAlignment.Left, comboBox.HorizontalContentAlignment);
				window.Close();
				return 0;
			});
		}

		[Fact]
		public void ClosedBox_FixedWidthPanelContent_IsLeftAlignedNotCentered()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				ComboBox comboBox = CreateTargetStyleComboBox();
				var window = new Window { Width = 500, Height = 200, Content = comboBox };
				window.Show();
				comboBox.SelectedIndex = 1;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				ContentPresenter selectionPresenter = FindSelectionPresenter(comboBox);
				// Avalonia/WPF ComboBox 对 UIElement 项同样做 VisualBrush 快照：关闭态内容是
				// Rectangle（宽 = 项面板定宽 280），其相对 presenter 的 X 坐标即对齐结果。
				Rectangle snapshot = selectionPresenter.GetVisualChildren().OfType<Rectangle>().FirstOrDefault();
				Assert.NotNull(snapshot);
				// 守卫两层：快照 Rectangle 被全局样式补了 HorizontalAlignment=Left（App 级样式走
				// InheritanceParent 链可达运行时生成的快照），且布局结果 X=0（左对齐、溢出右侧）。
				// 修复前：Stretch 居中 → X = (74-280)/2 = -103（可见区只剩内容中段）。
				Assert.Equal(HorizontalAlignment.Left, snapshot.HorizontalAlignment);
				Assert.Equal(0, snapshot.Bounds.X, 0.5);
				window.Close();
				return 0;
			});
		}

		[Fact]
		public void Popup_FixedWidthPanelContent_IsLeftAlignedNotCentered()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				ComboBox comboBox = CreateTargetStyleComboBox();
				var window = new Window { Width = 500, Height = 200, Content = comboBox };
				window.Show();
				comboBox.SelectedIndex = 1;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				comboBox.IsDropDownOpen = true;
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				Popup popup = comboBox.GetVisualDescendants().OfType<Popup>().First((Popup p) => p.Name == "PART_Popup");
				Assert.True(popup.IsOpen);
				// 弹层内容挂在独立 PopupRoot（overlay）——经 popup.Child 查视觉后代。
				// 弹层宽度守卫（定宽项不得撑爆弹层）：dropDownBorder 显式 Width 绑定 Bounds.Width=100，
				// 280 定宽项只在 ScrollViewer extent 里横向滚动（HSB=Auto，与 WPF 原版一致）。
				Border dropDownBorder = popup.Child.GetVisualDescendants().OfType<Border>()
					.First((Border b) => b.Name == "dropDownBorder");
				Assert.Equal(comboBox.Bounds.Width, dropDownBorder.Bounds.Width, 0.5);
				foreach (ComboBoxItem item in popup.Child.GetVisualDescendants().OfType<ComboBoxItem>()
					.Where((ComboBoxItem i) => i.Content is DockPanel))
				{
					// ComboBoxItem.HorizontalContentAlignment 绑 ancestor ItemsControl 在 Avalonia 弹层里
					// 因 PopupRoot 逻辑断链而失败——守卫 FallbackValue=Left 生效（回退值丢了即变回 Stretch）。
					Assert.Equal(HorizontalAlignment.Left, item.HorizontalContentAlignment);
					DockPanel panel = (DockPanel)item.Content;
					// panel 相对项模板 ContentPresenter 的 X：左对齐时为 margin/圆整的小正偏移（≤4.5）；
					// 回归（Stretch 居中）时为深负偏移（≈-96）。
					Assert.True(panel.Bounds.X >= 0 && panel.Bounds.X <= 4.5,
						$"弹层项内容未左对齐: panelX={panel.Bounds.X}, itemBounds={item.Bounds}, itemHCA={item.HorizontalContentAlignment}");
				}
				window.Close();
				return 0;
			});
		}
	}
}
