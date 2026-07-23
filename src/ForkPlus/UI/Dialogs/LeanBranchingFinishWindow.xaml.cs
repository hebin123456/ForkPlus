using Avalonia.Threading;
using System;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class LeanBranchingFinishWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		// 阶段 3：承接 LeanBranchingFinish 的多重 behind/ahead 校验 + 命令预览。
		// VM 持 (GitModule, RepositoryData, CommitGraphCache)，内部调用 GetBehindAheadCountGitCommand
		// 做状态校验；SetStatus 副作用留 View，翻译键通过 RequiresTranslation 标志委托给 View。
		private readonly LeanBranchingFinishWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				(bool isAllowed, ForkPlusDialogStatus status, string statusMessage, bool requiresTranslation) = _viewModel.Validate();
				if (status != ForkPlusDialogStatus.None)
				{
					string message = requiresTranslation ? Translate(statusMessage ?? string.Empty) : (statusMessage ?? string.Empty);
					SetStatus(status, message);
				}
				return isAllowed;
			}
		}

		public LeanBranchingFinishWindow(RepositoryUserControl repositoryUserControl)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				RepositoryData repositoryData = repositoryUserControl.RepositoryData;
				if (repositoryData != null)
				{
					_repositoryUserControl = repositoryUserControl;
					_viewModel = new LeanBranchingFinishWindowViewModel(gitModule, repositoryData, repositoryUserControl.CommitGraphCache);
					InitializeComponent();
					LocalBranch activeBranch = repositoryData.References.ActiveBranch;
					LocalBranch localBranch = repositoryData.References.LocalMain(gitModule);
					base.DialogTitle = Translate("Finish Branch");
					base.DialogDescription = string.Format(Translate("Finish '{0}' and merge it into '{1}'"), activeBranch.Name, localBranch.Name);
					base.SubmitButtonTitle = Translate("Finish");
					CurrentBranchGitPointView.Value = activeBranch;
					MainBranchGitPointView.Value = localBranch;
					UpdateSubmitButton();
					// InitializeComponent 期间 AddCommandPreview 已执行，但此时仓库状态尚未读取，
					// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
					RefreshCommandPreview();
				}
			}
		}

		protected override string GetCommandPreview()
		{
			// LeanBranchingFinishWindow：把当前分支收尾合并回 main。
			// 命令序列：可选 git fetch（main 落后 remote 时）→ git checkout main → git merge <feature>
			return _viewModel.CommandPreview;
		}

		protected override void OnSubmit()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			CommitGraphCache commitGraphCache = _repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			LocalBranch localMain = repositoryData.References.LocalMain(gitModule);
			if (localMain == null)
			{
				return;
			}
			RemoteBranch remoteMain = repositoryData.References.Upstream(localMain);
			if (remoteMain == null)
			{
				return;
			}
			LocalBranch activeBranch = repositoryData.References.ActiveBranch;
			if (activeBranch == null)
			{
				return;
			}
			SubmodulesToUpdate submodulesToUpdate = _repositoryUserControl.SubmodulesToUpdate();
			GitCommandResult<BehindAheadCount> mainBehindAheadCountResponse = new GetBehindAheadCountGitCommand().Execute(gitModule, localMain.Sha, remoteMain.Sha, commitGraphCache);
			if (!mainBehindAheadCountResponse.Succeeded)
			{
				new ErrorWindow(_repositoryUserControl, mainBehindAheadCountResponse.Error).ShowDialog();
				return;
			}
			GitCommandResult<BehindAheadCount> behindAheadCountResponse = new GetBehindAheadCountGitCommand().Execute(gitModule, activeBranch.Sha, localMain.Sha, commitGraphCache);
			if (!behindAheadCountResponse.Succeeded)
			{
				new ErrorWindow(_repositoryUserControl, behindAheadCountResponse.Error).ShowDialog();
				return;
			}
			DisableEditableControls();
			SetStatus(ForkPlusDialogStatus.InProgress, string.Format(Translate("Finishing {0}..."), activeBranch.Name));
			_repositoryUserControl.JobQueue.Add(string.Format(Translate("Finish '{0}'"), activeBranch.Name), delegate(JobMonitor monitor)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, string.Format(Translate("Fast-forward '{0}' to '{1}'"), localMain.Name, remoteMain.Name));
				});
				if (mainBehindAheadCountResponse.Result.Right > 0)
				{
					GitCommandResult mainFastForwardResult = new FastForwardGitCommand().Execute(gitModule, localMain, monitor);
					if (!mainFastForwardResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(mainFastForwardResult);
						});
						return;
					}
				}
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Checkout...");
				});
				GitCommandResult checkoutResult = new CheckoutBranchGitCommand().Execute(gitModule, localMain, monitor);
				if (!checkoutResult.Succeeded && !(checkoutResult.Error is GitCommandError.Cancelled))
				{
					base.Dispatcher.Async(delegate
					{
						Close(checkoutResult);
					});
				}
				else
				{
					MergeType mergeType;
					if (!gitModule.Settings.LeanBranchingNoFastForward && behindAheadCountResponse.Result.Left == 1)
					{
						mergeType = MergeType.FastForward;
						monitor.AppendOutputLine("'" + activeBranch.Name + "' consists of a single commit. Using fast-forward");
					}
					else
					{
						mergeType = MergeType.NoFastForward;
					}
					base.Dispatcher.Async(delegate
					{
						SetStatus(ForkPlusDialogStatus.InProgress, string.Format(Translate("Merging into '{0}'..."), localMain.Name));
					});
					GitCommandResult mergeResult = new MergeGitCommand().Execute(gitModule, activeBranch, mergeType, repositoryData.References, monitor);
					if (!mergeResult.Succeeded)
					{
						base.Dispatcher.Async(delegate
						{
							Close(mergeResult);
						});
					}
					else
					{
						if (submodulesToUpdate.Length > 0)
						{
							base.Dispatcher.Async(delegate
							{
								SetStatus(ForkPlusDialogStatus.InProgress, "Updating submodules...");
							});
							GitCommandResult updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, monitor);
							if (!updateSubmodulesResult.Succeeded)
							{
								base.Dispatcher.Async(delegate
								{
									Close(updateSubmodulesResult);
								});
								return;
							}
						}
						base.Dispatcher.Async(delegate
						{
							Close(mergeResult);
						});
					}
				}
			}, JobFlags.SaveToLog);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
