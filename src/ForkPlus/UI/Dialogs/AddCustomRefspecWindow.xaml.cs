using System;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class AddCustomRefspecWindow : ForkPlusDialogWindow
	{
		private readonly string _remoteName;

		private readonly string _localBranchName;

		public string OutRefspec { get; private set; }

		public AddCustomRefspecWindow(string remoteName, string localBranchName)
		{
			_remoteName = remoteName;
			_localBranchName = localBranchName;
			InitializeComponent();
			base.DialogTitle = PreferencesLocalization.Current("Custom Remote Branch Name");
			base.DialogDescription = PreferencesLocalization.Current("Enter custom destination");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Add");
			RemoteNameTextBlock.Text = remoteName + "/";
			BranchNameTextBox.Text = localBranchName;
			BranchNameTextBox.Focus();
			BranchNameTextBox.SelectAll();
		}

		protected override void OnSubmit()
		{
			OutRefspec = BranchNameTextBox.Text;
			CloseWithOk();
		}

	}
}
