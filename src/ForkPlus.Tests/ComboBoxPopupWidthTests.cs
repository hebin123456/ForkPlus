// 回归测试（2026-09-07，"所有下拉框选项框的宽度和最长那个选项一样宽，应该要和
// 下拉框那个组件的宽度一致"）：自定义 ComboBox ControlTheme（Theme/Styles/Combobox.axaml）
// 的三个模板（ComboBoxTemplate / ComboBoxEditableTemplate / InteractiveRebaseComboBoxTemplate）
// 下拉 Border.Width 原绑定 TemplatedParent 的 ActualWidth——WPF 属性，Avalonia 控件无此
// 属性，绑定静默失败 → 下拉宽度退化为内容自适应（与最长选项同宽）。修复改绑 Bounds.Width。
// 本测试守卫：下拉 Border 宽度 == ComboBox 组件宽度（长选项不得撑宽下拉框）。
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ComboBoxPopupWidthTests
	{
		/// <summary>打开下拉并返回模板内 dropDownBorder 的布局宽度。</summary>
		private static double MeasureDropDownBorderWidth(ComboBox comboBox)
		{
			comboBox.IsDropDownOpen = true;
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			Popup popup = comboBox.GetVisualDescendants().OfType<Popup>().First((Popup p) => p.Name == "PART_Popup");
			Assert.True(popup.IsOpen, "PART_Popup 未随 IsDropDownOpen 打开");
			// Popup 内容挂在独立 PopupRoot（overlay）里——经 popup.Child 查视觉后代。
			Border border = popup.Child.GetVisualDescendants().OfType<Border>()
				.First((Border b) => b.Name == "dropDownBorder");
			// Border.Width 直接读绑定结果（NaN = 绑定失败退化为自适应），Bounds.Width 读布局结果。
			return double.IsNaN(border.Width) ? -1.0 : border.Bounds.Width;
		}

		[Fact]
		public void StandardComboBox_DropDownWidth_MatchesComboBoxWidth()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(double borderWidth, double comboBoxWidth) = HeadlessAppBootstrap.Run(delegate
			{
				var comboBox = new ComboBox { Width = 120, Height = 24 };
				// 一个远超 120px 宽的长选项 + 常规选项：修复前下拉被撑成 ~450px。
				comboBox.ItemsSource = new string[]
				{
					new string('很', 60) + "-long-option-text-padding-padding",
					"short",
					"medium option"
				};
				var window = new Window { Width = 500, Height = 200, Content = comboBox };
				window.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				double width = MeasureDropDownBorderWidth(comboBox);
				double comboWidth = comboBox.Bounds.Width;
				window.Close();
				return (width, comboWidth);
			});
			// 绑定失败（修复前）：Border.Width 为 NaN → 返回 -1（自适应内容 ~450px）。
			Assert.True(borderWidth > 0, "dropDownBorder.Width 未绑定（NaN——ActualWidth 死绑定回归）");
			Assert.Equal(comboBoxWidth, borderWidth, 1);
		}
	}
}
