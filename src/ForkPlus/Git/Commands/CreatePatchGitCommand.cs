using System;
using System.Text;

namespace ForkPlus.Git.Commands
{
	internal class CreatePatchGitCommand
	{
		// 修复（2026-09-14，"另存为补丁内容多时界面卡住"）：新增 progress 回调（已处理文件数/总数），
		// 供 UI 层后台执行时逐文件汇报进度（Action<index, total>，完成后第 N 个文件回调 N/N）。
		public GitCommandResult<string> Execute(GitModule gitModule, ChangedFile[] changedFiles, bool amend, Action<int, int> progress = null)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int index = 0; index < changedFiles.Length; index++)
			{
				ChangedFile changedFile = changedFiles[index];
				GitCommandResult<string> changesAsBinaryPatch = new GetWorkingDirectoryFileChangesGitCommand().GetChangesAsBinaryPatch(gitModule, changedFile, amend);
				if (!string.IsNullOrEmpty(changesAsBinaryPatch.Result))
				{
					stringBuilder.Append(changesAsBinaryPatch.Result);
				}
				else if (changesAsBinaryPatch.Error != null)
				{
					return GitCommandResult<string>.Failure(changesAsBinaryPatch.Error);
				}
				progress?.Invoke(index + 1, changedFiles.Length);
			}
			return GitCommandResult<string>.Success(stringBuilder.ToString());
		}
	}
}
