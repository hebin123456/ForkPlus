using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;

namespace ForkPlus.UI.Dialogs
{
	public partial class EditRemoteWindow : ForkPlusDialogWindow
	{
		public class AccountItem : INotifyPropertyChanged
		{
			public AccountItemType ItemType { get; }

			[Null]
			public Account Account { get; }

			public ImageSource Icon { get; }

			public string Title { get; }

			public string Description { get; }

			public event PropertyChangedEventHandler PropertyChanged;

			public static AccountItem CreateDefaultItem(Account account)
			{
				return new AccountItem(AccountItemType.Default, account, "default", "(" + account.Username + ")");
			}

			public static AccountItem CreateAccountItem(Account account)
			{
				return new AccountItem(AccountItemType.Account, account, account.ServiceType.FriendlyName(), "(" + account.Username + ")");
			}

			public static AccountItem CreateSeparator()
			{
				return new AccountItem(AccountItemType.Separator, null, "", "");
			}

			public static AccountItem CreateCustom(string userName)
			{
				return new AccountItem(AccountItemType.Custom, null, "custom", "(" + userName + ")");
			}

			private AccountItem(AccountItemType type, [Null] Account account, string title, string description)
			{
				ItemType = type;
				Account = account;
				Title = title;
				Description = description;
				Icon = account?.ServiceType.Icon();
			}
		}

		public enum AccountItemType
		{
			Default,
			Account,
			Separator,
			Custom
		}

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		private readonly Remote[] _remotes;

		private readonly Remote _remoteToEdit;

		private bool _refreshingUrl;

		protected override bool IsSubmitAllowed
		{
			get
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				string remoteName = RemoteNameTextBox.Text.Trim();
				string text = RepositoryUrlTextBox.Text.Trim();
				if (string.IsNullOrWhiteSpace(remoteName) || string.IsNullOrWhiteSpace(text))
				{
					return false;
				}
				if (_remoteToEdit?.Name == remoteName && _remoteToEdit?.Url == text)
				{
					return false;
				}
				string text2 = ReferenceNameValidator.Validate(remoteName);
				if (text2 != null)
				{
					SetStatus(ForkPlusDialogStatus.Warning, text2);
					return false;
				}
				if (_remoteToEdit?.Name != remoteName && _remotes.AnyItem((Remote x) => x.Name == remoteName))
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Remote '" + remoteName + "' already exists");
					return false;
				}
				return true;
			}
		}

		public EditRemoteWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule, Remote remoteToEdit = null)
		{
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			_remoteToEdit = remoteToEdit;
			GitCommandResult<RepositoryRemotes> gitCommandResult = new GetRemotesGitCommand().Execute(_gitModule);
			_remotes = gitCommandResult.Result?.Items ?? new Remote[0];
			InitializeComponent();
			InitializeRemoteWindow();
		}

		protected override void OnActivated(EventArgs e)
		{
			base.OnActivated(e);
			HideStatusControls();
		}

		protected override void OnSubmit()
		{
			string newName = RemoteNameTextBox.Text.Trim();
			string newUrl = RepositoryUrlTextBox.Text.Trim();
			bool edit = _remoteToEdit != null;
			Remote remoteToEdit = _remoteToEdit;
			bool syncWithParent = SyncCheckBox.IsChecked.GetValueOrDefault();
			GitModule gitModule = _gitModule;
			string name = (edit ? ("Edit remote '" + _remoteToEdit.Name + "'") : ("Add remote '" + newName + "'"));
			DisableEditableControls();
			_repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				if (edit)
				{
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, "Editing remote...");
					});
					GitCommandResult result2 = GitCommandResult.Success();
					if (remoteToEdit.Url != newUrl)
					{
						if (gitModule.Type == ModuleType.Submodule && syncWithParent)
						{
							GitCommandResult<GitModule> openParentGitModuleResult = new OpenGitRepositoryGitCommand().Execute(gitModule.ParentRepoPath);
							if (!openParentGitModuleResult.Succeeded)
							{
								base.Dispatcher.Async(delegate
								{
									Close(openParentGitModuleResult.ToGitCommandResult());
								});
								return;
							}
							GitModule result3 = openParentGitModuleResult.Result;
							GitCommandResult<Submodule[]> getSubmodulesResult = new GetSubmodulesGitCommand().Execute(result3);
							if (!getSubmodulesResult.Succeeded)
							{
								base.Dispatcher.Async(delegate
								{
									Close(getSubmodulesResult.ToGitCommandResult());
								});
							}
							string submodule = "";
							Submodule[] result4 = getSubmodulesResult.Result;
							foreach (Submodule submodule2 in result4)
							{
								if (PathHelper.NormalizeUnix(gitModule.Path).EndsWith(submodule2.Path))
								{
									submodule = submodule2.Path;
								}
							}
							GitCommandResult updateSubmoduleUrlResult = new UpdateSubmoduleUrlGitCommand().Execute(result3, submodule, newUrl, monitor);
							if (!updateSubmoduleUrlResult.Succeeded)
							{
								base.Dispatcher.Async(delegate
								{
									Close(updateSubmoduleUrlResult);
								});
							}
						}
						else
						{
							result2 = new EditRemoteUrlGitCommand().Execute(gitModule, remoteToEdit.Name, newUrl, monitor);
							if (!result2.Succeeded)
							{
								base.Dispatcher.Async(delegate
								{
									Close(result2);
								});
								return;
							}
						}
					}
					if (remoteToEdit.Name != newName)
					{
						result2 = new RenameRemoteGitCommand().Execute(gitModule, remoteToEdit.Name, newName, monitor);
					}
					base.Dispatcher.Async(delegate
					{
						Close(result2);
					});
				}
				else
				{
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, "Adding remote...");
					});
					GitCommandResult result = new AddRemoteGitCommand().Execute(gitModule, newName, newUrl, monitor);
					base.Dispatcher.Async(delegate
					{
						Close(result);
					});
				}
			}, JobFlags.SaveToLog);
		}

		private void RemoteNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			HideStatusControls();
		}

		private void RepositoryUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			HideStatusControls();
			RefreshNetworkProtocolButton();
			RefreshSyncCheckBox();
			if (!_refreshingUrl)
			{
				RefreshAccountsComboBox();
			}
		}

		private void AccountsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.RemovedItems.Count != 0)
			{
				AccountItem accountItem = e.AddedItems.FirstItem<AccountItem>();
				if (accountItem != null)
				{
					RefreshUrlUserName(accountItem);
					RefreshAccountsComboBox();
				}
			}
		}

		private void TestButton_Click(object sender, RoutedEventArgs e)
		{
			string newUrl = RepositoryUrlTextBox.Text.Trim();
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			DisableEditableControls();
			StatusImage.Hide();
			BusyIndicator.Show();
			StatusTextBlock.Show();
			StatusTextBlock.Text = PreferencesLocalization.Current("Connecting...");
			TestButton.Collapse();
			_repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("Test connection"), delegate(JobMonitor monitor)
			{
				GitCommandResult result = new TestRemoteRepositoryConnectionGitCommand().Execute(newUrl, monitor);
				base.Dispatcher.Async(delegate
				{
					EnableEditableControls();
					BusyIndicator.Hide();
					StatusImage.Show();
					if (!result.Succeeded)
					{
						StatusImage.Source = new BitmapImage(ForkPlusDialogWindow.WarningIcon);
						if (result.Error is GitCommandError.CallbackUnknownError callbackUnknownError)
						{
							if (callbackUnknownError.FullOutput.Contains("Permission denied (publickey)"))
							{
								StatusTextBlock.Show();
								StatusTextBlock.Text = PreferencesLocalization.Current("Permission denied (publickey)");
								ConfigureSSHKeyButton.Show();
								TestButton.Collapse();
							}
							else if (callbackUnknownError.FullOutput.Contains("not found"))
							{
								StatusTextBlock.Show();
								StatusTextBlock.Text = PreferencesLocalization.Current("Repository not found");
								TestButton.Collapse();
							}
							else
							{
								new ErrorWindow(repositoryUserControl, result.Error).ShowDialog();
							}
						}
					}
					else
					{
						StatusImage.Source = new BitmapImage(ForkPlusDialogWindow.SuccessIcon);
						StatusTextBlock.Show();
						StatusTextBlock.Text = PreferencesLocalization.Current("Connection succeeded");
					}
				});
			}, JobFlags.Hidden);
		}

		private void ConfigureSSHKeyButton_Click(object sender, RoutedEventArgs e)
		{
			MainWindow.Commands.ShowConfigureSSHKeysWindow.Execute();
			HideStatusControls();
		}

		private void NetworkProtocolContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
			RepositoryUrlBuilder repositoryUrlBuilder = new RepositoryUrlBuilder(RepositoryUrlTextBox.Text.Trim());
			string httpsUrlString = repositoryUrlBuilder.CreateHttpsUrlString();
			if (httpsUrlString != null)
			{
				MenuItem menuItem = new MenuItem
				{
					Header = httpsUrlString
				};
				menuItem.Click += delegate
				{
					RepositoryUrlTextBox.Text = httpsUrlString;
				};
				contextMenu.Items.Add(menuItem);
			}
			string sshUrlString = repositoryUrlBuilder.CreateSshUrlString();
			if (sshUrlString != null)
			{
				MenuItem menuItem2 = new MenuItem
				{
					Header = sshUrlString
				};
				menuItem2.Click += delegate
				{
					RepositoryUrlTextBox.Text = sshUrlString;
				};
				contextMenu.Items.Add(menuItem2);
			}
		}

		private void RefreshNetworkProtocolButton()
		{
			GitUrl gitUrl = new GitUrl(RepositoryUrlTextBox.Text.Trim());
			if (gitUrl.IsValid)
			{
				NetworkProtocolDropDownButton.Show();
				if (gitUrl.Protocol == GitUrl.NetworkProtocol.Https)
				{
					NetworkProtocolTextBlock.Text = "HTTPS";
				}
				else if (gitUrl.Protocol == GitUrl.NetworkProtocol.Ssh)
				{
					NetworkProtocolTextBlock.Text = "SSH";
				}
				else
				{
					NetworkProtocolDropDownButton.Hide();
				}
			}
			else
			{
				NetworkProtocolDropDownButton.Hide();
			}
		}

		private void HideStatusControls()
		{
			StatusImage.Hide();
			TestButton.Show();
			StatusTextBlock.Collapse();
			ConfigureSSHKeyButton.Collapse();
		}

		private void RefreshSyncCheckBox()
		{
			if (_remoteToEdit != null && _gitModule.Type == ModuleType.Submodule && RepositoryUrlTextBox.Text.Trim() != _remoteToEdit.Url)
			{
				SyncCheckBox.Show();
			}
			else
			{
				SyncCheckBox.Collapse();
			}
		}

		private void InitializeRemoteWindow()
		{
			if (_remoteToEdit != null)
			{
				base.DialogTitle = PreferencesLocalization.Current("Edit Remote");
				base.DialogDescription = PreferencesLocalization.Current("Edit URL of the remote repository");
				base.SubmitButtonTitle = PreferencesLocalization.Current("Edit");
				RepositoryUrlTextBox.Text = _remoteToEdit.Url;
				RemoteNameTextBox.Text = _remoteToEdit.Name;
				RemoteNameTextBox.Icon = _remoteToEdit.Icon;
				RemoteNameTextBox.SelectAll();
				RefreshAccountsComboBox();
				return;
			}
			base.DialogTitle = PreferencesLocalization.Current("Add Remote");
			base.DialogDescription = PreferencesLocalization.Current("Add new remote repository reference");
			base.SubmitButtonTitle = PreferencesLocalization.Current("Add New Remote");
			RemoteNameTextBox.Icon = Theme.RemoteIcon;
			RemoteNameTextBox.Text = Consts.Git.DefaultRemoteName;
			string text = ServiceLocator.Clipboard.GetText();
			if (text != null)
			{
				GitUrl gitUrl = new GitUrl(text.Trim());
				if (gitUrl != null && gitUrl.IsValid)
				{
					if (_remotes.Length != 0)
					{
						RemoteNameTextBox.Text = gitUrl.Host;
					}
					RepositoryUrlTextBox.Text = gitUrl.UrlString;
				}
			}
			RemoteNameTextBox.SelectAll();
			RefreshAccountsComboBox();
		}

		private void RefreshAccountsComboBox()
		{
			AccountsTextBlock.Collapse();
			AccountsComboBox.Collapse();
			GitUrl gitUrl = new GitUrl(RepositoryUrlTextBox.Text.Trim());
			if (gitUrl.Protocol != GitUrl.NetworkProtocol.Https || gitUrl.RemoteType == RemoteType.Custom)
			{
				return;
			}
			List<Account> list = AccountManager.Current.Accounts.Filter((Account x) => x.ServiceType == gitUrl.RemoteType);
			if (list.Count < 2)
			{
				return;
			}
			List<AccountItem> list2 = new List<AccountItem>(list.Count + 4);
			AccountItem accountItem = AccountItem.CreateDefaultItem(list.FirstItem());
			list2.Add(accountItem);
			list2.Add(AccountItem.CreateSeparator());
			list2.AddRange(list.Map((Account x) => AccountItem.CreateAccountItem(x)));
			AccountItem selectedItem = accountItem;
			string userName = gitUrl.Username;
			if (userName != null)
			{
				AccountItem accountItem2 = list2.Where((AccountItem x) => x.ItemType == AccountItemType.Account && x.Account.Username.ToLower() == userName.ToLower()).FirstOrDefault();
				if (accountItem2 != null)
				{
					selectedItem = accountItem2;
				}
				else
				{
					AccountItem.CreateSeparator();
					AccountItem accountItem3 = AccountItem.CreateCustom(userName);
					list2.Add(accountItem3);
					selectedItem = accountItem3;
				}
			}
			AccountsTextBlock.Show();
			AccountsComboBox.Show();
			AccountsComboBox.ItemsSource = list2;
			AccountsComboBox.SelectedItem = selectedItem;
		}

		private void RefreshUrlUserName(AccountItem account)
		{
			GitUrl gitUrl = new GitUrl(RepositoryUrlTextBox.Text.Trim());
			string userName = null;
			switch (account.ItemType)
			{
			case AccountItemType.Account:
				userName = account.Account.Username;
				break;
			case AccountItemType.Custom:
				Log.Error("Cannot reach here.");
				break;
			}
			UriBuilder uriBuilder = new UriBuilder(gitUrl.UrlString);
			uriBuilder.UserName = userName;
			_refreshingUrl = true;
			RepositoryUrlTextBox.Text = uriBuilder.Uri.AbsoluteUri;
			_refreshingUrl = false;
		}

	}
}
