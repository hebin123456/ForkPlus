using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 把单个贮藏（stash）导出为补丁文件（贮藏右键 →「另存为补丁」）。
	/// 用 `git stash show -p --binary` 生成差异：--binary 保证二进制改动完整写入补丁
	/// （否则会退化成 "Binary files differ"，补丁无法应用）。
	/// 贮藏若包含未跟踪文件（保存贮藏时勾选了"包含未跟踪文件"，stash 会有第三个父提交），
	/// 需 `--include-untracked`（git 2.32+）才能一并导出；老版本 git 不认该选项时，
	/// 退回仅导出已跟踪改动。
	/// </summary>
	public class ExportStashPatchGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string stashRef, string filePath)
		{
			GitRequestResult gitRequestResult = Run(gitModule, stashRef, includeUntracked: true);
			if (!gitRequestResult.Success)
			{
				gitRequestResult = Run(gitModule, stashRef, includeUntracked: false);
			}
			if (!gitRequestResult.Success)
			{
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			try
			{
				File.WriteAllText(filePath, gitRequestResult.Stdout);
				return GitCommandResult.Success();
			}
			catch (Exception ex)
			{
				Log.Error($"Cannot create stash patch. Error: '{ex}'");
				return GitCommandResult.Failure(new GitCommandError.UnknownException(ex));
			}
		}

		private static GitRequestResult Run(GitModule gitModule, string stashRef, bool includeUntracked)
		{
			GitCommand gitCommand = new GitCommand("stash", "show", "-p", "--binary");
			if (includeUntracked)
			{
				gitCommand.Add("--include-untracked");
			}
			gitCommand.Add(stashRef);
			return new GitRequest(gitModule).Command(gitCommand).Execute();
		}
	}
}