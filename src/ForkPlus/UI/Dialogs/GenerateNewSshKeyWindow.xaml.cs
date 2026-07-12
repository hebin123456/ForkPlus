using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.Shell;
using ForkPlus.Shell.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class GenerateNewSshKeyWindow : ForkPlusDialogWindow
	{
		private readonly SshKey[] _existingSshKeys;

		[Null]
		public string ResultKey { get; private set; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				if (string.IsNullOrEmpty(KeyFileNameTextBox.Text))
				{
					return false;
				}
				try
				{
					if (_existingSshKeys.Any((SshKey x) => Path.GetFileNameWithoutExtension(x.FilePath) == KeyFileNameTextBox.Text))
					{
						SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Ssh key '{0}' already exists"), KeyFileNameTextBox.Text));
						return false;
					}
				}
				catch (Exception ex)
				{
					SetStatus(ForkPlusDialogStatus.Error, ex.ToString());
					return false;
				}
				if (string.IsNullOrEmpty(EmailTextBox.Text))
				{
					return false;
				}
				return true;
			}
		}

		public GenerateNewSshKeyWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("Generate new SSH Key");
			base.DialogDescription = Translate("Generate new ED25519 key");
			base.SubmitButtonTitle = Translate("Generate");
			_existingSshKeys = new GetLocalSshKeysCommand().Execute();
		}

		protected override async void OnSubmit()
		{
			try
			{
				string keyName = KeyFileNameTextBox.Text;
				string email = EmailTextBox.Text;
				DisableEditableControls();
				SetStatus(ForkPlusDialogStatus.InProgress, Translate("Generating..."));
				GitCommandResult gitCommandResult = await Task.Run(() => new GenerateSshKeyShellCommand().Execute(email, keyName));
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				EnableEditableControls();
				if (gitCommandResult.Succeeded)
				{
					ResultKey = keyName;
				}
				Close(gitCommandResult);
			}
			catch (Exception ex)
			{
				Log.Error("OnSubmit failed", ex);
			}
		}

		private void KeyFileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
