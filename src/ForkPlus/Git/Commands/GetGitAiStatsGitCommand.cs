using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 执行 <c>git-ai stats &lt;rev-or-range&gt; --json</c> 获取 AI 作者统计。
	/// revSpec 形态（git-ai 1.x）：
	/// <list type="bullet">
	/// <item>单提交：sha / HEAD → 根级 JSON</item>
	/// <item>区间：sha1..sha2 → { authorship_stats, range_stats } 嵌套 JSON</item>
	/// </list>
	/// 两种形态由 GitAiStats.Decode 统一解析。
	/// </summary>
	public class GetGitAiStatsGitCommand
	{
		/// <summary>
		/// 获取 AI 统计。
		/// </summary>
		/// <param name="gitModule">仓库模块。</param>
		/// <param name="gitAiPath">git-ai 可执行文件路径（App.GitAiPath），null 表示未安装。</param>
		/// <param name="revSpec">统计目标：单提交（"HEAD"/sha）或区间（"a..b"）。null/空等同 "HEAD"。</param>
		public GitCommandResult<GitAiStats> Execute(GitModule gitModule, [Null] string gitAiPath, [Null] string revSpec)
		{
			if (string.IsNullOrWhiteSpace(gitAiPath) || !File.Exists(gitAiPath))
			{
				return GitCommandResult<GitAiStats>.Failure(new GitCommandError.GenericError("git-ai not found. Install it from https://usegitai.com and configure the instance in Preferences → Git."));
			}
			string target = string.IsNullOrWhiteSpace(revSpec) ? "HEAD" : revSpec;
			try
			{
				GitRequestResult result = new ShellRequest(gitModule.Path, gitAiPath, new string[3] { "stats", target, "--json" }).Execute();
				if (!result.Success)
				{
					return GitCommandResult<GitAiStats>.Failure(new GitCommandError.GenericError("git-ai stats '" + target + "' failed: " + result.Stderr.Trim()));
				}
				return GitCommandResult<GitAiStats>.Success(GitAiStats.Decode(result.Stdout));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git-ai stats for '" + target + "'", ex);
				return GitCommandResult<GitAiStats>.Failure(new GitCommandError.GenericError("Failed to parse git-ai stats output: " + ex.Message));
			}
		}
	}
}
