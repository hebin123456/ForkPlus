using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Settings;

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
			if (sender is ToggleButton { DataContext: UserColorViewModel dataContext })
			{
				int selectedColor = (dataContext.IsSelected ? (dataContext.BrushIndex - 1) : (-1));
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
