// 真机 bug 复现与回归：提交详情作者邮箱颜色弹窗里点色块"没反应"（不会在轨道上标记颜色）。
// 根因（headless 实证）：Avalonia 的 TwoWay 绑定写回发生在 IsCheckedChanged 事件之后，
// 生产代码（UserColorsUserControl.ColorButton_Changed）原在事件回调里读
// dataContext.IsSelected —— 恒为点击前旧值 false → selectedColor 恒为 -1 →
// 写入 colorId 0（无色）→ 轨道上无任何变化。修复后改为读 sender 的 IsChecked。
// 点击模拟遵循 UiClick.Toggle 铁律：生产点击序 = 先设 IsChecked 再 raise Click。
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class UserColorsPopupTests
	{
		private class Vm : INotifyPropertyChanged
		{
			private bool _isSelected;

			public bool IsSelected
			{
				get => _isSelected;
				set
				{
					_isSelected = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSelected"));
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;
		}

		// 固化 Avalonia 12.1.1 事实：IsCheckedChanged 回调时 TwoWay 绑定尚未写回 VM。
		// 生产代码因此不能在 IsCheckedChanged 里读 VM 的绑定属性，必须读控件自身状态
		//（UserColorsUserControl.ColorButton_Changed 读 toggleButton.IsChecked）。
		[Fact]
		public void ToggleButton_IsCheckedChanged_TwoWayWritebackHappensAfterEvent()
		{
			bool eventFired = false;
			bool vmValueAtEvent = true;
			HeadlessAppBootstrap.Run(delegate
			{
				var vm = new Vm();
				var button = new ToggleButton();
				button.Bind(ToggleButton.IsCheckedProperty, new Binding("IsSelected")
				{
					Source = vm,
					Mode = BindingMode.TwoWay
				});
				button.IsCheckedChanged += delegate
				{
					eventFired = true;
					vmValueAtEvent = vm.IsSelected;
				};
				// ToggleButton 点击路径：OnClick → Toggle → SetCurrentValue(IsChecked)
				button.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
			});
			Assert.True(eventFired, "IsCheckedChanged 未触发");
			Assert.False(vmValueAtEvent, "Avalonia 行为变更：绑定写回已提前到事件之前，UserColorsUserControl.ColorButton_Changed 可改回读 VM");
		}

		// 端到端：真实 UserColorsUserControl 挂窗口物化色块，点击第 3 个色块
		//（BrushIndex=3）应触发 SelectedColorChanged 且 colorId=3。
		[Fact]
		public void UserColorsUserControl_ClickSwatch_RaisesSelectedColorChanged()
		{
			bool eventFired = false;
			byte reportedColorId = 255;
			HeadlessAppBootstrap.Run(delegate
			{
				var parentButton = new ToggleButton();
				var control = new UserColorsUserControl(parentButton, "dev@example.com", 0);
				control.SelectedColorChanged += delegate(object s, (string, byte) colormap)
				{
					eventFired = true;
					reportedColorId = colormap.Item2;
				};
				var window = new Window { Content = control, Width = 400, Height = 200 };
				window.Show();
				global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
				control.UpdateLayout();
				// 只取真正的色块（DataContext 为 UserColorViewModel；视觉树里另有许多模板内 ToggleButton）。
				var swatch = control.GetVisualDescendants()
					.OfType<ToggleButton>()
					.SkipWhile(x => !(x.DataContext is UserColorViewModel))
					.Where(x => x.DataContext is UserColorViewModel vm && vm.BrushIndex == 3)
					.First();
				// 生产点击序（UiClick.Toggle 铁律）：先设 IsChecked 再 raise Click。
				swatch.IsChecked = true;
				swatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			});
			Assert.True(eventFired, "SelectedColorChanged 未触发：点击色块整条链路无反应");
			Assert.Equal(3, reportedColorId);
		}

		// 取消选择（点击已选中的色块）应触发 colorId=0（无色，UpdateUserColorGitCommand 据此删除条目）。
		[Fact]
		public void UserColorsUserControl_ClickSelectedSwatch_RaisesColorIdZero()
		{
			bool eventFired = false;
			byte reportedColorId = 255;
			HeadlessAppBootstrap.Run(delegate
			{
				var parentButton = new ToggleButton();
				var control = new UserColorsUserControl(parentButton, "dev@example.com", 2);
				control.SelectedColorChanged += delegate(object s, (string, byte) colormap)
				{
					eventFired = true;
					reportedColorId = colormap.Item2;
				};
				var window = new Window { Content = control, Width = 400, Height = 200 };
				window.Show();
				global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
				control.UpdateLayout();
				var swatch = control.GetVisualDescendants()
					.OfType<ToggleButton>()
					.Where(x => x.DataContext is UserColorViewModel vm && vm.BrushIndex == 2)
					.First();
				Assert.True(swatch.IsChecked == true, "BrushIndex=2 的色块应因初始 userBrushIndex=2 呈选中态");
				swatch.IsChecked = false;
				swatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			});
			Assert.True(eventFired, "SelectedColorChanged 未触发");
			Assert.Equal(0, reportedColorId);
		}

		// 风暴回归（真机 bug：标记颜色后弹窗秒关）：打开弹窗时 Refresh() 预选中已有颜色的
		// 色块，TwoWay 绑定把 IsChecked 置 true —— 绑定初始化不得触发"应用颜色 + 关弹窗"。
		// （原绑 IsCheckedChanged 时该场景会在容器物化瞬间触发 ColorButton_Changed。）
		[Fact]
		public void UserColorsUserControl_PreSelectedSwatch_MaterializationDoesNotRaiseSelectedColorChanged()
		{
			bool eventFired = false;
			HeadlessAppBootstrap.Run(delegate
			{
				var parentButton = new ToggleButton();
				var control = new UserColorsUserControl(parentButton, "dev@example.com", 3);
				control.SelectedColorChanged += delegate
				{
					eventFired = true;
				};
				var window = new Window { Content = control, Width = 400, Height = 200 };
				window.Show();
				global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
				control.UpdateLayout();
			});
			Assert.False(eventFired, "预选中色块的绑定初始化不应触发 SelectedColorChanged（弹窗秒关根因）");
		}
	}
}
