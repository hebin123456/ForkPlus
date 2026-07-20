using System.Text;

namespace ForkPlus.Git.Commands
{
	internal class CreatePatchGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule, ChangedFile[] changedFiles, bool amend)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (ChangedFile changedFile in changedFiles)
			{
				GitCommandResult<string> changesAsBinaryPatch = new GetWorkingDirectoryFileChangesGitCommand().GetChangesAsBinaryPatch(gitModule, changedFile, amend);
				if (!string.IsNullOrEmpty(changesAsBinaryPatch.Result))
				{
					stringBuilder.Append(changesAsBinaryPatch.Result);
				}
				else if (changesAsBinaryPatch.Error != null)
				{
					return GitCommandResult<string>.Failure(changesAsBinaryPatch.Error);
				}
			}
			return GitCommandResult<string>.Success(stringBuilder.ToString());
		}
	}
}
