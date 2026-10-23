using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Settings;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls
{
	public partial class UserColorsUserControl : UserControl
	{
		private static readonly UserColorBrushes _userBrushes;

		private readonly ToggleButton _parentButton;

		private readonly string _userEmail;

		private List<UserColorViewModel> ColorViewModels { get; set; }

		public event EventHandler<(string, byte)> SelectedColorChanged;

		static UserColorsUserControl()
		{
			_userBrushes = new UserColorBrushes();
		}

		public UserColorsUserControl(ToggleButton parentButton, string userEmail, byte userBrushIndex)
		{
			InitializeComponent();
			_parentButton = parentButton;
			_userEmail = userEmail;
			Refresh(userBrushIndex);
		}

		private void ColorButton_Changed(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			if (sender is ToggleButton toggleButton && toggleButton.DataContext is UserColorViewModel dataContext)
			{
				// Bug 修复（点色块无效果）：Avalonia 的 TwoWay 绑定写回发生在 IsCheckedChanged
				// 事件之后（headless 实证见 UserColorsPopupTests），原代码读 dataContext.IsSelected
				// 恒为点击前旧值 false → selectedColor 恒为 -1 → 写入 colorId 0（无色）。
				// 改读 sender 的 IsChecked（事件触发时已是新值）。
				int selectedColor = (toggleButton.IsChecked == true ? (dataContext.BrushIndex - 1) : (-1));
				UpdateUserColor(selectedColor);
			}
			HidePopup();
		}

		private void Refresh(byte userBrush)
		{
			SolidColorBrush[] array = _userBrushes.AllBrushes(ForkPlusSettings.Default.Theme);
			ColorViewModels = new List<UserColorViewModel>(array.Length);
			for (int i = 1; i < array.Length; i++)
			{
				ColorViewModels.Add(new UserColorViewModel(i, array[i], isSelected: false));
			}
			Colors.ItemsSource = ColorViewModels;
			int num = userBrush - 1;
			if (num > -1)
			{
				ColorViewModels[num].IsSelected = true;
			}
		}

		private void UpdateUserColor(int selectedColor)
		{
			int num = selectedColor + 1;
			this.SelectedColorChanged?.Invoke(this, (_userEmail, (byte)num));
		}

		private void HidePopup()
		{
			_parentButton.IsChecked = false;
		}

	}
}
