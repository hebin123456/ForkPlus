using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.Shell.Commands;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class SshPassphraseWindow : ForkPlusDialogWindow
	{
		private readonly string _sshKeyPath;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				return !string.IsNullOrWhiteSpace(PasswordBox.Password);
			}
		}

		public SshPassphraseWindow(string sshKeyName, string sshKeyPath)
		{
			InitializeComponent();
			_sshKeyPath = sshKeyPath;
			base.DialogTitle = "Passphrase for SSH key";
			base.DialogDescription = string.Format(Translate("Enter passphrase for SSH key '{0}'"), sshKeyName);
			base.SubmitButtonTitle = "OK";
			PasswordBox.PasswordChanged += delegate
			{
				UpdateSubmitButton();
			};
			PasswordBox.Focus();
		}

		protected override void OnSubmit()
		{
			string password = PasswordBox.Password;
			GitCommandResult<ValidateSshKeyShellCommand.Result> gitCommandResult = new ValidateSshKeyShellCommand().Execute(_sshKeyPath, password);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
			}
			if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.IncorrectPassphrase)
			{
				SetStatus(ForkPlusDialogStatus.Warning, Translate("Incorrect passphrase"));
				PasswordBox.Focus();
				PasswordBox.SelectAll();
			}
			else if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.Success)
			{
				WindowsCredentialManager.StoreSshPassphrase(PathHelper.NormalizeUnix(_sshKeyPath), password);
				CloseWithOk();
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
