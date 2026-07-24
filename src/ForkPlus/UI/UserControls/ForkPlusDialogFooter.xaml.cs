// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia（Thickness）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl、DockPanel、Dock）
// - 移除 using System.Windows.Markup（WPF XAML 代码生成专用，Avalonia 不需要）
// - 新增 using Avalonia.Interactivity（RoutedEventArgs 在 Avalonia 中位于 Avalonia.Interactivity）
// - 新增 using Avalonia.Layout（HorizontalAlignment 在 Avalonia 中位于 Avalonia.Layout）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace ForkPlus.UI.UserControls
{
	public partial class ForkPlusDialogFooter : UserControl
	{

		public event EventHandler Cancel;

		public event EventHandler Submit;

		public ForkPlusDialogFooter()
		{
			InitializeComponent();
		}

		public void AlignStatusRight()
		{
			StatusMessageTextBlock.HorizontalAlignment = HorizontalAlignment.Right;
			BusyIndicator.Margin = new Thickness(5.0, 0.0, 5.0, 0.0);
			StatusImage.Margin = new Thickness(5.0, 0.0, 5.0, 0.0);
			DockPanel.SetDock(BusyIndicator, Dock.Right);
			DockPanel.SetDock(StatusImage, Dock.Right);
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			this.Cancel?.Invoke(this, EventArgs.Empty);
		}

		private void SubmitButton_Click(object sender, RoutedEventArgs e)
		{
			e.Handled = true;
			this.Submit?.Invoke(this, EventArgs.Empty);
		}

	}
}
