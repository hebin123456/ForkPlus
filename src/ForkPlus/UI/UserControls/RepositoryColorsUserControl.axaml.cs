using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Settings;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls
{
	public partial class RepositoryColorsUserControl : UserControl
	{
		private static readonly SolidColorBrush[] _repositoryBrushes = new SolidColorBrush[7]
		{
			new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00000000")), // Migration note：Avalonia Brushes.Transparent 是不可变接口类型。
			new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3B30")),
			new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9502")),
			new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC00")),
			new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64DA38")),
			new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1CADF8")),
			new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CB73E1"))
		};

		private bool _initialized;

		private readonly RepositoryManager.Repository _repository;

		public RepositoryColorsUserControl(RepositoryManager.Repository repository)
		{
			InitializeComponent();
			_repository = repository;
			SolidColorBrush[] repositoryBrushes = _repositoryBrushes;
			for (int i = 0; i < repositoryBrushes.Length; i++)
			{
			}
			InitializeColorButtons(repository.Color);
			_initialized = true;
		}

		[Null]
		public static SolidColorBrush GetBrush(RepositoryColor colorIndex)
		{
			if (colorIndex == RepositoryColor.None)
			{
				return null;
			}
			return _repositoryBrushes[(int)colorIndex];
		}

		private void ColorButton_Changed(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			if (_initialized)
			{
				if (sender is RadioButton)
				{
					UpdateRepositoryColor();
				}
				HideParentContextMenu(sender);
			}
		}

		private static void HideParentContextMenu(object ctrl)
		{
			for (global::Avalonia.Controls.Control frameworkElement = ctrl as global::Avalonia.Controls.Control; frameworkElement != null; frameworkElement = frameworkElement.Parent as global::Avalonia.Controls.Control)
			{
				if (frameworkElement is ContextMenu contextMenu)
				{
					contextMenu.Close();
					break;
				}
			}
		}

		private void InitializeColorButtons(RepositoryColor colorIndex)
		{
			Color0Button.Background = _repositoryBrushes[1];
			Color1Button.Background = _repositoryBrushes[2];
			Color2Button.Background = _repositoryBrushes[3];
			Color3Button.Background = _repositoryBrushes[4];
			Color4Button.Background = _repositoryBrushes[5];
			Color5Button.Background = _repositoryBrushes[6];
			switch (colorIndex)
			{
			case RepositoryColor.None:
				NoColorButton.IsChecked = true;
				break;
			case RepositoryColor.Red:
				Color0Button.IsChecked = true;
				break;
			case RepositoryColor.Orange:
				Color1Button.IsChecked = true;
				break;
			case RepositoryColor.Yellow:
				Color2Button.IsChecked = true;
				break;
			case RepositoryColor.Green:
				Color3Button.IsChecked = true;
				break;
			case RepositoryColor.Blue:
				Color4Button.IsChecked = true;
				break;
			case RepositoryColor.Violet:
				Color5Button.IsChecked = true;
				break;
			}
		}

		private void UpdateRepositoryColor()
		{
			RepositoryManager.Instance.UpdateRepositoryColor(_repository.Path, GetSelectedColorIndex());
			RepositoryManager.Instance.Save();
			NotificationCenter.Current.RaiseRepositoryColorChanged(this, _repository);
		}

		private RepositoryColor GetSelectedColorIndex()
		{
			if (NoColorButton.IsChecked.GetValueOrDefault())
			{
				return RepositoryColor.None;
			}
			if (Color0Button.IsChecked.GetValueOrDefault())
			{
				return RepositoryColor.Red;
			}
			if (Color1Button.IsChecked.GetValueOrDefault())
			{
				return RepositoryColor.Orange;
			}
			if (Color2Button.IsChecked.GetValueOrDefault())
			{
				return RepositoryColor.Yellow;
			}
			if (Color3Button.IsChecked.GetValueOrDefault())
			{
				return RepositoryColor.Green;
			}
			if (Color4Button.IsChecked.GetValueOrDefault())
			{
				return RepositoryColor.Blue;
			}
			if (Color5Button.IsChecked.GetValueOrDefault())
			{
				return RepositoryColor.Violet;
			}
			throw new Exception("Cannot reach here");
		}

	}
}
