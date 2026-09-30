using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class RemoveLocalBranchWindow : ForkPlusDialogWindow
	{
		public class RemoveLocalBranchItem : INotifyPropertyChanged
		{
			private bool _upstreamVisibility;

			public string BranchName { get; }

			[Null]
			public string UpstreamName { get; }

			[Null]
			public string RemoteName { get; }

			[Null]
			public global::Avalonia.Media.IImage RemoteIcon { get; }

			// WS2.1 安全预览徽标：null 表示未计算/失败，徽标隐藏。
			[Null]
			public string SafetyText { get; }

			[Null]
			public global::Avalonia.Media.IBrush SafetyBrush { get; }

			[Null]
			public string UniqueCommitsHeaderText { get; }

			[Null]
			public string UniqueCommitsText { get; }

			public bool HasUniqueCommits => !string.IsNullOrEmpty(UniqueCommitsText);

			public bool UpstreamVisibility
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

			public RemoveLocalBranchItem(LocalBranch localBranch, [Null] RemoteBranch remoteBranch, Remote remote, bool showUpstream, [Null] string safetyText, [Null] global::Avalonia.Media.IBrush safetyBrush, [Null] string uniqueCommitsHeaderText, [Null] string uniqueCommitsText)
			{
				BranchName = localBranch.Name;
				UpstreamName = remoteBranch?.Name;
				RemoteName = remote?.Name;
				RemoteIcon = remote?.Icon;
				SafetyText = safetyText;
				SafetyBrush = safetyBrush;
				UniqueCommitsHeaderText = uniqueCommitsHeaderText;
				UniqueCommitsText = uniqueCommitsText;
				RefreshUpstreamVisibility(showUpstream);
			}

			public void RefreshUpstreamVisibility(bool showUpstream)
			{
				UpstreamVisibility = ((!showUpstream) ? false : true);
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
			// WS2.1：删除分支安全预览的公共前置——基准分支 + 已合并集合。
			// 构造期同步计算（本地 for-each-ref/rev-list 很快）；任何失败都静默降级，
			// 绝不让窗口构造抛异常（此时窗口尚未 Show，异常会直接打崩调用方）。
			GitModule safetyGitModule = null;
			string safetyBaseRef = "HEAD";
			HashSet<string> mergedIntoBase = null;
			try
			{
				safetyGitModule = _repositoryUserControl.GitModule;
				string leanBranchingMainBranch = safetyGitModule.Settings.LeanBranchingMainBranch;
				if (!string.IsNullOrWhiteSpace(leanBranchingMainBranch))
				{
					safetyBaseRef = leanBranchingMainBranch;
				}
				GitCommandResult<string[]> mergedBranchesResult = new GetMergedBranchesGitCommand().Execute(safetyGitModule, safetyBaseRef);
				if (mergedBranchesResult.Succeeded)
				{
					mergedIntoBase = new HashSet<string>(mergedBranchesResult.Result, StringComparer.Ordinal);
				}
			}
			catch
			{
				// 合并状态不可得时按"未知"降级：徽标留空
			}
			if (_branchesToRemove.Length == 1)
			{
				base.SizeToContent = global::Avalonia.Controls.SizeToContent.Height;
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
				// WS2.1：单分支模式在 GitPointView 下方加状态行（+独有提交 Expander）
				try
				{
					ApplySingleBranchSafetyPreview(safetyGitModule, localBranch, safetyBaseRef, mergedIntoBase);
				}
				catch
				{
					// 徽标留空即可
				}
			}
			else
			{
				base.Height = 270.0;
				base.MinHeight = 270.0;
				ResizeMode = ResizeMode.CanResizeWithGrip;
				base.DialogTitle = Translate("Delete Branches");
				base.DialogDescription = Translate("Delete local branches from your repository");
				StartPointTextBlock.Text = Translate("Branches:");
				DeleteRemoteBranchCheckBox.Content = Translate("Also delete corresponding remote branches");
				base.SubmitButtonTitle = PreferencesLocalization.FormatCurrent("Delete {0} branches", _branchesToRemove.Length);
				GitPointView.Collapse();
				DeleteRemoteBranchCheckBox.IsEnabled = AtLeastOneBranchHasUpstream(_branchesToRemove, _remoteBranches);
				BranchesContainer.Show();
				string uniqueCommitsHeaderText = Translate("Unique commits on this branch");
				List<RemoveLocalBranchItem> list = new List<RemoveLocalBranchItem>(4);
				LocalBranch[] branchesToRemove2 = _branchesToRemove;
				foreach (LocalBranch localBranch2 in branchesToRemove2)
				{
					RemoteBranch remoteBranch2 = FindUpstream(localBranch2, _remoteBranches);
					Remote remote = GetRemote(remoteBranch2, _remotes);
					// WS2.1：逐分支安全徽标（失败仅留空该行徽标）
					string safetyText = null;
					global::Avalonia.Media.IBrush safetyBrush = null;
					string uniqueCommitsText = null;
					try
					{
						(string text, global::Avalonia.Media.IBrush brush, string uniqueText) = ComputeBranchSafetyPreview(safetyGitModule, localBranch2, safetyBaseRef, mergedIntoBase);
						safetyText = text;
						safetyBrush = brush;
						uniqueCommitsText = uniqueText;
					}
					catch
					{
						// 该分支徽标留空
					}
					list.Add(new RemoveLocalBranchItem(localBranch2, remoteBranch2, remote, showUpstream: false, safetyText, safetyBrush, uniqueCommitsHeaderText, uniqueCommitsText));
				}
				_branchesSource = list.ToArray();
				BranchesItemsControl.ItemsSource = list;
			}
			// WS2.1：底部小字提示——删除可经 Ctrl+Z 撤销（配合 OnSubmit 的 AddUndoable）
			try
			{
				UndoHintTextBlock.Text = Translate("This deletion can be undone with Ctrl+Z");
				UndoHintTextBlock.Show();
			}
			catch
			{
				// 提示行非关键路径
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
			// WS2.4：待删分支含受保护分支 → 提交前拦截（DisableEditableControls 之前，
			// 取消时窗口控件保持可用，用户可关窗或改选）。
			if (gitModule != null && !ProtectedBranchConfirmWindow.Confirm(this, gitModule.Settings.ProtectedBranches, branchesToRemove.Map((LocalBranch x) => x.Name)))
			{
				return;
			}
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
					base.Dispatcher.Post(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting worktree..."));
					});
					GitCommandResult removeWorktreeResult = new RemoveWorktreeGitCommand().Execute(gitModule, valueOrDefault.Path, monitor);
					if (!removeWorktreeResult.Succeeded)
					{
						finalResult = removeWorktreeResult;
						base.Dispatcher.Post(delegate
						{
							Close(removeWorktreeResult);
						});
						return finalResult;
					}
					base.Dispatcher.Post(delegate
					{
						MainWindow.Instance.TabManager.CloseTab(worktreeToRemove.Value.Path);
					});
				}
				base.Dispatcher.Post(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, Translate("Deleting..."));
				});
				GitCommandResult removeLocalBranchResult = new RemoveLocalBranchGitCommand().Execute(gitModule, branchesToRemove.Map((LocalBranch x) => x.Name), monitor);
				if (!removeLocalBranchResult.Succeeded)
				{
					finalResult = removeLocalBranchResult;
					base.Dispatcher.Post(delegate
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
									base.Dispatcher.Post(delegate
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
								base.Dispatcher.Post(delegate
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
					base.Dispatcher.Post(delegate
					{
						Close(GitCommandResult.Success());
					});
				}
				return finalResult;
			}, JobFlags.SaveToLog);
		}

		// WS2.1：单分支模式安全状态行——GitPointView 下方状态 TextBlock + 独有提交 Expander
		private void ApplySingleBranchSafetyPreview(GitModule gitModule, LocalBranch branch, string baseRef, HashSet<string> mergedIntoBase)
		{
			(string text, global::Avalonia.Media.IBrush brush, string uniqueText) = ComputeBranchSafetyPreview(gitModule, branch, baseRef, mergedIntoBase);
			if (!string.IsNullOrEmpty(text))
			{
				BranchSafetyStatusTextBlock.Text = text;
				BranchSafetyStatusTextBlock.Foreground = brush;
				BranchSafetyStatusTextBlock.Show();
			}
			if (!string.IsNullOrEmpty(uniqueText))
			{
				BranchSafetyExpanderHeaderTextBlock.Text = Translate("Unique commits on this branch");
				BranchSafetyUniqueCommitsTextBlock.Text = uniqueText;
				BranchSafetyExpander.Show();
			}
		}

		// WS2.1：逐分支安全徽标。优先级（按任务规格）：
		//   已合并进 base → 绿"✓ 已合并"；未推送 N>0 → 橙"N 个未推送提交"；
		//   独有 M>0 → 红"M 个独有提交"；否则中性"未合并"。
		// 独有提交 M>0 时无论徽标取哪一级都附 Expander 明细（未推送的提交可能同时是独有提交，
		// 徽标按规格优先报未推送，明细仍需可见）。
		private (string Text, global::Avalonia.Media.IBrush Brush, string UniqueCommitsText) ComputeBranchSafetyPreview(GitModule gitModule, LocalBranch branch, string baseRef, HashSet<string> mergedIntoBase)
		{
			if (gitModule == null || branch == null || string.IsNullOrEmpty(branch.FullReference))
			{
				return (null, null, null);
			}
			if (mergedIntoBase != null && mergedIntoBase.Contains(branch.FullReference))
			{
				return ("✓ " + string.Format(Translate("Merged into {0}"), baseRef), global::ForkPlus.UI.Theme.ApplicationColors.GreenBrush, null);
			}
			GitCommandResult<int> unpushedResult = new GetBranchUnpushedCommitsGitCommand().Execute(gitModule, branch.FullReference, branch.UpstreamFullReference);
			int unpushed = (unpushedResult.Succeeded ? unpushedResult.Result : 0);
			GitCommandResult<int> uniqueResult = new GetBranchUniqueCommitsGitCommand().Execute(gitModule, branch.FullReference);
			int unique = (uniqueResult.Succeeded ? uniqueResult.Result : 0);
			string uniqueCommitsText = ((unique > 0) ? ReadUniqueCommitPreview(gitModule, branch) : null);
			string text;
			global::Avalonia.Media.IBrush brush;
			if (unpushed > 0)
			{
				text = string.Format(Translate("Unpushed commits: {0}"), unpushed);
				brush = global::ForkPlus.UI.Theme.ApplicationColors.YellowBrush;
			}
			else if (unique > 0)
			{
				text = string.Format(Translate("Unique commits: {0}"), unique);
				brush = global::ForkPlus.UI.Theme.ApplicationColors.RedBrush;
			}
			else
			{
				text = Translate("Not merged");
				brush = global::ForkPlus.UI.Theme.SecondaryLabelBrush;
			}
			return (text, brush, uniqueCommitsText);
		}

		// WS2.1：Expander 内独有提交明细——git log --format=%h\t%s -n 30 <branch> --not
		// --exclude=<短名> --branches --remotes（--exclude 对 --branches 匹配短名，见
		// GetBranchUniqueCommitsGitCommand 的注释）
		private string ReadUniqueCommitPreview(GitModule gitModule, LocalBranch branch)
		{
			string branchName = branch.FullReference;
			if (branchName.StartsWith("refs/heads/", StringComparison.Ordinal))
			{
				branchName = branchName.Substring("refs/heads/".Length);
			}
			GitCommand gitCommand = new GitCommand("log", "--format=%h\t%s", "-n", "30", branch.FullReference, "--not", "--exclude=" + branchName, "--branches", "--remotes");
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return null;
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.NewLine, StringSplitOptions.RemoveEmptyEntries);
			List<string> list = new List<string>(array.Length);
			string[] array2 = array;
			foreach (string text in array2)
			{
				string trimmed = text.Trim();
				if (trimmed.Length > 0)
				{
					list.Add(trimmed.Replace('\t', ' '));
				}
			}
			if (list.Count == 0)
			{
				return null;
			}
			return string.Join("\n", list);
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
