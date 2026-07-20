using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class RemoveLocalBranchWindow : ForkPlusDialogWindow
	{
		public class RemoveLocalBranchItem : INotifyPropertyChanged
		{
			private Visibility _upstreamVisibility;

			public string BranchName { get; }

			[Null]
			public string UpstreamName { get; }

			[Null]
			public string RemoteName { get; }

			[Null]
			public ImageSource RemoteIcon { get; }

			public Visibility UpstreamVisibility
			{
				get
				{
					return _upstreamVisibility;
				}
				set
				{
					if (_upstreamVisibility != value)
					{
						_upstreamVisibility = value;
						this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("UpstreamVisibility"));
					}
				}
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public RemoveLocalBranchItem(LocalBranch localBranch, [Null] RemoteBranch remoteBranch, Remote remote, bool showUpstream)
			{
				BranchName = localBranch.Name;
				UpstreamName = remoteBranch?.Name;
				RemoteName = remote?.Name;
				RemoteIcon = remote?.Icon;
				RefreshUpstreamVisibility(showUpstream);
			}

			public void RefreshUpstreamVisibility(bool showUpstream)
			{
				UpstreamVisibility = ((!showUpstream) ? Visibility.Collapsed : Visibility.Visible);
			}
		}

		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly LocalBranch[] _branchesToRemove;

		private readonly RemoteBranch[] _remoteBranches;

		private readonly RepositoryRemotes _remotes;

		private readonly RepositoryReferences _references;

		private RemoveLocalBranchItem[] _branchesSource = new RemoveLocalBranchItem[0];

		private readonly Worktree? _worktreeToRemove;

		protected override string GetCommandPreview()
		{
			if (_branchesToRemove == null || _branchesToRemove.Length == 0)
			{
				return null;
			}
			// 与 RemoveLocalBranchGitCommand 实际执行的 --delete --force 一致。
			var parts = new List<string> { "git", "branch", "-D" };
			foreach (LocalBranch b in _branchesToRemove)
			{
				parts.Add(b.Name);
			}
			string command = string.Join(" ", parts);
			if (DeleteRemoteBranchCheckBox.IsChecked.GetValueOrDefault())
			{
				foreach (LocalBranch b in _branchesToRemove)
				{
					RemoteBranch upstream = FindUpstream(b, _remoteBranches);
					Remote remote = GetRemote(upstream, _remotes);
					if (upstream != null && remote != null)
					{
						command += "\ngit push " + remote.Name + " --delete " + upstream.ShortName;
					}
				}
			}
			return command;
		}

		public RemoveLocalBranchWindow(RepositoryUserControl repositoryUserControl, RepositoryReferences references, LocalBranch[] branchesToRemove, RepositoryRemotes remotes, Worktree? worktreeToRemove = null)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_branchesToRemove = branchesToRemove;
			_references = references;
			_remotes = remotes;
			_remoteBranches = references.RemoteBranches;
			_worktreeToRemove = worktreeToRemove;
			if (_branchesToRemove.Length == 1)
			{
				base.SizeToContent = SizeToContent.Height;
				base.DialogTitle = Translate("Delete Branch");
				base.DialogDescription = Translate("Delete local branch from your repository");
				StartPointTextBlock.Text = Translate("Branch:");
				base.SubmitButtonTitle = Translate("Delete");
				LocalBranch localBranch = branchesToRemove.FirstItem();
				BranchesContainer.Collapse();
				GitPointView.Show();
				GitPointView.Value = localBranch;
				RemoteBranch remoteBranch = FindUpstream(localBranch, _remoteBranches);
				if (remoteBranch != null)
				{
					DeleteRemoteBranchCheckBox.Content = Translate("Also delete remote branch");
					DeleteRemoteBranchCheckBox.IsEnabled = true;
					DeleteRemoteBranchCheckBoxUpstream.Show();
					DeleteRemoteBranchCheckBoxUpstreamIcon.Show();
					DeleteRemoteBranchCheckBoxUpstream.Text = remoteBranch.Name ?? "";
					DeleteRemoteBranchCheckBoxUpstreamIcon.Source = GetRemote(remoteBranch, _remotes)?.Icon;
				}
				else
				{
					DeleteRemoteBranchCheckBox.Content = Translate("Also delete corresponding remote branch");
					DeleteRemoteBranchCheckBox.IsEnabled = false;
					DeleteRemoteBranchCheckBoxUpstream.Collapse();
					DeleteRemoteBranchCheckBoxUpstreamIcon.Collapse();
				}
				if (_worktreeToRemove.HasValue)
				{
					DeleteWorktreeContainer.Show();
					DeleteWorktreeLabel.Text = _worktreeToRemove.Value.FriendlyName;
				}
				else
				{
					DeleteWorktreeContainer.Collapse();
				}
			}
			else
			{
				base.Height = 270.0;
				base.MinHeight = 270.0;
				base.ResizeMode = ResizeMode.CanResizeWithGrip;
				base.DialogTitle = Translate("Delete Branches");
				base.DialogDescription = Translate("Delete local branches from your repository");
				StartPointTextBlock.Text = Translate("Branches:");
				DeleteRemoteBranchCheckBox.Content = Translate("Also delete corresponding remote branches");
				base.SubmitButtonTitle = PreferencesLocalization.FormatCurrent("Delete {0} branches", _branchesToRemove.Length);
				GitPointView.Collapse();
				DeleteRemoteBranchCheckBox.IsEnabled = AtLeastOneBranchHasUpstream(_branchesToRemove, _remoteBranches);
				BranchesContainer.Show();
				List<RemoveLocalBranchItem> list = new List<RemoveLocalBranchItem>(4);
				LocalBranch[] branchesToRemove2 = _branchesToRemove;
				foreach (LocalBranch localBranch2 in branchesToRemove2)
				{
					RemoteBranch remoteBranch2 = FindUpstream(localBranch2, _remoteBranches);
					Remote remote = GetRemote(remoteBranch2, _remotes);
					list.Add(new RemoveLocalBranchItem(localBranch2, remoteBranch2, remote, showUpstream: false));
				}
				_branchesSource = list.ToArray();
				BranchesItemsControl.ItemsSource = list;
			}
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 _branchesToRemove 尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			LocalBranch[] branchesToRemove = _branchesToRemove;
			RemoteBranch[] remoteBranches = _remoteBranches;
			RepositoryRemotes remotes = _remotes;
			bool removeUpstreams = DeleteRemoteBranchCheckBox.IsChecked.GetValueOrDefault();
			List<string> pinned = new List<string>(_references.PinnedReferences);
			List<string> filtered = new List<string>(_references.FilterReferences);
			DisableEditableControls();
			// v3.4.1：状态栏标题国际化（之前是硬编码英文）
		string name = ((branchesToRemove.Length > 1)
			? string.Format(Translate("Delete {0} branches"), branchesToRemove.Length)
			: string.Format(Translate("Delete '{0}'"), branchesToRemove[0].Name));
			bool removeWorktree = DeleteWorktreeCheckBox.IsChecked.GetValueOrDefault();
			Worktree? worktreeToRemove = _worktreeToRemove;
			// v3.4.0 Layer 2：删 branch 走 AddUndoable，操作前抓工作区快照（stash create），
			// Undo 时 stash apply --index 恢复，让用户能撤回误删的分支引用
			_repositoryUserControl.AddUndoable(name, delegate(JobMonitor monitor)
			{
				GitCommandResult finalResult = GitCommandResult.Success();
				if (removeWorktree && worktreeToRemove.HasValue)
				{
					Worktree valueOrDefault = worktreeToRemove.GetValueOrDefault();
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting worktree..."));
					});
					GitCommandResult removeWorktreeResult = new RemoveWorktreeGitCommand().Execute(gitModule, valueOrDefault.Path, monitor);
					if (!removeWorktreeResult.Succeeded)
					{
						finalResult = removeWorktreeResult;
						base.Dispatcher.Async(delegate
						{
							Close(removeWorktreeResult);
						});
						return finalResult;
					}
					base.Dispatcher.Async(delegate
					{
						MainWindow.Instance.TabManager.CloseTab(worktreeToRemove.Value.Path);
					});
				}
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting..."));
				});
				GitCommandResult removeLocalBranchResult = new RemoveLocalBranchGitCommand().Execute(gitModule, branchesToRemove.Map((LocalBranch x) => x.Name), monitor);
				if (!removeLocalBranchResult.Succeeded)
				{
					finalResult = removeLocalBranchResult;
					base.Dispatcher.Async(delegate
					{
						Close(removeLocalBranchResult);
					});
				}
				else
				{
					LocalBranch[] array = branchesToRemove;
					foreach (LocalBranch localBranch in array)
					{
						pinned.Remove(localBranch.FullReference);
						filtered.Remove(localBranch.FullReference);
					}
					if (removeUpstreams)
					{
						RemoteBranch[] array2 = branchesToRemove.CompactMap((LocalBranch x) => x.UpstreamFullReference).CompactMap((string x) => IReadOnlyListExtensions.FirstItem(remoteBranches, (RemoteBranch y) => y.FullReference == x));
						if (array2.Length != 0)
						{
							Dictionary<string, RemoteBranch[]> dictionary = (from x in array2
								group x by x.Remote).ToDictionary((IGrouping<string, RemoteBranch> x) => x.Key, (IGrouping<string, RemoteBranch> x) => x.ToArray());
							GitCommandResult removeRemoteBranchesResult = GitCommandResult.Success();
							foreach (KeyValuePair<string, RemoteBranch[]> group in dictionary)
							{
								Remote remote = IReadOnlyListExtensions.FirstItem(remotes.Items, (Remote x) => x.Name == group.Key);
								if (remote != null)
								{
									string title = ((group.Value.Length > 1) ? string.Format(Translate("Deleting {0} remote branches..."), group.Value.Length) : string.Format(Translate("Deleting '{0}'..."), group.Value[0].Name));
									base.Dispatcher.Async(delegate
									{
										SetStatus(ForkPlusDialogStatus.InProgress, title);
									});
									GitCommandResult gitCommandResult = new RemoveMultipleRemoteBranchesGitCommand().Execute(gitModule, group.Value, remote, monitor);
									if (!gitCommandResult.Succeeded)
									{
										removeRemoteBranchesResult = gitCommandResult;
									}
									else
									{
										RemoteBranch[] value = group.Value;
										foreach (RemoteBranch remoteBranch in value)
										{
											pinned.Remove(remoteBranch.FullReference);
											filtered.Remove(remoteBranch.FullReference);
										}
									}
								}
							}
							if (!removeRemoteBranchesResult.Succeeded)
							{
								finalResult = removeRemoteBranchesResult;
								base.Dispatcher.Async(delegate
								{
									Close(removeRemoteBranchesResult);
								});
								return finalResult;
							}
						}
					}
					gitModule.Settings.PinnedReferences = pinned.ToArray();
					gitModule.Settings.FilterReferences = filtered.ToArray();
					gitModule.Settings.Save();
					base.Dispatcher.Async(delegate
					{
						Close(GitCommandResult.Success());
					});
				}
				return finalResult;
			}, JobFlags.SaveToLog);
		}

		private void DeleteRemoteBranchCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (DeleteRemoteBranchCheckBox.IsChecked.GetValueOrDefault())
			{
				RefreshBranchesUpstreamVisibility(showUpstream: true);
				WarningImage.Show();
			}
			else
			{
				RefreshBranchesUpstreamVisibility(showUpstream: false);
				WarningImage.Collapse();
			}
			RefreshCommandPreview();
		}

		private void DeleteWorktreeCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (DeleteWorktreeCheckBox.IsChecked.GetValueOrDefault())
			{
				WorktreeWarningImage.Show();
			}
			else
			{
				WorktreeWarningImage.Collapse();
			}
			RefreshCommandPreview();
		}

		private void RefreshBranchesUpstreamVisibility(bool showUpstream)
		{
			RemoveLocalBranchItem[] branchesSource = _branchesSource;
			for (int i = 0; i < branchesSource.Length; i++)
			{
				branchesSource[i].RefreshUpstreamVisibility(showUpstream);
			}
		}

		private static bool AtLeastOneBranchHasUpstream(LocalBranch[] localBranches, RemoteBranch[] remoteBranches)
		{
			return localBranches.AnyItem((LocalBranch x) => FindUpstream(x, remoteBranches) != null);
		}

		[Null]
		private static RemoteBranch FindUpstream(LocalBranch localBranch, RemoteBranch[] remoteBranches)
		{
			string upstream = localBranch.UpstreamFullReference;
			if (upstream == null)
			{
				return null;
			}
			return IReadOnlyListExtensions.FirstItem(remoteBranches, (RemoteBranch x) => x.FullReference == upstream);
		}

		[Null]
		private static Remote GetRemote([Null] RemoteBranch remoteBranch, RepositoryRemotes remotes)
		{
			return IReadOnlyListExtensions.FirstItem(remotes.Items, (Remote x) => x.Name == remoteBranch?.Remote);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
