using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.Shell;
using ForkPlus.Shell.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Dialogs
{
	public partial class ConfigureSshKeysWindow : ForkPlusDialogWindow
	{

		public ConfigureSshKeysWindow()
		{
			base.ShowLogo = false;
			InitializeComponent();
			base.DialogTitle = Translate("Configure SSH Keys");
			base.DialogDescription = Translate("1. Select or generate a new SSH key which will identify your computer\n2. Copy the public key content to the account section on the website of your git provider");
			base.SubmitButtonTitle = Translate("OK");
			Refresh();
			SshKeyListBox.SelectedIndex = 0;
		}

		protected override void OnSubmit()
		{
			string[] sshKeys = SshKeyListBox.Items.CompactMap((object x) => x as SshKeyViewModel).Filter((SshKeyViewModel x) => x.IsActive).Map((SshKeyViewModel x) => x.KeyPath);
			ForkPlusSettings.Default.SshKeys = sshKeys;
			ForkPlusSettings.Default.Save();
			base.OnSubmit();
		}

		private void SshKeyListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			RefreshDetails();
		}

		private void SshKeyCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			CheckBox checkBox = sender as CheckBox;
			if (checkBox != null && ((checkBox.Parent as DockPanel)?.DataContext is SshKeyViewModel sshKeyViewModel))
			{
				// Migration note：WPF 原仓 Checked/Unchecked 事件触发时 TwoWay 绑定已把 IsChecked 写回
				// VM.IsActive（直接读 VM 是新值）；Avalonia 12 的 IsCheckedChanged 先于绑定回写触发，
				// 从 VM 读到旧值 → 勾选后验证被跳过、配置文本不联动（用户可见）。处理器内直接读
				// IsChecked 同步推回 VM，消除对绑定回写时序的依赖（CreatePartialStashWindow 同款修复）。
				sshKeyViewModel.IsActive = checkBox.IsChecked == true;
				if (sshKeyViewModel.IsActive)
				{
					sshKeyViewModel.IsActive = ValidateSshKey(sshKeyViewModel.KeyFileName, sshKeyViewModel.KeyPath);
				}
				RefreshConfigutationTextBlock();
				RefreshStatus();
			}
		}

		private void GenerateNewSSHKeyMenuItem_Click(object sender, RoutedEventArgs e)
		{
			GenerateNewSshKeyWindow generateNewSshKeyWindow = new GenerateNewSshKeyWindow();
			generateNewSshKeyWindow.SetOwnerCompat(this);
			if (!generateNewSshKeyWindow.ShowDialog().GetValueOrDefault())
			{
				return;
			}
			if (!generateNewSshKeyWindow.GitResult.Succeeded)
			{
				new ErrorWindow(null, generateNewSshKeyWindow.GitResult.Error).ShowDialog();
				return;
			}
			string resultKey = generateNewSshKeyWindow.ResultKey;
			if (resultKey != null)
			{
				Refresh();
				ActivateAndSelectSshKey(resultKey);
			}
		}

		private void BrowseKeyMenuItem_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = SystemEnvironment.UserProfileDirectory;
			if (OpenDialog.SelectFile(this, "Select SSH key", initialDirectory, "SSH key", "*.pub", out var filePath))
			{
				string[] sshKeys = ForkPlusSettings.Default.SshKeys;
				List<string> list = new List<string>(sshKeys.Length + 1);
				list.AddRange(sshKeys);
				list.Add(Path.ChangeExtension(filePath, null));
				ForkPlusSettings.Default.SshKeys = list.ToArray();
				Refresh();
				SelectAndFocusSshKey(Path.GetFileNameWithoutExtension(filePath));
			}
		}

		private void CopyPublicKey_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			ServiceLocator.Clipboard.SetText(SshKeyPublicKeyTextBox.Text);
		}

		private void ActivateAndSelectSshKey(string keyName)
		{
			SshKeyViewModel sshKeyViewModel = IReadOnlyListExtensions.FirstItem(SshKeyListBox.Items.CompactMap((object x) => x as SshKeyViewModel), (SshKeyViewModel x) => x.KeyFileName == keyName);
			if (sshKeyViewModel != null)
			{
				sshKeyViewModel.IsActive = true;
				SshKeyListBox.SelectedItem = sshKeyViewModel;
				SshKeyListBox.Focus();
			}
		}

		private void SelectAndFocusSshKey(string keyName)
		{
			SshKeyViewModel sshKeyViewModel = IReadOnlyListExtensions.FirstItem(SshKeyListBox.Items.CompactMap((object x) => x as SshKeyViewModel), (SshKeyViewModel x) => x.KeyFileName == keyName);
			if (sshKeyViewModel != null)
			{
				SshKeyListBox.SelectedItem = sshKeyViewModel;
				SshKeyListBox.Focus();
			}
		}

		private bool ValidateSshKey(string keyName, string keyPath)
		{
			GitCommandResult<ValidateSshKeyShellCommand.Result> gitCommandResult = new ValidateSshKeyShellCommand().Execute(keyPath);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
				return false;
			}
			if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.Success)
			{
				return true;
			}
			if (gitCommandResult.Result == ValidateSshKeyShellCommand.Result.IncorrectPassphrase)
			{
				return new SshPassphraseWindow(keyName, keyPath)
					.SetOwnerAndCenter(this).ShowDialog().GetValueOrDefault(); // Migration note：WPF { Owner=this } → 链式扩展。
			}
			return false;
		}

		private void Refresh()
		{
			List<SshKeyViewModel> list = new List<SshKeyViewModel>(new GetLocalSshKeysCommand().Execute().Map((SshKey x) => new SshKeyViewModel(x)));
			string[] sshKeys = ForkPlusSettings.Default.SshKeys;
			foreach (string activeKeyPath in sshKeys)
			{
				SshKeyViewModel sshKeyViewModel = list.FirstOrDefault((SshKeyViewModel x) => x.KeyPath == activeKeyPath);
				if (sshKeyViewModel != null)
				{
					sshKeyViewModel.IsActive = true;
					continue;
				}
				SshKey customSshKey = GetCustomSshKey(activeKeyPath);
				if (customSshKey != null)
				{
					list.Add(new SshKeyViewModel(customSshKey, isActive: true));
				}
			}
			list.Sort((SshKeyViewModel x, SshKeyViewModel y) => x.KeyFileName.CompareTo(y.KeyFileName));
			SshKeyListBox.ItemsSource = list;
			if (list.Count == 0)
			{
				FallbackUserControl.Show();
				DetailsFallbackUserControl.Show();
			}
			else
			{
				FallbackUserControl.Collapse();
				DetailsFallbackUserControl.Collapse();
			}
			RefreshConfigutationTextBlock();
			RefreshStatus();
		}

		private void RefreshConfigutationTextBlock()
		{
			string[] array = SshKeyListBox.Items.CompactMap((object x) => x as SshKeyViewModel).Filter((SshKeyViewModel x) => x.IsActive).Map((SshKeyViewModel x) => x.KeyFileName);
			StringBuilder stringBuilder = new StringBuilder(array.Length);
			string[] array2 = array;
			foreach (string value in array2)
			{
				if (stringBuilder.Length != 0)
				{
					stringBuilder.Append(", ");
				}
				stringBuilder.Append(value);
			}
			if (stringBuilder.Length == 0)
			{
				stringBuilder.Append(Translate("default system ssh-agent"));
				SshConfigurationTextBlock.FontStyle = FontStyles.Italic;
				SshConfigurationIcon.Collapse();
			}
			else
			{
				SshConfigurationTextBlock.FontStyle = FontStyles.Normal;
				SshConfigurationIcon.Show();
			}
			SshConfigurationTextBlock.Text = stringBuilder.ToString();
		}

		private void RefreshStatus()
		{
			if (SshKeyListBox.Items.CompactMap((object x) => x as SshKeyViewModel).Filter((SshKeyViewModel x) => x.IsActive).Count > 1)
			{
				SetStatus(ForkPlusDialogStatus.Warning, Translate("Note: you can't use multiple SSH keys with the same server"));
			}
			else
			{
				SetStatus(ForkPlusDialogStatus.None, "");
			}
		}

		private void RefreshDetails()
		{
			SshKeyViewModel sshKeyViewModel = SshKeyListBox.SelectedItem as SshKeyViewModel;
			SshKeyPathTextBlock.Text = sshKeyViewModel?.KeyPath ?? "";
			global::Avalonia.Controls.ToolTip.SetTip(SshKeyPathTextBlock,sshKeyViewModel?.KeyPath ?? "");
			SshKeySha256TextBox.Text = sshKeyViewModel?.Sha256 ?? "";
			SshKeyPublicKeyTextBox.Text = sshKeyViewModel?.PublicKey ?? "";
		}

		private static SshKey GetCustomSshKey(string privateKeyFilePath)
		{
			if (!File.Exists(privateKeyFilePath))
			{
				new ErrorWindow(string.Format(Translate("Cannot find private key: '{0}'"), privateKeyFilePath)).ShowDialog();
				return null;
			}
			string text = Path.ChangeExtension(privateKeyFilePath, ".pub");
			if (!File.Exists(text))
			{
				new ErrorWindow(string.Format(Translate("Cannot find public key: '{0}'"), text)).ShowDialog();
				return null;
			}
			try
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);
				string rawPublicKey = File.ReadAllText(text);
				return new SshKey(privateKeyFilePath, fileNameWithoutExtension, rawPublicKey);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read '" + text + "'", ex);
				return null;
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
