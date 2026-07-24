// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/RadioButton/ContextMenu）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（SolidColorBrush/Brushes/Color）
// - (Color)ColorConverter.ConvertFromString("#RRGGBB") → Color.Parse("#RRGGBB")（参考 UserColorBrushes）
// - SolidColorBrush.Freeze() → 移除（Avalonia 画刷默认不可变，参考 BranchViewModel）
// - FrameworkElement → Control（Avalonia 无 FrameworkElement；Control.Parent 等价，参考 ExternalToolsUserControl）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.Settings;

namespace ForkPlus.UI.UserControls
{
	public partial class RepositoryColorsUserControl : UserControl
	{
		private static readonly SolidColorBrush[] _repositoryBrushes = new SolidColorBrush[7]
		{
			Brushes.Transparent,
			new SolidColorBrush(Color.Parse("#FF3B30")),
			new SolidColorBrush(Color.Parse("#FF9502")),
			new SolidColorBrush(Color.Parse("#FFCC00")),
			new SolidColorBrush(Color.Parse("#64DA38")),
			new SolidColorBrush(Color.Parse("#1CADF8")),
			new SolidColorBrush(Color.Parse("#CB73E1"))
		};

		private bool _initialized;

		private readonly RepositoryManager.Repository _repository;

		public RepositoryColorsUserControl(RepositoryManager.Repository repository)
		{
			InitializeComponent();
			_repository = repository;
			// 阶段 4.5：Avalonia SolidColorBrush 默认不可变，无需 WPF Freeze()（参考 BranchViewModel）。
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
			// 阶段 4.5：WPF FrameworkElement → Avalonia Control（参考 ExternalToolsUserControl）。
			for (Control control = ctrl as Control; control != null; control = control.Parent as Control)
			{
				if (control is ContextMenu contextMenu)
				{
					contextMenu.IsOpen = false;
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
