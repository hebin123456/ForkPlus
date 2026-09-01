using System;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 执行 <c>git-ai diff &lt;sha&gt; --json</c> 获取单个提交的行级 AI 归属数据。
	/// 用于 Blame 窗口给该提交新增的行打 AI 徽标（agent tool / model）。
	/// git-ai 未安装或该提交无 AI 归属时按需返回空结果，不报错。
	/// </summary>
	public class GetGitAiDiffAttributionGitCommand
	{
		/// <summary>
		/// 获取指定提交的 AI 归属。
		/// </summary>
		/// <param name="gitModule">仓库模块。</param>
		/// <param name="sha">提交 sha。</param>
		/// <param name="gitAiPath">git-ai 可执行文件路径（App.GitAiPath），null 表示未安装。</param>
		public GitCommandResult<GitAiDiffAttribution> Execute(GitModule gitModule, Sha sha, [Null] string gitAiPath)
		{
			if (gitAiPath == null || !File.Exists(gitAiPath))
			{
				return GitCommandResult<GitAiDiffAttribution>.Success(GitAiDiffAttribution.Empty);
			}
			try
			{
				GitRequestResult result = new ShellRequest(gitModule.Path, gitAiPath, new string[3] { "diff", sha.ToString(), "--json" }).Execute();
				if (!result.Success)
				{
					// 老仓库/根提交/未使用 git-ai 的仓库可能产生非零退出码，属正常情况，
					// 记日志并返回空归属，不打断正常 blame 流程。
					Log.Info("git-ai diff for '" + sha + "' returned no attribution: " + result.Stderr.Trim());
					return GitCommandResult<GitAiDiffAttribution>.Success(GitAiDiffAttribution.Empty);
				}
				if (string.IsNullOrWhiteSpace(result.Stdout))
				{
					return GitCommandResult<GitAiDiffAttribution>.Success(GitAiDiffAttribution.Empty);
				}
				return GitCommandResult<GitAiDiffAttribution>.Success(GitAiDiffAttribution.Decode(result.Stdout));
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git-ai diff attribution for '" + sha + "'", ex);
				// AI 归属是增强信息，解析失败不影响主流程
				return GitCommandResult<GitAiDiffAttribution>.Success(GitAiDiffAttribution.Empty);
			}
		}
	}
}
