// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.* → using Avalonia.*
// - Border/StackPanel/Image/TextBlock → Avalonia.Controls 同名类型
// - ImageSource → IImage（Avalonia.Media）
// - BindingOperations.SetBinding(_stackPanel, WidthProperty, new Binding("Width"){Source=this})
//   → Avalonia Bind(WidthProperty, new Binding("Width"){Source=this}) 扩展方法
// - CornerRadius/Thickness（API 兼容）
// - TextTrimming.CharacterEllipsis（API 兼容）
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;

namespace ForkPlus.UI.Dialogs
{
	public class TooltipView : Border
	{
		private Thickness _padding = new Thickness(4.0);

		private Size _iconSize = new Size(14.0, 14.0);

		private StackPanel _stackPanel;

		private Image _iconImage;

		private TextBlock _filenameTextBlock;

		private TextBlock _pathTextBlock;

		private TextBlock _descriptionTextBlock;

		public TooltipView()
		{
			base.BorderThickness = new Thickness(1.0);
			base.BorderBrush = Theme.BorderBrush;
			base.Background = Theme.BackgroundBrush;
			base.CornerRadius = new CornerRadius(3.0);
			base.Padding = _padding;
			base.Height = 62.0;
			base.MaxWidth = 400.0;
			base.Opacity = 0.9;
			_stackPanel = new StackPanel();
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal
			};
			_iconImage = new Image();
			_iconImage.Width = _iconSize.Width;
			_iconImage.Height = _iconSize.Height;
			stackPanel.Children.Add(_iconImage);
			_filenameTextBlock = new TextBlock();
			_filenameTextBlock.FontSize = 13.0;
			_filenameTextBlock.Foreground = Theme.LabelBrush;
			stackPanel.Children.Add(_filenameTextBlock);
			_stackPanel.Children.Add(stackPanel);
			_pathTextBlock = new TextBlock();
			_pathTextBlock.FontSize = 12.0;
			_pathTextBlock.Foreground = Theme.SecondaryLabelBrush;
			_pathTextBlock.TextTrimming = TextTrimming.CharacterEllipsis;
			_stackPanel.Children.Add(_pathTextBlock);
			_descriptionTextBlock = new TextBlock();
			_descriptionTextBlock.Margin = new Thickness(0.0, 3.0, 0.0, 0.0);
			_descriptionTextBlock.FontSize = 13.0;
			_descriptionTextBlock.Foreground = Theme.LabelBrush;
			_stackPanel.Children.Add(_descriptionTextBlock);
			Child = _stackPanel;
			// 阶段 4.5：WPF BindingOperations.SetBinding(_stackPanel, FrameworkElement.WidthProperty, binding)
			// → Avalonia AvaloniaObject.Bind(WidthProperty, binding) 扩展方法。
			Binding binding = new Binding("Width")
			{
				Source = this
			};
			_stackPanel.Bind(StackPanel.WidthProperty, binding);
		}

		public void SetDetails(IImage icon, string filename, string folder, string description)
		{
			_filenameTextBlock.Text = filename;
			_pathTextBlock.Text = folder;
			_iconImage.Source = icon;
			_descriptionTextBlock.Text = description;
		}
	}
}
