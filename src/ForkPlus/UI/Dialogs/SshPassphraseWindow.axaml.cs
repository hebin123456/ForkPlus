using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.Shell.Commands;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

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
				return !string.IsNullOrWhiteSpace(PasswordBox.Text);
			}
		}

		public SshPassphraseWindow(string sshKeyName, string sshKeyPath)
		{
			InitializeComponent();
			_sshKeyPath = sshKeyPath;
			base.DialogTitle = Translate("Passphrase for SSH key");
			base.DialogDescription = string.Format(Translate("Enter passphrase for SSH key '{0}'"), sshKeyName);
			base.SubmitButtonTitle = Translate("OK");
			PasswordBox.TextChanged += delegate
			{
				UpdateSubmitButton();
			};
			PasswordBox.Focus(); // Migration note：WPF PasswordBox.Focus() 被误转成类型静态调用。
		}

		protected override void OnSubmit()
		{
			string password = PasswordBox.Text;
			GitCommandResult<ValidateSshKeyShellCommand.Result> gitCommandResult = new ValidateSshKeyShellCommand().Execute(_sshKeyPath, password);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
			}
			if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.IncorrectPassphrase)
			{
				SetStatus(ForkPlusDialogStatus.Warning, Translate("Incorrect passphrase"));
				PasswordBox.Focus(); // Migration note：WPF PasswordBox.Focus() 被误转成类型静态调用。
				PasswordBox.SelectAll(); // Migration note：WPF PasswordBox.SelectAll() 被误转成类型静态调用。
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
