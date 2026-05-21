using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GoToLineWindow : ForkPlusDialogWindow
	{

		public int? LineNumber { get; private set; }

		public GoToLineWindow()
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			base.IsTitleVisible = true;
			InitializeComponent();
			base.Title = PreferencesLocalization.Current("Go To Line");
			base.SubmitButtonTitle = "Go";
		}

		protected override void OnSubmit()
		{
			if (int.TryParse(LineNumberTextBox.Text, out var result))
			{
				LineNumber = result;
			}
			else
			{
				LineNumber = null;
			}
			CloseWithOk();
		}

	}
}
