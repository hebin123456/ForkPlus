using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public partial class AskPassWindow : ForkPlusDialogWindow
	{
		[Null]
		private AskPassRequest _askPassRequest;

		private string _arguments;

		[Null]
		private string _httpsUsernameHost;

		[Null]
		private string _httpsPasswordHost;

		[Null]
		private string _httpsPasswordUsername;

		public string Result { get; private set; }

		public AskPassWindow(string arguments, string repositoryPath)
		{
			InitializeComponent();
			_askPassRequest = AskPassRequest.Parse(arguments);
			_arguments = arguments;
			RememberCheckBox.Hide();
			RememberPasswordCheckBox.Hide();
			NeverAskCheckBox.Hide();
			base.DialogTitle = ((repositoryPath != "") ? Path.GetFileName(repositoryPath) : PreferencesLocalization.Current("Credentials Required"));
			if (_arguments.StartsWith("Username for"))
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("User Name:");
				InputTextBox.Show();
				InputPasswordBox.Hide();
				InputTextBox.Focus();
				// 凭据记忆（Layer D）第一档：自动记住上次输入的账号（默认行为，无勾选框），
				// 本次询问预填已记住账号
				if (SavedCredentialStore.TryParseUsernamePrompt(_arguments, out _httpsUsernameHost))
				{
					SavedCredentialStore.SavedCredential saved = SavedCredentialStore.Current.FindEntry(_httpsUsernameHost);
					if (saved?.Username != null)
					{
						InputTextBox.Text = saved.Username;
					}
				}
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
			else if (SavedCredentialStore.TryParsePasswordPrompt(_arguments, out _httpsPasswordHost, out _httpsPasswordUsername))
			{
				// 凭据记忆（Layer D）第二/三档：HTTP(S) 密码询问——已记住的密码预填密码框
				// （弹窗内自动填充），"记住密码"随存量预勾选；"不再弹出"选项可升级为第三档
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
				SavedCredentialStore.SavedCredential saved = SavedCredentialStore.Current.FindEntry(_httpsPasswordHost);
				if (saved != null)
				{
					if (saved.HasPassword)
					{
						InputPasswordBox.Text = saved.Password;
						RememberPasswordCheckBox.IsChecked = true;
					}
				}
				RememberPasswordCheckBox.Show();
				NeverAskCheckBox.Show();
			}
			else
			{
				base.DialogDescription = _arguments;
				InputTextBlock.Text = PreferencesLocalization.Current("Password:");
				InputTextBox.Hide();
				InputPasswordBox.Show();
				InputPasswordBox.Focus();
			}
			base.SubmitButtonTitle = PreferencesLocalization.Current("OK");
		}

		protected override void OnSubmit()
		{
			if (_arguments.StartsWith("Username for"))
			{
				Result = InputTextBox.Text;
				// 凭据记忆（Layer D）第一档：自动记住上次输入的账号（默认行为，无勾选框）
				if (_httpsUsernameHost != null && !string.IsNullOrEmpty(Result))
				{
					SavedCredentialStore.Current.RememberUsername(_httpsUsernameHost, Result);
				}
			}
			else if (_httpsPasswordHost != null)
			{
				Result = InputPasswordBox.Text;
				if (NeverAskCheckBox.IsChecked.GetValueOrDefault() && !string.IsNullOrEmpty(Result))
				{
					// 第三档"记住密码 + 不再弹出"：密码落盘 + 标记，之后全链路静默回填
					SavedCredentialStore.Current.RememberPassword(_httpsPasswordHost, _httpsPasswordUsername, Result);
					SavedCredentialStore.Current.SetNeverAsk(_httpsPasswordHost, true);
				}
				else if (RememberPasswordCheckBox.IsChecked.GetValueOrDefault() && !string.IsNullOrEmpty(Result))
				{
					// 第二档"记住密码"：密码落盘，下次弹窗密码框预填；顺带清残留的第三档标记
					SavedCredentialStore.Current.RememberPassword(_httpsPasswordHost, _httpsPasswordUsername, Result);
					SavedCredentialStore.Current.SetNeverAsk(_httpsPasswordHost, false);
				}
				else
				{
					// 不勾选：停止记住密码（清存量，账号记忆保留）
					SavedCredentialStore.Current.ForgetPassword(_httpsPasswordHost);
				}
			}
			else
			{
				Result = InputPasswordBox.Text;
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
