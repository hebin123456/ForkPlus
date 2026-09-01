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
		/// git-ai diff 超时（毫秒）。单提交归属本应亚秒级完成；超时通常意味着
		/// daemon 冷启动卡住或异常，此时放弃归属（Blame 先正常显示），不再拖住整个 blame 流程。
		/// </summary>
		private const int TimeoutMilliseconds = 15000;

		/// <summary>
		/// 获取指定提交的 AI 归属。命中缓存（含此前确认过的空归属）时零开销直接返回。
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
			string shaText = sha.ToString();
			GitAiDiffAttribution cached = GitAiResultCache.GetDiffAttribution(gitModule.Path, shaText);
			if (cached != null)
			{
				return GitCommandResult<GitAiDiffAttribution>.Success(cached);
			}
			try
			{
				GitRequestResult result = new ShellRequest(gitModule.Path, gitAiPath, new string[3] { "diff", shaText, "--json" }).Execute(TimeoutMilliseconds);
				if (!result.Success)
				{
					// 老仓库/根提交/未使用 git-ai 的仓库可能产生非零退出码，属正常情况，
					// 记日志并返回空归属，不打断正常 blame 流程。失败不缓存（可能是 daemon 冷启动瞬时问题）。
					Log.Info("git-ai diff for '" + shaText + "' returned no attribution: " + result.Stderr.Trim());
					return GitCommandResult<GitAiDiffAttribution>.Success(GitAiDiffAttribution.Empty);
				}
				GitAiDiffAttribution attribution = string.IsNullOrWhiteSpace(result.Stdout)
					? GitAiDiffAttribution.Empty
					: GitAiDiffAttribution.Decode(result.Stdout);
				// 空归属同样缓存：未使用 git-ai 的仓库后续 blame 不再反复 spawn 进程
				GitAiResultCache.PutDiffAttribution(gitModule.Path, shaText, attribution);
				return GitCommandResult<GitAiDiffAttribution>.Success(attribution);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git-ai diff attribution for '" + shaText + "'", ex);
				// AI 归属是增强信息，解析失败不影响主流程；不缓存，下次重试
				return GitCommandResult<GitAiDiffAttribution>.Success(GitAiDiffAttribution.Empty);
			}
		}
	}
}
