using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

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
