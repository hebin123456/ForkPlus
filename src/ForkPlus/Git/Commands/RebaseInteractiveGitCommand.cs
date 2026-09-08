using System;
using System.IO;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class RebaseInteractiveGitCommand
	{
		private static readonly string TokenSeparator = "#!_";

		/// <summary>
		/// git 的编辑器优先级是 GIT_EDITOR/GIT_SEQUENCE_EDITOR 环境变量 &gt; -c core.editor/sequence.editor。
		/// 用户（或启动 ForkPlus 的终端）若设置了这些环境变量，会盖掉命令行里的 -c 配置，
		/// 导致 reword/squash 停下来时 git 调的是外部编辑器而不是 ForkPlus.RI——消息改写静默失效。
		/// 因此除了 -c 之外，同时以环境变量形式显式钉死两个编辑器。
		/// </summary>
		internal static (string, string)[] BuildEditorEnv(string riHelperPath)
		{
			return new (string, string)[2]
			{
				("GIT_EDITOR", riHelperPath),
				("GIT_SEQUENCE_EDITOR", riHelperPath)
			};
		}

		public GitCommandResult Execute(GitModule gitModule, [Null] IGitPoint destination)
		{
			string input = PathHelper.NormalizeUnix(Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.RIHelperFilename));
			// RI.exe 缺失（部署不完整）时直接给出明确错误，避免 git 报晦涩的
			// "there was a problem with the editor '<路径>'"，让用户误以为路径写死。
			GitCommandResult helperMissing = ContinueRebaseGitCommand.CheckRebaseHelperExists(input);
			if (helperMissing != null)
			{
				return helperMissing;
			}
			GitCommand gitCommand = new GitCommand(App.OverrideCredentialHelper, "-c", "core.commentChar=" + Consts.Git.CommentChar, "-c", "rebase.instructionFormat=" + TokenSeparator + "%H", "-c", "rebase.abbreviateCommands=true", "-c", "sequence.editor=" + input.EscapeSpaces().Quotify(), "-c", "core.editor=" + input.EscapeSpaces().Quotify(), "rebase", "-i", "--autosquash", "--update-refs");
			if (destination == null)
			{
				gitCommand.Add("--root");
			}
			else
			{
				gitCommand.Add(destination.ObjectName);
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Env(BuildEditorEnv(input)).Execute();
			if (!gitRequestResult.Success)
			{
				if (gitRequestResult.Stderr.IndexOf("nothing to do", StringComparison.OrdinalIgnoreCase) != -1 || gitRequestResult.Stderr.Contains("error: Failed to merge in the changes.") || gitRequestResult.Stderr.Contains("Resolve all conflicts manually, mark them as resolved with"))
				{
					return GitCommandResult.Success();
				}
				return GitCommandResult.Failure(gitRequestResult.ToGitCommandError());
			}
			return GitCommandResult.Success();
		}
	}
}
