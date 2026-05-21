using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ForkPlus.UI.Dialogs
{
	public partial class CustomActionResultWindow : ForkPlusDialogWindow
	{

		public CustomActionResultWindow(string customActionName, string output)
		{
			InitializeComponent();
			base.DialogTitle = customActionName ?? "";
			base.DialogDescription = customActionName + " completed";
			OutputTextBox.Text = output;
			base.CancelButtonTitle = "Close";
			base.ShowSubmitButton = false;
		}

	}
}
