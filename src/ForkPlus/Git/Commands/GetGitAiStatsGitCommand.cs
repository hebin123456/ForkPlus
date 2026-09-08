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
		/// git-ai stats 超时（毫秒）。全历史/大区间统计可能要遍历大量提交的 notes，耗时可达数十秒；
		/// 超时后返回失败（统计区显示错误，用户可重试），避免任务无限挂在 JobQueue 里。
		/// </summary>
		private const int TimeoutMilliseconds = 60000;

		/// <summary>
		/// 获取 AI 统计。命中缓存时零开销直接返回（再次打开统计页/切回同一区间秒出结果）。
		/// </summary>
		/// <param name="gitModule">仓库模块。</param>
		/// <param name="gitAiPath">git-ai 可执行文件路径（App.GitAiPath），null 表示未安装。</param>
		/// <param name="revSpec">统计目标：单提交（"HEAD"/sha）或区间（"a..b"）。null/空等同 "HEAD"。</param>
		/// <param name="forceRefresh">true 时跳过缓存强制重查（统计页 Refresh 按钮使用）。</param>
		public GitCommandResult<GitAiStats> Execute(GitModule gitModule, [Null] string gitAiPath, [Null] string revSpec, bool forceRefresh = false)
		{
			if (string.IsNullOrWhiteSpace(gitAiPath) || !File.Exists(gitAiPath))
			{
				return GitCommandResult<GitAiStats>.Failure(new GitCommandError.GenericError("git-ai not found. Install it from https://usegitai.com and configure the instance in Preferences → Git."));
			}
			string target = string.IsNullOrWhiteSpace(revSpec) ? "HEAD" : revSpec;
			if (!forceRefresh)
			{
				GitAiStats cached = GitAiResultCache.GetStats(gitModule.Path, target);
				if (cached != null)
				{
					return GitCommandResult<GitAiStats>.Success(cached);
				}
			}
			try
			{
				GitRequestResult result = new ShellRequest(gitModule.Path, gitAiPath, new string[3] { "stats", target, "--json" }).Execute(TimeoutMilliseconds);
				if (!result.Success)
				{
					string stderr = result.Stderr.Trim();
					// git 代理转发症状（2026-09-07 修复链的兜底防线）：git-ai 可执行文件名不是
					// git-ai 时，git-ai 会把 stats 原样转发给真 git（git: 'stats' is not a git
					// command）。App.GitAiPath 的 staging 符号链接已根治常规场景；此处兜底
					// staging 降级（如 Windows 无符号链接权限）等残余场景，把 git 的原始报错
					// 翻译成可定位的提示，而不是让用户对着一脸懵的 "not a git command"。
					string message = IsGitProxyForwardingSymptom(stderr)
						? "git-ai at '" + gitAiPath + "' responded as a git proxy (its file name is not '" + App.GitAiExecutableName
							+ "', so git-ai forwards unknown commands to git). Rename the executable to '" + App.GitAiExecutableName
							+ "' or reconfigure Preferences \u2192 Git with the original git-ai binary."
						: "git-ai stats '" + target + "' failed: " + stderr;
					return GitCommandResult<GitAiStats>.Failure(new GitCommandError.GenericError(message));
				}
				GitAiStats stats = GitAiStats.Decode(result.Stdout);
				GitAiResultCache.PutStats(gitModule.Path, target, stats);
				return GitCommandResult<GitAiStats>.Success(stats);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to get git-ai stats for '" + target + "'", ex);
				return GitCommandResult<GitAiStats>.Failure(new GitCommandError.GenericError("Failed to parse git-ai stats output: " + ex.Message));
			}
		}

		/// <summary>
		/// 识别 stderr 是否为 git 代理转发症状（git 报 "xxx is not a git command"）——
		/// git-ai 以非标准文件名被调用时进入 git 透明代理模式，把 stats 转发给真 git 所致。
		/// </summary>
		internal static bool IsGitProxyForwardingSymptom([Null] string stderr)
		{
			return stderr != null && stderr.Contains("is not a git command", StringComparison.Ordinal);
		}
	}
}
