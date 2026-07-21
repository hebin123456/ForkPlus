using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class PushWindow : ForkPlusDialogWindow
	{
		public class RemoteItem : INotifyPropertyChanged
		{
			public RemoteItemType ItemType { get; private set; }

			public string Title { get; private set; }

			[Null]
			public Remote Remote { get; private set; }

			public string Name => Remote?.Name;

			public ImageSource RemoteIcon => Remote?.GetIconImage();

			public Visibility IconVisibility
			{
				get
				{
					if (Remote == null)
					{
						return Visibility.Collapsed;
					}
					return Visibility.Visible;
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public static RemoteItem CreateRemoteItem(Remote remote)
			{
				return new RemoteItem(remote.Name, RemoteItemType.Remote, remote);
			}

			public static RemoteItem CreateAddExistingRemoteItem()
			{
				return new RemoteItem(PushWindow.Translate("Add Remote..."), RemoteItemType.AddExistingRemote);
			}

			private RemoteItem(string title, RemoteItemType type, [Null] Remote remote = null)
			{
				Title = title;
				ItemType = type;
				Remote = remote;
			}
		}

		public enum RemoteItemType
		{
			Remote,
			AddExistingRemote
		}

		public class RemoteBranchItem : INotifyPropertyChanged
		{
			public RemoteBranchItemType ItemType { get; private set; }

			public RemoteBranch RemoteBranch { get; private set; }

			public Visibility IconVisibility { get; private set; }

			public string Title { get; private set; }

			public string ShortName { get; private set; }

			public event PropertyChangedEventHandler PropertyChanged;

			public static RemoteBranchItem CreateRemoteBranchItem(RemoteBranch remoteBranch)
			{
				return new RemoteBranchItem(remoteBranch.Name, RemoteBranchItemType.Branch, remoteBranch, showIcon: true);
			}

			public static RemoteBranchItem CreateCustomItem(string title, RemoteBranch remoteBranch = null, bool showIcon = false)
			{
				return new RemoteBranchItem(title, RemoteBranchItemType.Custom, remoteBranch, showIcon);
			}

			public static RemoteBranchItem CreateSeparator()
			{
				return new RemoteBranchItem("", RemoteBranchItemType.Separator);
			}

			public static RemoteBranchItem CreateAddCustomItem()
			{
				return new RemoteBranchItem(PushWindow.Translate("Custom..."), RemoteBranchItemType.AddCustom);
			}

			private RemoteBranchItem(string title, RemoteBranchItemType type, [Null] RemoteBranch remoteBranch = null, bool showIcon = false)
			{
				Title = title;
				ShortName = remoteBranch?.ShortName ?? Title;
				ItemType = type;
				RemoteBranch = remoteBranch;
				IconVisibility = ((!showIcon) ? Visibility.Collapsed : Visibility.Visible);
			}
		}

		public enum RemoteBranchItemType
		{
			Branch,
			Separator,
			Custom,
			AddCustom
		}

		private readonly RepositoryUserControl _repositoryUserControl;

		[Null]
		private readonly Remote _remoteToSelect;

		[Null]
		private readonly LocalBranch _localBranchToSelect;

		private LocalBranch[] _localBranches;

		private Remote[] _remotes;

		private RemoteBranch[] _allRemoteBranches;

		[Null]
		private string _customRefspec;

		private bool _stopRefresh;

		private RemoteItem[] RemoteItems { get; set; }

		[Null]
		private Remote SelectedRemote => (RemotesComboBox.SelectedItem as RemoteItem)?.Remote;

		protected override bool IsSubmitAllowed
	{
		get
		{
			if (!(LocalBranchesComboBox.SelectedItem is LocalBranch) || SelectedRemote == null)
			{
				return false;
			}
			return base.IsSubmitAllowed;
		}
	}

	protected override string GetCommandPreview()
	{
		Remote remote = SelectedRemote;
		if (remote == null || !(LocalBranchesComboBox.SelectedItem is LocalBranch localBranch))
		{
			return null;
		}
		RemoteBranch remoteBranch = (RemoteBranchesComboBox.SelectedItem as RemoteBranchItem)?.RemoteBranch;
		bool pushAllTags = AllTagsCheckBox.IsChecked.GetValueOrDefault();
		bool force = ForcePushCheckBox.IsChecked.GetValueOrDefault();
		bool track = false;
		if (localBranch.UpstreamFullReference == null)
		{
			track = CreateTrackingReferenceCheckBox.IsChecked.GetValueOrDefault(true);
		}
		System.Collections.Generic.List<string> parts = new System.Collections.Generic.List<string> { "git", "push" };
		if (force) { parts.Add("--force-with-lease"); }
		if (pushAllTags) { parts.Add("--tags"); }
		if (track) { parts.Add("--set-upstream"); }
		parts.Add(remote.Name);
		if (remoteBranch != null)
		{
			string dst = (remoteBranch.Remote == remote.Name) ? ("refs/heads/" + remoteBranch.ShortName) : ("refs/heads/" + localBranch.Name);
			parts.Add(localBranch.FullReference + ":" + dst);
		}
		else if (_customRefspec != null)
		{
			parts.Add(localBranch.FullReference + ":" + _customRefspec);
		}
		else
		{
			parts.Add(localBranch.FullReference);
		}
		return string.Join(" ", parts);
	}

		public PushWindow(RepositoryUserControl repositoryUserControl, [Null] Remote remote = null, [Null] LocalBranch localBranch = null)
		{
			_repositoryUserControl = repositoryUserControl;
			_remoteToSelect = remote;
			_localBranchToSelect = localBranch;
			_customRefspec = null;
			InitializeComponent();
			base.DialogTitle = Translate("Push");
			base.DialogDescription = Translate("Push your local changes to remote repository");
			base.SubmitButtonTitle = Translate("Push");
			AllTagsCheckBox.IsChecked = ForkPlusSettings.Default.Push_PushAllTags;
			ForcePushWarningImage.ToolTip = Translate("Overwrite the remote branch even if it's not an ancestor of the local branch.\n- Force push is required for rebase of already published branch.\n- Blindly using force push can be dangerous as you can overwrite other users' commits.\n- Fork always uses --force-with-lease which protects from race conditions.");
			Refresh();
			CheckSubmodules();
			UpdateSubmitButton();
		}

		protected override void OnSubmit()
		{
			Remote remote = SelectedRemote;
			if (remote == null)
			{
				return;
			}
			object selectedItem = LocalBranchesComboBox.SelectedItem;
			LocalBranch localBranch = selectedItem as LocalBranch;
			if (localBranch == null)
			{
				return;
			}
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			GitModule gitModule = _repositoryUserControl.GitModule;
			RemoteBranch remoteBranch = (RemoteBranchesComboBox.SelectedItem as RemoteBranchItem)?.RemoteBranch;
			bool pushAllTags = AllTagsCheckBox.IsChecked.GetValueOrDefault();
			bool force = ForcePushCheckBox.IsChecked.GetValueOrDefault();
			bool track = false;
			string customRefspec = _customRefspec;
			ForkPlusSettings.Default.Push_PushAllTags = pushAllTags;
			if (_remotes.Length > 1)
			{
				gitModule.Settings.RecentRemote = remote.Name;
			}
			if (localBranch.UpstreamFullReference == null)
			{
				track = CreateTrackingReferenceCheckBox.IsChecked.GetValueOrDefault(true);
			}
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Push '{0}' to '{1}'"), localBranch.Name, remote.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult pushResult = new PushGitCommand().Execute(gitModule, remote.Name, localBranch, remoteBranch, customRefspec, pushAllTags, force, track, monitor);
				base.Dispatcher.Async(delegate
				{
					if (!pushResult.Succeeded && !monitor.IsCanceled)
					{
						new ErrorWindow(repositoryUserControl, pushResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);
				});
			});
			Close();
		}

		private void ForcePushCheckBox_Changed(object sender, RoutedEventArgs e)
	{
		if (ForcePushCheckBox.IsChecked.GetValueOrDefault())
		{
			ForcePushWarningImage.Show();
		}
		else
		{
			ForcePushWarningImage.Hide();
		}
		RefreshCommandPreview();
	}

	private void CheckBox_Changed(object sender, RoutedEventArgs e)
	{
		RefreshCommandPreview();
	}

	private void LocalBranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!(LocalBranchesComboBox.SelectedItem is LocalBranch localBranch))
		{
			return;
		}
		_customRefspec = null;
		if (localBranch.UpstreamFullReference != null)
		{
			CreateTrackingReferenceCheckBox.IsChecked = false;
			CreateTrackingReferenceCheckBox.Collapse();
		}
		else
		{
			CreateTrackingReferenceCheckBox.IsChecked = true;
			CreateTrackingReferenceCheckBox.Show();
		}
		if (!_stopRefresh)
		{
			RemoteBranch upstream = FindUpstream(_allRemoteBranches, localBranch);
			string recentRemote = _repositoryUserControl.GitModule.Settings.RecentRemote;
			Remote remote = IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == upstream?.Remote) ?? IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == recentRemote) ?? IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? _remotes.FirstItem();
			if (remote != null)
			{
				_stopRefresh = true;
				SelectRemote(remote);
				_stopRefresh = false;
			}
			RefreshRemoteBranches();
			UpdateSubmitButton();
		}
		RefreshCommandPreview();
	}

	private void RemotesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (_stopRefresh)
		{
			return;
		}
		_customRefspec = null;
		RemoteItem selectedItem = ((e.RemovedItems.Count > 0) ? (e.RemovedItems[0] as RemoteItem) : null);
		if (!(RemotesComboBox.SelectedItem is RemoteItem remoteItem))
		{
			return;
		}
		GitModule gitModule = _repositoryUserControl.GitModule;
		if (gitModule == null)
		{
			return;
		}
		if (remoteItem.ItemType == RemoteItemType.AddExistingRemote)
		{
			EditRemoteWindow editRemoteWindow = new EditRemoteWindow(_repositoryUserControl, gitModule);
			editRemoteWindow.Owner = this;
			if (editRemoteWindow.ShowDialog().GetValueOrDefault())
			{
				if (!editRemoteWindow.GitResult.Succeeded)
				{
					new ErrorWindow(_repositoryUserControl, editRemoteWindow.GitResult.Error).ShowDialog();
				}
				_repositoryUserControl.Invalidate(SubDomain.Remotes | SubDomain.References);
				GitCommandResult<GitConfig> gitCommandResult = new GetGitConfigGitCommand().Execute(gitModule);
				if (!gitCommandResult.Succeeded)
				{
					Log.Error(gitCommandResult.Error.FriendlyDescription);
				}
				else
				{
					GitConfig result = gitCommandResult.Result;
					GitCommandResult<RepositoryRemotes> gitCommandResult2 = new GetRemotesGitCommand().Execute(result);
					if (!gitCommandResult2.Succeeded)
					{
						Log.Error(gitCommandResult2.Error.FriendlyDescription);
					}
					else
					{
						_remotes = gitCommandResult2.Result.Items;
						RefreshRemotes();
						SelectRemote(_remotes.FirstItem());
					}
				}
			}
			else
			{
				RemotesComboBox.SelectedItem = selectedItem;
			}
		}
		RefreshRemoteBranches();
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

	private void RemoteBranchesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		RemoteBranchItem selectedItem = ((e.RemovedItems.Count > 0) ? (e.RemovedItems[0] as RemoteBranchItem) : null);
		if (!(RemoteBranchesComboBox.SelectedItem is RemoteBranchItem remoteBranchItem))
		{
			return;
		}
		Remote selectedRemote = SelectedRemote;
		if (selectedRemote == null)
		{
			return;
		}
		GitModule gitModule = _repositoryUserControl.GitModule;
		if (remoteBranchItem.ItemType == RemoteBranchItemType.AddCustom && LocalBranchesComboBox.SelectedItem is LocalBranch localBranch)
		{
			string localBranchName = gitModule.Settings.PushLastCustomRefspec ?? localBranch.Name;
			AddCustomRefspecWindow addCustomRefspecWindow = new AddCustomRefspecWindow(selectedRemote.Name, localBranchName);
			addCustomRefspecWindow.Owner = this;
			if (addCustomRefspecWindow.ShowDialog().GetValueOrDefault())
			{
				_customRefspec = addCustomRefspecWindow.OutRefspec;
				gitModule.Settings.PushLastCustomRefspec = addCustomRefspecWindow.OutRefspec;
				gitModule.Settings.Save();
				RefreshRemoteBranches();
			}
			else
			{
				RemoteBranchesComboBox.SelectedItem = selectedItem;
			}
		}
		UpdateSubmitButton();
		RefreshCommandPreview();
	}

		private void RefreshRemoteBranches()
		{
			if (SelectedRemote == null)
			{
				return;
			}
			string selectedRemoteName = SelectedRemote.Name;
			List<RemoteBranch> list = _allRemoteBranches.Filter((RemoteBranch x) => x.Remote == selectedRemoteName);
			RemoteBranchItem[] array = list.Map((RemoteBranch x) => RemoteBranchItem.CreateRemoteBranchItem(x));
			object selectedItem = LocalBranchesComboBox.SelectedItem;
			LocalBranch selectedLocalBranch = selectedItem as LocalBranch;
			if (selectedLocalBranch == null)
			{
				RemoteBranchesComboBox.ItemsSource = array;
				return;
			}
			string text = null;
			RemoteBranch remoteBranch = null;
			RemoteBranch remoteBranch2 = FindUpstream(list, selectedLocalBranch);
			if (remoteBranch2 != null)
			{
				text = string.Format(Translate("default ({0})"), remoteBranch2.Name);
			}
			else
			{
				string trackingReferenceName = GetLocalBranchTrackingReferenceName(selectedLocalBranch);
				if (trackingReferenceName != null)
				{
					remoteBranch = IReadOnlyListExtensions.FirstItem(list, (RemoteBranch x) => x.ShortName == trackingReferenceName);
					if (remoteBranch == null)
					{
						text = string.Format(Translate("new ({0})"), selectedRemoteName + "/" + trackingReferenceName);
					}
				}
				else if (remoteBranch == null)
				{
					remoteBranch = IReadOnlyListExtensions.FirstItem(list, (RemoteBranch x) => x.ShortName == selectedLocalBranch.Name);
					if (remoteBranch == null)
					{
						text = string.Format(Translate("new ({0})"), selectedRemoteName + "/" + selectedLocalBranch.Name);
					}
				}
			}
			List<RemoteBranchItem> list2 = new List<RemoteBranchItem>(list.Count + 5);
			RemoteBranchItem remoteBranchItem = null;
			if (text != null)
			{
				remoteBranchItem = RemoteBranchItem.CreateCustomItem(text, null, showIcon: true);
				list2.Add(remoteBranchItem);
				list2.Add(RemoteBranchItem.CreateSeparator());
			}
			RemoteBranchItem remoteBranchItem2 = null;
			RemoteBranchItem[] array2 = array;
			foreach (RemoteBranchItem remoteBranchItem3 in array2)
			{
				list2.Add(remoteBranchItem3);
				if (remoteBranch == remoteBranchItem3.RemoteBranch)
				{
					remoteBranchItem2 = remoteBranchItem3;
				}
			}
			if (list.Count > 0)
			{
				list2.Add(RemoteBranchItem.CreateSeparator());
			}
			RemoteBranchItem remoteBranchItem4 = null;
			if (_customRefspec != null)
			{
				if (_customRefspec.StartsWith("refs/"))
				{
					remoteBranchItem4 = RemoteBranchItem.CreateCustomItem(_customRefspec);
					list2.Add(remoteBranchItem4);
				}
				else
				{
					string name = SelectedRemote.Name;
					string customRefspec = _customRefspec;
					string fullReference = "refs/remotes/" + customRefspec;
					string text2 = name + "/" + customRefspec;
					RemoteBranch remoteBranch3 = new RemoteBranch(selectedLocalBranch.Sha, fullReference, text2, customRefspec, name, selectedLocalBranch.CommitterDate);
					remoteBranchItem4 = RemoteBranchItem.CreateCustomItem(text2, remoteBranch3, showIcon: true);
					list2.Add(remoteBranchItem4);
				}
			}
			list2.Add(RemoteBranchItem.CreateAddCustomItem());
			RemoteBranchesComboBox.ItemsSource = list2.ToArray();
			RemoteBranchesComboBox.SelectedItem = remoteBranchItem4 ?? remoteBranchItem ?? remoteBranchItem2;
		}

		[Null]
		private string GetLocalBranchTrackingReferenceName(LocalBranch branch)
		{
			string upstreamFullReference = branch.UpstreamFullReference;
			if (upstreamFullReference == null)
			{
				return null;
			}
			string text = upstreamFullReference.Substring("refs/remotes/".Length);
			int num = text.IndexOf('/');
			if (num != -1 && num + 1 < text.Length)
			{
				return text.Substring(num + 1);
			}
			return null;
		}

		private void CheckSubmodules()
		{
			if (SelectedRemote == null)
			{
				return;
			}
			object selectedItem = LocalBranchesComboBox.SelectedItem;
			LocalBranch localBranch = selectedItem as LocalBranch;
			if (localBranch == null)
			{
				return;
			}
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null || repositoryData.Submodules.Items.Length == 0)
			{
				return;
			}
			GitModule gitModule = _repositoryUserControl.GitModule;
			string[] submodulePaths = repositoryData.Submodules.Items.Map((Submodule x) => x.Path);
			_repositoryUserControl.JobQueue.Add(Translate("Unpushed submodules check"), delegate(JobMonitor monitor)
			{
				GitCommandResult<string[]> unpushedSubmodulesResponse = new GetUnpushedSubmodulesGitCommand().Execute(gitModule, localBranch.Sha, submodulePaths, monitor);
				base.Dispatcher.Async(delegate
				{
					if (!unpushedSubmodulesResponse.Succeeded)
					{
						Log.Error(unpushedSubmodulesResponse.Error.FriendlyDescription);
					}
					else if (unpushedSubmodulesResponse.Result.Length != 0)
					{
						SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("Submodule '{0}' contains unpushed changes"), unpushedSubmodulesResponse.Result.FirstItem()));
					}
				});
			}, JobFlags.Hidden);
		}

		private void Refresh()
		{
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			_stopRefresh = true;
			_localBranches = repositoryData.References.LocalBranches;
			_remotes = repositoryData.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
			_allRemoteBranches = repositoryData.References.RemoteBranches;
			LocalBranchesComboBox.ItemsSource = _localBranches;
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			LocalBranchesComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(_localBranches, (LocalBranch x) => x.FullReference == _localBranchToSelect?.FullReference) ?? activeBranch ?? _localBranches.FirstItem();
			RefreshRemotes();
			Remote remote = null;
			string upstream = activeBranch?.UpstreamFullReference;
			if (upstream != null)
			{
				RemoteBranch activeUpstream = IReadOnlyListExtensions.FirstItem(_allRemoteBranches, (RemoteBranch x) => x.FullReference == upstream);
				if (activeUpstream != null)
				{
					remote = IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == activeUpstream.Remote);
				}
			}
			string recentRemote = gitModule.Settings.RecentRemote;
			Remote remote2 = IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == _remoteToSelect?.Name) ?? remote ?? IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == recentRemote) ?? IReadOnlyListExtensions.FirstItem(_remotes, (Remote x) => x.Name == Consts.Git.DefaultRemoteName) ?? _remotes.FirstItem();
			_stopRefresh = false;
			SelectRemote(remote2);
		}

		private void RefreshRemotes()
		{
			if (_remotes.Length == 1)
			{
				RemotesLabel.Collapse();
				RemotesComboBox.Collapse();
			}
			List<RemoteItem> list = new List<RemoteItem>(_remotes.Length + 1);
			list.AddRange(_remotes.Map((Remote x) => RemoteItem.CreateRemoteItem(x)));
			if (_remotes.Length == 0)
			{
				list.Add(RemoteItem.CreateAddExistingRemoteItem());
			}
			RemoteItems = list.ToArray();
			RemotesComboBox.ItemsSource = RemoteItems;
		}

		private void SelectRemote(Remote remote)
		{
			RemotesComboBox.SelectedItem = System.Linq.Enumerable.FirstOrDefault(RemoteItems ?? Array.Empty<RemoteItem>(), (RemoteItem x) => x.ItemType == RemoteItemType.Remote && x.Remote == remote);
		}

		private static RemoteBranch FindUpstream(IReadOnlyList<RemoteBranch> remoteBranches, LocalBranch localBranch)
		{
			string upstreamFullReference = localBranch?.UpstreamFullReference;
			if (upstreamFullReference == null)
			{
				return null;
			}
			return remoteBranches.FirstItem((RemoteBranch x) => x.FullReference == upstreamFullReference);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
