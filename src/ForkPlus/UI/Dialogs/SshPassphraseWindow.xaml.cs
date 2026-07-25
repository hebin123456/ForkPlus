using Avalonia.Controls;
using System;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.Shell.Commands;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class SshPassphraseWindow : ForkPlusDialogWindow
	{
		private readonly string _sshKeyPath;

		// 阶段 3：承接 passphrase 非空校验（纯判断，SetStatus 副作用留 override）。
		private readonly SshPassphraseWindowViewModel _viewModel = new SshPassphraseWindowViewModel();

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, "");
				_viewModel.Passphrase = PasswordBox.Password;
				return _viewModel.IsSubmitAllowed;
			}
		}

		public SshPassphraseWindow(string sshKeyName, string sshKeyPath)
		{
			InitializeComponent();
			_sshKeyPath = sshKeyPath;
			base.DialogTitle = Translate("Passphrase for SSH key");
			base.DialogDescription = string.Format(Translate("Enter passphrase for SSH key '{0}'"), sshKeyName);
			base.SubmitButtonTitle = Translate("OK");
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
