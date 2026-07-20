using System;
using System.IO;
using System.Text;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	public class CommitGitCommand
	{
		public GitCommandResult Execute(GitModule gitModule, string message, bool amend, bool commitAndPush, JobMonitor monitor, bool noVerify = false)
		{
			string text = gitModule.CommitMessagePath();
			try
			{
				// git 默认按 UTF-8 读取 commit message 文件；显式写 UTF-8 无 BOM，
				// 避免 .NET 默认编码（部分系统为 ANSI）导致非 ASCII commit 乱码。
				File.WriteAllText(text, message, new UTF8Encoding(false));
			}
			catch (Exception ex)
			{
				return GitCommandResult.Failure(ex);
			}
			GitCommand gitCommand = new GitCommand("commit");
			if (amend)
			{
				gitCommand.Add("--amend");
			}
			if (gitModule.Settings.SignOff)
			{
				gitCommand.Add("--signoff");
			}
			if (noVerify)
			{
				gitCommand.Add("--no-verify");
			}
			gitCommand.Add("--file");
			gitCommand.Add(text.Quotify());
			monitor.Append(null, gitCommand);
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).ExecuteLong(delegate(string stdOutLine)
			{
				monitor.AppendOutputLine(stdOutLine);
			}, delegate(string line)
			{
				monitor.AppendOutputLine(line);
			}, monitor, 3);
			if (monitor.IsCanceled)
			{
				return GitCommandResult.Failure(new GitCommandError.Cancelled());
			}
			if (!gitRequestResult.Success)
			{
				if (GitCommandError.RepositoryIsLocked.Test(gitRequestResult.Stderr))
				{
					return GitCommandResult.Failure(new GitCommandError.RepositoryIsLocked(gitRequestResult));
				}
				return GitCommandResult.Failure(new GitCommandError.CommitFailed(message, amend, commitAndPush, monitor.Output));
			}
			return GitCommandResult.Success();
		}
	}
}
