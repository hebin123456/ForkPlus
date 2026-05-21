using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

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
