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

	// 阶段 3：承接 key 名/邮箱校验（含重名警告+异常错误）+ ssh-keygen 命令预览。
	// Validate() 返回 (IsAllowed, Status, StatusMessage)，View override 据此调 SetStatus。
	private readonly GenerateNewSshKeyWindowViewModel _viewModel;

	[Null]
	public string ResultKey { get; private set; }

	protected override bool IsSubmitAllowed
	{
		get
		{
			_viewModel.KeyFileName = KeyFileNameTextBox.Text;
			_viewModel.Email = EmailTextBox.Text;
			(bool isAllowed, ForkPlusDialogStatus status, string statusMessage) = _viewModel.Validate();
			SetStatus(status, statusMessage ?? string.Empty);
			return isAllowed;
		}
	}

		public GenerateNewSshKeyWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("Generate new SSH Key");
			base.DialogDescription = Translate("Generate new ED25519 key");
			base.SubmitButtonTitle = Translate("Generate");
			_existingSshKeys = new GetLocalSshKeysCommand().Execute();
		_viewModel = new GenerateNewSshKeyWindowViewModel(_existingSshKeys);
	}

		protected override string GetCommandPreview()
	{
		_viewModel.KeyFileName = KeyFileNameTextBox.Text;
		_viewModel.Email = EmailTextBox.Text;
		return _viewModel.CommandPreview;
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
			RefreshCommandPreview();
		}

		private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
