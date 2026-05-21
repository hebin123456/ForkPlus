using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class AskPassWindow : ForkPlusDialogWindow
	{
		[Null]
		private AskPassRequest _askPassRequest;

		private string _arguments;

		public string Result { get; private set; }

		public AskPassWindow(string arguments, string repositoryPath)
		{
			InitializeComponent();
			_askPassRequest = AskPassRequest.Parse(arguments);
			_arguments = arguments;
			RememberCheckBox.Hide();
			base.DialogTitle = ((repositoryPath != "") ? Path.GetFileName(repositoryPath) : PreferencesLocalization.Current("Credentials Required"));
			if (_arguments.StartsWith("Username for"))
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("User Name:");
				InputTextBox.Show();
				InputPasswordBox.Hide();
				InputTextBox.Focus();
			}
			else if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Passphrase for SSH key '{0}'", sshPassphrase.KeyPath);
				InputTextBlock.Text = PreferencesLocalization.Current("Passphrase:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				RememberCheckBox.Show();
			}
			else if (_arguments.StartsWith("Enter passphrase"))
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Passphrase:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword)
			{
				base.DialogDescription = PreferencesLocalization.FormatCurrent("Passphrase for '{0}'", sshUserPassword.Username + "@" + sshUserPassword.Url.Host);
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				RememberCheckBox.Show();
			}
			else
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			base.SubmitButtonTitle = "OK";
		}

		protected override void OnSubmit()
		{
			if (_arguments.StartsWith("Username for"))
			{
				Result = InputTextBox.Text;
			}
			else
			{
				Result = InputPasswordBox.Password;
			}
			if (_askPassRequest is AskPassRequest.SshPassphrase sshPassphrase)
			{
				if (RememberCheckBox.IsChecked.GetValueOrDefault())
				{
					WindowsCredentialManager.StoreSshPassphrase(sshPassphrase.KeyPath, Result);
				}
			}
			else if (_arguments.StartsWith("Enter passphrase"))
			{
				string text = AskPassParser.ParseSshKey(_arguments);
				if (!string.IsNullOrEmpty(text))
				{
					WindowsCredentialManager.StoreSshPassphrase(text, Result);
				}
			}
			else if (_askPassRequest is AskPassRequest.SshUserPassword sshUserPassword && RememberCheckBox.IsChecked.GetValueOrDefault())
			{
				WindowsCredentialManager.StoreSshUserPassword(sshUserPassword.Url, sshUserPassword.Username, Result);
			}
			Close();
		}

	}
}
