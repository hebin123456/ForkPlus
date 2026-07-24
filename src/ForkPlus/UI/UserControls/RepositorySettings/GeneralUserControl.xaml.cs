// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Documents → using Avalonia.Controls.Documents（Hyperlink）
// - using System.Windows.Markup → 移除
// - using System.Windows.Navigation → 移除（Avalonia 无 RequestNavigateEventArgs）
// - SelectionChangedEventArgs/TextChangedEventArgs → Avalonia.Controls 同名类型
// - Hyperlink.RequestNavigate + RequestNavigateEventArgs.Uri → Hyperlink.Click + Hyperlink.NavigateUri（参考 HighlightingTextBlockExtensions）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.RepositorySettings
{
	public partial class GeneralUserControl : UserControl
	{
		public class LocalBranchItem : INotifyPropertyChanged
		{
			public LocalBranch LocalBranch { get; }

			public LocalBranchItemType ItemType { get; }

			public string Title { get; }

			public event PropertyChangedEventHandler PropertyChanged;

			public static LocalBranchItem CreateLocalBranchItem(LocalBranch localBranch)
			{
				return new LocalBranchItem(localBranch.Name, LocalBranchItemType.Branch, localBranch);
			}

			public static LocalBranchItem CreateDefaultItem(string title)
			{
				return new LocalBranchItem(title, LocalBranchItemType.Default);
			}

			public static LocalBranchItem CreateSeparator()
			{
				return new LocalBranchItem("", LocalBranchItemType.Separator);
			}

			private LocalBranchItem(string title, LocalBranchItemType type, [Null] LocalBranch localBranch = null)
			{
				Title = title;
				ItemType = type;
				LocalBranch = localBranch;
			}
		}

		public enum LocalBranchItemType
		{
			Default,
			Separator,
			Branch
		}

		private GitModule _gitModule;

		private bool _updateInProgress;

		private bool _saveLocalIdentityReqired;

		private DelayedAction<UserIdentity> _updateAvatarAction;

		public GeneralUserControl()
		{
			InitializeComponent();
			_updateAvatarAction = new DelayedAction<UserIdentity>(UpdateAvatar, 0.3);
		}

		public void Initialize(GitModule gitModule)
		{
			_gitModule = gitModule;
			Refresh();
		}

		public void Save()
		{
			if (_saveLocalIdentityReqired)
			{
				SaveLocalIdentity();
			}
			_gitModule.Settings.Save();
		}

		private void UseGlobalGitCredentialsCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (!_updateInProgress)
			{
				SaveLocalIdentity();
				Refresh();
			}
		}

		private void NoFastForwardCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (!_updateInProgress)
			{
				_gitModule.Settings.LeanBranchingNoFastForward = NoFastForwardCheckBox.IsChecked.GetValueOrDefault();
				_gitModule.Settings.Save();
			}
		}

		private void Hyperlink_Click(object sender, RoutedEventArgs e)
	{
		// 阶段 4.5：WPF Hyperlink.RequestNavigate + RequestNavigateEventArgs.Uri
		// → Avalonia Hyperlink.Click + Hyperlink.NavigateUri（参考 HighlightingTextBlockExtensions）。
		if (sender is Hyperlink hyperlink && hyperlink.NavigateUri != null)
		{
			hyperlink.NavigateUri.OpenInBrowser();
		}
		e.Handled = true;
	}

		private void SaveLocalIdentity()
		{
			UserIdentity identity = (UseGlobalGitCredentialsCheckBox.IsChecked.GetValueOrDefault() ? null : new UserIdentity(UserNameTextBox.Text.Trim(), EmailTextBox.Text.Trim()));
			new SetRepositoryLocalUserIdentityGitCommand().Execute(_gitModule, identity);
			_saveLocalIdentityReqired = false;
		}

		private void UserNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress)
			{
				_saveLocalIdentityReqired = true;
				_updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
			}
		}

		private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress)
			{
				_saveLocalIdentityReqired = true;
				_updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
			}
		}

		private void UpdateAvatar(UserIdentity userIdentity)
		{
			AuthorAvatarImage.ShowAvatarNoCache(userIdentity);
		}

		private void MainBranchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!_updateInProgress)
			{
				LocalBranchItem localBranchItem = MainBranchComboBox.SelectedItem as LocalBranchItem;
				_gitModule.Settings.LeanBranchingMainBranch = localBranchItem.LocalBranch?.Name;
				_gitModule.Settings.Save();
			}
		}

		private void TabWidthTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress)
			{
				if (!int.TryParse(TabWidthTextBox.Text, out var result))
				{
					result = _gitModule.Settings.TabWidth;
				}
				_gitModule.Settings.TabWidth = result;
				_gitModule.Settings.Save();
			}
		}

		private void Refresh()
		{
			RepositoryData repositoryData = MainWindow.ActiveRepositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			_updateInProgress = true;
			GitCommandResult<UserIdentity> gitCommandResult = new GetRepositoryIdentityGitCommand().Execute(_gitModule, GitConfigFileOption.Local);
			if (gitCommandResult.Succeeded)
			{
				UseGlobalGitCredentialsCheckBox.IsChecked = false;
				UserNameTextBox.Text = gitCommandResult.Result.Name;
				EmailTextBox.Text = gitCommandResult.Result.Email;
				UserNameTextBox.IsEnabled = true;
				EmailTextBox.IsEnabled = true;
				_updateAvatarAction.InvokeNow(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
			}
			else
			{
				GitCommandResult<UserIdentity> gitCommandResult2 = new GetRepositoryIdentityGitCommand().Execute(_gitModule, GitConfigFileOption.Global);
				UseGlobalGitCredentialsCheckBox.IsChecked = true;
				UserNameTextBox.Text = gitCommandResult2.Result?.Name ?? "";
				EmailTextBox.Text = gitCommandResult2.Result?.Email ?? "";
				UserNameTextBox.IsEnabled = false;
				EmailTextBox.IsEnabled = false;
				_updateAvatarAction.InvokeNow(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
			}
			LocalBranch[] localBranches = repositoryData.References.LocalBranches;
			LocalBranch localBranch = IReadOnlyListExtensions.FirstItem(localBranches, (LocalBranch x) => x.Name == "develop") ?? IReadOnlyListExtensions.FirstItem(localBranches, (LocalBranch x) => x.Name == "main") ?? IReadOnlyListExtensions.FirstItem(localBranches, (LocalBranch x) => x.Name == "master");
			string title = Translate("default (develop, main or master)");
			if (localBranch != null)
			{
				title = string.Format(Translate("default ({0})"), localBranch.Name);
			}
			List<LocalBranchItem> list = new List<LocalBranchItem>(localBranches.Length + 2);
			LocalBranchItem localBranchItem = LocalBranchItem.CreateDefaultItem(title);
			list.Add(localBranchItem);
			list.Add(LocalBranchItem.CreateSeparator());
			LocalBranchItem selectedItem = localBranchItem;
			string mainBranchName = _gitModule.Settings.LeanBranchingMainBranch;
			LocalBranch localBranch2 = IReadOnlyListExtensions.FirstItem(localBranches, (LocalBranch x) => x.Name == mainBranchName);
			LocalBranchItem[] array = localBranches.Map((LocalBranch x) => LocalBranchItem.CreateLocalBranchItem(x));
			foreach (LocalBranchItem localBranchItem2 in array)
			{
				list.Add(localBranchItem2);
				if (localBranchItem2.LocalBranch == localBranch2)
				{
					selectedItem = localBranchItem2;
				}
			}
			MainBranchComboBox.ItemsSource = list;
			MainBranchComboBox.SelectedItem = selectedItem;
			NoFastForwardCheckBox.IsChecked = _gitModule.Settings.LeanBranchingNoFastForward;
			TabWidthTextBox.Text = _gitModule.Settings.TabWidth.ToString();
			_updateInProgress = false;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
