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
			// Migration note：删除按钮的使能实时跟随选中项（有选中才可删；无选中置灰）。
			// 与添加按钮（DropDownButton）不同，删除作用于"当前选中的密钥"，选中项变化即刷新。
			DeleteSSHKeyButton.IsEnabled = SshKeyListBox.SelectedItem is SshKeyViewModel;
			RefreshDetails();
		}

		private void DeleteSSHKeyButton_Click(object sender, RoutedEventArgs e)
		{
			if (!(SshKeyListBox.SelectedItem is SshKeyViewModel selected))
			{
				return;
			}
			// 修复（2026-09-11，"删除 SSH 密钥没效果、无法从列表里删除"）：
			// 列表由 ~/.ssh 目录扫描装配而来，旧实现仅从 ForkPlus 配置（SshKeys）移除引用，
			// 磁盘私/公钥文件仍在 → Refresh() 重扫又把它加回来，用户感知为"删不掉"。
			// 现在先弹确认框（不可逆操作防误删），确认后真正删除磁盘上的私钥 + 公钥文件，
			// 再从 SshKeys 移除引用，Refresh() 重扫时该密钥自然消失。
		// 修复（2026-09-11，"删除 SSH 密钥确认框置底、点删除没用"）：确认框默认 owner 是 MainWindow，
		// 但本窗口已是模态（MainWindow 已被禁用），确认框挂错模态链导致置底、本窗口仍可交互。
		// 改为把确认框 owner 设成本窗口（与 AddAccountWindow / IR 确认框同款修复）。
		MessageBoxWindow confirmDialog = new MessageBoxWindow(
			string.Format(Translate("Do you want to delete SSH key '{0}'?"), selected.KeyFileName),
			Translate("The private and public key files will be permanently removed from your disk. This action can't be undone."),
			Translate("Delete"),
			Translate("Cancel"),
			showCancelButton: true,
			520.0,
			showWarningIcon: true);
		confirmDialog.SetOwnerCompat(this);
		bool confirmed = confirmDialog.ShowDialog().GetValueOrDefault();
			if (!confirmed)
			{
				return;
			}
			DeleteSshKeyFilesFromDisk(selected.KeyPath);

			string[] sshKeys = ForkPlusSettings.Default.SshKeys;
			List<string> list = new List<string>(sshKeys.Length);
			foreach (string keyPath in sshKeys)
			{
				if (string.Equals(keyPath, selected.KeyPath, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				list.Add(keyPath);
			}
			ForkPlusSettings.Default.SshKeys = list.ToArray();
			ForkPlusSettings.Default.Save();
			Refresh();
			DeleteSSHKeyButton.IsEnabled = false;
			SshKeyListBox.Focus();
		}

		// 删除私钥文件及其同名的 .pub 公钥文件。单独失败不阻断另一个删除，
		// 全部失败向用户报错；任一成功即视为删除完成（列表随之消失）。
		private static void DeleteSshKeyFilesFromDisk(string privateKeyPath)
		{
			if (string.IsNullOrEmpty(privateKeyPath))
			{
				return;
			}
			string publicKeyPath = Path.ChangeExtension(privateKeyPath, ".pub");
			List<string> failed = new List<string>(2);
			if (File.Exists(privateKeyPath))
			{
				try
				{
					File.Delete(privateKeyPath);
				}
				catch (Exception ex)
				{
					Log.Error("Failed to delete SSH private key '" + privateKeyPath + "'", ex);
					failed.Add(privateKeyPath);
				}
			}
			if (File.Exists(publicKeyPath))
			{
				try
				{
					File.Delete(publicKeyPath);
				}
				catch (Exception ex2)
				{
					Log.Error("Failed to delete SSH public key '" + publicKeyPath + "'", ex2);
					failed.Add(publicKeyPath);
				}
			}
			if (failed.Count > 0)
			{
				new MessageBoxWindow(
					Translate("Failed to delete SSH key"),
					string.Format(Translate("The following SSH key files could not be deleted: '{0}'. Please delete them manually."), string.Join("', '", failed)),
					Translate("OK"),
					"Cancel",
					showCancelButton: false).ShowDialog();
			}
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
