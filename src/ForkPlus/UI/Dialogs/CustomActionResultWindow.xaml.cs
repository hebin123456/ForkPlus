using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class CustomActionResultWindow : ForkPlusDialogWindow
	{

		public CustomActionResultWindow(string customActionName, string output)
		{
			InitializeComponent();
			base.DialogTitle = customActionName ?? "";
			base.DialogDescription = PreferencesLocalization.FormatCurrent("{0} completed", customActionName);
			OutputTextBox.Text = output;
			base.CancelButtonTitle = PreferencesLocalization.Current("Close");
			base.ShowSubmitButton = false;
		}

	}
}
