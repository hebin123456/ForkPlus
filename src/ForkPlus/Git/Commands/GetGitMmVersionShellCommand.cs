using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 执行 <c>git-mm.exe --version</c> 并返回原始版本字符串。
	/// </summary>
	public class GetGitMmVersionShellCommand
	{
		public GitCommandResult<string> Execute(string path)
		{
			try
			{
				if (!File.Exists(path))
				{
					Log.Error("Cannot find git-mm instance at: '" + path + "'");
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
				Log.Error("Failed to get git-mm version for '" + path + "'", ex);
				return GitCommandResult<string>.Failure(ex);
			}
		}

		/// <summary>
		/// 取版本输出的首行，去除内嵌换行符（含 Windows \r\n）。
		/// git-mm --version 可能输出多行（版本号 + build info），内嵌换行会导致下拉框每项显示两行。
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
