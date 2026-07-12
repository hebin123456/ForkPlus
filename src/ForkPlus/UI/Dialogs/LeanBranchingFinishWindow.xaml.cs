using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Markup;
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

		protected override bool IsSubmitAllowed
		{
			get
			{
				GitModule gitModule = _repositoryUserControl.GitModule;
				if (gitModule != null)
				{
					RepositoryData repositoryData = _repositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						CommitGraphCache commitGraphCache = _repositoryUserControl.CommitGraphCache;
						if (commitGraphCache != null)
						{
							LocalBranch localBranch = repositoryData.References.LocalMain(gitModule);
							if (localBranch != null)
							{
								RemoteBranch remoteBranch = repositoryData.References.Upstream(localBranch);
								if (remoteBranch != null)
								{
									LocalBranch activeBranch = repositoryData.References.ActiveBranch;
									if (activeBranch != null)
									{
										RemoteBranch remoteBranch2 = repositoryData.References.Upstream(activeBranch);
										if (remoteBranch2 != null)
										{
											GitCommandResult<BehindAheadCount> gitCommandResult = new GetBehindAheadCountGitCommand().Execute(gitModule, activeBranch.Sha, remoteBranch2.Sha, commitGraphCache);
											if (!gitCommandResult.Succeeded)
											{
												return false;
											}
											if (gitCommandResult.Result.Right > 0)
											{
												SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("You must sync '{0}' first"), activeBranch.Name));
												return false;
											}
										}
										GitCommandResult<BehindAheadCount> gitCommandResult2 = new GetBehindAheadCountGitCommand().Execute(gitModule, localBranch.Sha, remoteBranch.Sha, commitGraphCache);
										if (!gitCommandResult2.Succeeded)
										{
											return false;
										}
										if (!gitCommandResult2.Result.AreInSync())
										{
											SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("You must checkout and sync '{0}' first"), localBranch.Name));
											return false;
										}
										GitCommandResult<BehindAheadCount> gitCommandResult3 = new GetBehindAheadCountGitCommand().Execute(gitModule, activeBranch.Sha, localBranch.Sha, commitGraphCache);
										if (!gitCommandResult3.Succeeded)
										{
											return false;
										}
										if (!gitCommandResult3.Result.AreInSync())
										{
											SetStatus(ForkPlusDialogStatus.Warning, string.Format(Translate("You must sync '{0}' with '{1}' first"), activeBranch.Name, localBranch.Name));
											return false;
										}
										return true;
									}
								}
							}
							return false;
						}
					}
				}
				return false;
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
					InitializeComponent();
					LocalBranch activeBranch = repositoryData.References.ActiveBranch;
					LocalBranch localBranch = repositoryData.References.LocalMain(gitModule);
					base.DialogTitle = Translate("Finish Branch");
					base.DialogDescription = string.Format(Translate("Finish '{0}' and merge it into '{1}'"), activeBranch.Name, localBranch.Name);
					base.SubmitButtonTitle = Translate("Finish");
					CurrentBranchGitPointView.Value = activeBranch;
					MainBranchGitPointView.Value = localBranch;
					UpdateSubmitButton();
				}
			}
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
