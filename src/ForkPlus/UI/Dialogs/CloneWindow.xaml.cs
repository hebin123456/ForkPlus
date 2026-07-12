using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;

namespace ForkPlus.UI.Dialogs
{
	public partial class CloneWindow : ForkPlusDialogWindow
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

		[Null]
		private Account _account;

		private bool _refreshingUrl;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (string.IsNullOrWhiteSpace(RepositoryUrlTextBox.Text.Trim()))
				{
					return false;
				}
				if (string.IsNullOrWhiteSpace(RepositoryNameTextBox.Text.Trim()))
				{
					return false;
				}
				if (string.IsNullOrWhiteSpace(ParentDirectoryTextBox.Text.Trim()))
				{
					return false;
				}
				return true;
			}
		}

		private AccountItem[] AccountItems { get; set; }

		public CloneWindow([Null] string url, [Null] Account account)
		{
			_account = account;
			InitializeComponent();
			base.DialogTitle = Translate("Clone");
			base.DialogDescription = Translate("Clone a remote repository into a local folder");
			base.SubmitButtonTitle = Translate("Clone");
			Refresh(url);
			RefreshNetworkProtocolButton();
			UpdateSubmitButton();
			RefreshAccountsComboBox();
			HideStatusControls();
			Account account2 = _account;
			if (account2 != null)
			{
				SelectAccount(account2);
			}
			base.Loaded += delegate
			{
				if (!string.IsNullOrEmpty(RepositoryUrlTextBox.Text.Trim()))
				{
					RepositoryNameTextBox.Focus();
					RepositoryNameTextBox.SelectAll();
				}
			};
		}

		protected override void OnActivated(EventArgs e)
		{
			base.OnActivated(e);
			if (_account == null)
			{
				TryParseUrlFromClipboard();
				HideStatusControls();
				RefreshAccountsComboBox();
			}
		}

		protected override async void OnSubmit()
		{
			string url = RepositoryUrlTextBox.Text.Trim();
			string destinationDirectory = System.IO.Path.Combine(ParentDirectoryTextBox.Text.Trim(), RepositoryNameTextBox.Text.Trim());
			bool recurseSubmodules = ForkPlusSettings.Default.UpdateSubmodulesOnCheckout;
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl != null)
			{
				string name = Translate("Cloning...");
				string repositoryName = new GitUrl(url).RepositoryName;
				if (repositoryName != null)
				{
					name = string.Format(Translate("Cloning {0}..."), repositoryName);
				}
				activeRepositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
				{
					RunClone(url, recurseSubmodules, destinationDirectory, monitor, foreground: false);
				});
				Close();
			}
			else
			{
				DisableEditableControls();
				SetStatus(ForkPlusDialogStatus.InProgress, Translate("Cloning..."));
				await Task.Run(delegate
				{
					RunClone(url, recurseSubmodules, destinationDirectory, new JobMonitor(), foreground: true);
				});
				Close();
			}
		}

		private async void TestButton_Click(object sender, RoutedEventArgs e)
		{
			string newUrl = RepositoryUrlTextBox.Text.Trim();
			DisableEditableControls();
			StatusImage.Hide();
			BusyIndicator.Show();
			StatusTextBlock.Show();
			StatusTextBlock.Text = Translate("Connecting...");
			TestButton.Collapse();
			await Task.Run(delegate
			{
				GitCommandResult result = new TestRemoteRepositoryConnectionGitCommand().Execute(newUrl, new JobMonitor());
				base.Dispatcher.Invoke(delegate
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
								StatusTextBlock.Text = Translate("Permission denied (publickey)");
								ConfigureSSHKeyButton.Show();
								TestButton.Collapse();
							}
							else if (callbackUnknownError.FullOutput.Contains("not found"))
							{
								StatusTextBlock.Show();
								StatusTextBlock.Text = Translate("Repository not found");
								TestButton.Collapse();
							}
							else
							{
								new ErrorWindow(null, result.Error).ShowDialog();
							}
						}
					}
					else
					{
						StatusImage.Source = new BitmapImage(ForkPlusDialogWindow.SuccessIcon);
						StatusTextBlock.Show();
						StatusTextBlock.Text = Translate("Connection succeeded");
					}
				});
			});
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

		private void ConfigureSSHKeyButton_Click(object sender, RoutedEventArgs e)
		{
			MainWindow.Commands.ShowConfigureSSHKeysWindow.Execute();
			HideStatusControls();
		}

		private void HideStatusControls()
		{
			StatusImage.Hide();
			TestButton.Show();
			TestButton.IsEnabled = IsSubmitAllowed;
			StatusTextBlock.Collapse();
			ConfigureSSHKeyButton.Collapse();
		}

		private void RunClone(string url, bool recurseSubmodules, string destinationDirectory, JobMonitor monitor, bool foreground)
		{
			if (foreground)
			{
				monitor.SetProgressAction(delegate
				{
					base.Dispatcher.Invoke(delegate
					{
						CloneWindow cloneWindow = this;
						object obj = monitor.ProgressMessage ?? "";
						if (obj == null)
						{
							obj = "";
						}
						cloneWindow.SetStatus(ForkPlusDialogStatus.InProgress, (string)obj);
					});
				});
			}
			GitCommandResult result = new CloneGitCommand().Execute(url, recurseSubmodules, destinationDirectory, monitor);
			monitor.SetProgressAction(null);
			base.Dispatcher.Invoke(delegate
			{
				if (foreground)
				{
					SetStatus(ForkPlusDialogStatus.None, string.Empty);
					EnableEditableControls();
				}
				if (!result.Succeeded && !monitor.IsCanceled)
				{
					new ErrorWindow(null, result.Error).ShowDialog();
				}
				else
				{
					Application.Current.TabManager().OpenRepository(destinationDirectory);
				}
			});
		}

		private void Refresh([Null] string url)
		{
			if (url != null)
			{
				RepositoryUrlTextBox.Text = url;
			}
			else
			{
				TryParseUrlFromClipboard();
			}
			ParentDirectoryTextBox.Text = RepositoryManager.Instance.DefaultSourceDir();
		}

		private void RepositoryUrlTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshRepositoryNameTextBox();
			HideStatusControls();
			RefreshNetworkProtocolButton();
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

		private void ParentDirectoryTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void RepositoryNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
			if (OpenDialog.SelectDirectory(this, Translate("Select location"), initialDirectory, out var directoryPath))
			{
				ParentDirectoryTextBox.Text = directoryPath;
				ParentDirectoryTextBox.Focus();
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

		private void TryParseUrlFromClipboard()
		{
			if (!string.IsNullOrEmpty(RepositoryUrlTextBox.Text.Trim()))
			{
				return;
			}
			string text = ServiceLocator.Clipboard.GetText();
			if (text != null)
			{
				text = RemoveGitClonePrefix(text).Trim().Trim('"');
				if (new GitUrl(text).IsValid)
				{
					RepositoryUrlTextBox.Text = text;
				}
			}
		}

		private string RemoveGitClonePrefix(string clipboardUrl)
		{
			string text = "git clone ";
			if (!clipboardUrl.StartsWith(text))
			{
				return clipboardUrl;
			}
			return clipboardUrl.Remove(0, text.Length);
		}

		private void RefreshRepositoryNameTextBox()
		{
			string repositoryName = new GitUrl(RepositoryUrlTextBox.Text.Trim()).RepositoryName;
			if (repositoryName != null)
			{
				RepositoryNameTextBox.Text = repositoryName;
			}
		}

		private void RefreshAccountsComboBox()
		{
			AccountItems = new AccountItem[0];
			AccountsTextBlock.Collapse();
			AccountsComboBox.Collapse();
			AccountsSeparator.Collapse();
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
			AccountsSeparator.Show();
			AccountItems = list2.ToArray();
			AccountsComboBox.ItemsSource = AccountItems;
			AccountsComboBox.SelectedItem = selectedItem;
		}

		private void SelectAccount(Account account)
		{
			AccountsComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(AccountItems, (AccountItem x) => x.ItemType == AccountItemType.Account && x.Account == account);
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

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
