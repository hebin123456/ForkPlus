using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowInteractiveRebaseWindowCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Interactive Rebase Branch";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void ExecuteReword(RepositoryUserControl rc, GitModule gitModule, LocalBranch source, Revision revisionToReword)
		{
			RevisionStorage revisionStorage = rc.RepositoryData?.RevisionStorage;
			if (revisionStorage != null)
			{
				Revision parentRevision = revisionStorage.GetParentRevision(gitModule, revisionToReword.Sha);
				Execute(rc, gitModule, source, parentRevision, new IrAction.Reword(revisionToReword.Sha.ToString()));
			}
		}

		public void ExecuteEdit(RepositoryUserControl rc, GitModule gitModule, LocalBranch source, Revision revisionToEdit)
		{
			RevisionStorage revisionStorage = rc.RepositoryData?.RevisionStorage;
			if (revisionStorage != null)
			{
				Revision parentRevision = revisionStorage.GetParentRevision(gitModule, revisionToEdit.Sha);
				Execute(rc, gitModule, source, parentRevision, new IrAction.Edit(revisionToEdit.Sha.ToString()));
			}
		}

		public void ExecuteDrop(RepositoryUserControl rc, GitModule gitModule, LocalBranch source, Revision[] revisionsToDrop)
		{
			RevisionStorage revisionStorage = rc.RepositoryData?.RevisionStorage;
			if (revisionStorage == null)
			{
				return;
			}
			Revision revision = revisionsToDrop?.LastItem();
			if (revision == null)
			{
				return;
			}
			Revision parentRevision = revisionStorage.GetParentRevision(gitModule, revision.Sha);
			if (parentRevision != null)
			{
				string[] shas = revisionsToDrop.Map((Revision x) => x.Sha.ToString());
				Execute(rc, gitModule, source, parentRevision, new IrAction.Drop(shas));
			}
		}

		public void ExecuteSquash(RepositoryUserControl rc, GitModule gitModule, LocalBranch source, Revision[] revisionsToSquash)
		{
			RevisionStorage revisionStorage = rc.RepositoryData?.RevisionStorage;
			if (revisionStorage == null)
			{
				return;
			}
			Revision revision = revisionsToSquash?.LastItem();
			if (revision == null)
			{
				return;
			}
			Revision parentRevision = revisionStorage.GetParentRevision(gitModule, revision.Sha);
			if (parentRevision == null)
			{
				Log.Error("Cannot find squash dst for " + revision.Sha);
				return;
			}
			Revision parentRevision2 = revisionStorage.GetParentRevision(gitModule, parentRevision.Sha);
			string[] shas = revisionsToSquash.Map((Revision x) => x.Sha.ToString());
			Execute(rc, gitModule, source, parentRevision2, new IrAction.Squash(shas));
		}

		public void ExecuteFixup(RepositoryUserControl rc, GitModule gitModule, LocalBranch source, Revision revisionToFixup)
		{
			RevisionStorage revisionStorage = rc.RepositoryData?.RevisionStorage;
			if (revisionStorage != null)
			{
				Revision parentRevision = revisionStorage.GetParentRevision(gitModule, revisionToFixup.Sha);
				if (parentRevision == null)
				{
					Log.Error("Cannot find fixup dst for " + revisionToFixup.Sha);
					return;
				}
				Revision parentRevision2 = revisionStorage.GetParentRevision(gitModule, parentRevision.Sha);
				Execute(rc, gitModule, source, parentRevision2, new IrAction.Fixup(revisionToFixup.Sha.ToString()));
			}
		}

		public void ExecuteDragAndDropFixup(RepositoryUserControl repositoryUserControl, LocalBranch rebaseSrc, Sha fixupSrc, Sha fixupDst)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				RevisionStorage revisionStorage = repositoryUserControl.RepositoryData?.RevisionStorage;
				if (revisionStorage != null)
				{
					bool flag = revisionStorage.RevisionRangeContainsSha(rebaseSrc.Sha, fixupDst, fixupSrc);
					Revision parentRevision = revisionStorage.GetParentRevision(gitModule, flag ? fixupDst : fixupSrc);
					Execute(repositoryUserControl, gitModule, rebaseSrc, parentRevision, new IrAction.Fixup(fixupSrc.ToString(), fixupDst));
				}
			}
		}

		public void ExecuteDragAndDropMove(RepositoryUserControl repositoryUserControl, LocalBranch rebaseSrc, Sha moveSrc, Sha moveDst)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				RevisionStorage revisionStorage = repositoryUserControl.RepositoryData?.RevisionStorage;
				if (revisionStorage != null)
				{
					bool flag = revisionStorage.RevisionRangeContainsSha(rebaseSrc.Sha, moveDst, moveSrc);
					Revision parentRevision = revisionStorage.GetParentRevision(gitModule, flag ? moveDst : moveSrc);
					Execute(repositoryUserControl, gitModule, rebaseSrc, parentRevision, new IrAction.Move(moveSrc.ToString(), moveDst));
				}
			}
		}

		public void Execute(RepositoryUserControl rc, [Null] GitModule gitModule, [Null] LocalBranch rebaseSrc, [Null] IGitPoint rebaseDst, [Null] IrAction initialAction = null)
		{
			if (gitModule == null || rebaseSrc == null)
			{
				return;
			}
			if (!rebaseSrc.IsActive)
			{
				GitCommandResult gitCommandResult = new CheckoutBranchGitCommand().Execute(gitModule, rebaseSrc, new JobMonitor());
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(rc, gitCommandResult.Error).ShowDialog();
				}
			}
			using InteractiveRebaseWindow interactiveRebaseWindow = new InteractiveRebaseWindow(rc, gitModule, rebaseSrc, rebaseDst, initialAction);
			if (interactiveRebaseWindow.ShowDialog().GetValueOrDefault())
			{
				rc.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.Submodules | SubDomain.BugtrackerSettings | SubDomain.CustomCommands | SubDomain.References, new RevisionSelector.Head());
				if (!interactiveRebaseWindow.GitResult.Succeeded && !IsCanceled(interactiveRebaseWindow.GitResult.Error))
				{
					new ErrorWindow(rc, interactiveRebaseWindow.GitResult.Error).ShowDialog();
				}
			}
		}

		private static bool IsCanceled(GitCommandError error)
		{
			if (error is GitCommandError.GitError gitError && gitError.Stderr.Contains("Could not execute editor"))
			{
				return true;
			}
			return false;
		}
	}
}
