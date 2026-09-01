using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 执行 <c>git-ai --version</c> 并返回原始版本字符串。
	/// git-ai 输出形如 "1.7.0"（纯版本号，可能带尾随换行），与 git-mm 的多行输出不同。
	/// </summary>
	public class GetGitAiVersionShellCommand
	{
		public GitCommandResult<string> Execute(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					Log.Error("Cannot find git-ai instance at: '" + path + "'");
					return GitCommandResult<string>.Failure(new GitCommandError.NotFound());
				}
				GitRequestResult gitRequestResult = new ShellRequest("", path, new string[1] { "--version" }).Execute();
				if (!gitRequestResult.Success)
				{
					return GitCommandResult<string>.Failure(gitRequestResult.ToGitCommandError());
				}
				return GitCommandResult<string>.Success(GetFirstLine(gitRequestResult.Stdout));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git-ai version for '" + path + "'", ex);
				return GitCommandResult<string>.Failure(ex);
			}
		}

		/// <summary>
		/// 取版本输出的首行并去除空白。git-ai --version 理论上只输出一行版本号，
		/// 但按 git-mm 同样的方式防御多行输出与内嵌换行符。
		/// </summary>
		private static string GetFirstLine(string output)
		{
			if (string.IsNullOrEmpty(output))
			{
				return "";
			}
			string normalized = output.Replace("\r\n", "\n");
			int newlineIndex = normalized.IndexOf('\n');
			return (newlineIndex >= 0 ? normalized.Substring(0, newlineIndex) : normalized).Trim();
		}
	}
}
