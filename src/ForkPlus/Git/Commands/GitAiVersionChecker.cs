using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 检测 git-ai 可执行文件版本。git-ai（https://github.com/git-ai-project/git-ai）
	/// 是通过 Git Notes（refs/notes/ai）追踪 AI 生成代码的 Git 扩展，
	/// ForkPlus 的 AI 归属（AI Blame / AI 统计）功能依赖它存在。
	/// </summary>
	public static class GitAiVersionChecker
	{
		/// <summary>
		/// ForkPlus 依赖的最低 git-ai 版本。git-ai 自 1.x 起提供 blame/diff/stats 的
		/// JSON 输出（Git AI Standard v3.0.0），低于 1.0.0 时警告。
		/// </summary>
		public static readonly Version MinimumRequiredVersion = new Version(1, 0, 0);

		/// <summary>
		/// 获取指定 git-ai 可执行文件的版本号；失败返回 null。
		/// </summary>
		public static Version GetVersion(string gitAiPath)
		{
			if (string.IsNullOrWhiteSpace(gitAiPath))
			{
				return null;
			}
			GitCommandResult<string> result = new GetGitAiVersionShellCommand().Execute(gitAiPath);
			if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Result))
			{
				return null;
			}
			// git 代理模式误判防线（2026-09-07 "git-ai stats 报 git: 'stats' is not a git
			// command" 修复链）：可执行文件名不是 git-ai 时，git-ai 会把 --version 转发给真 git
			// （退出码 0、输出 "git version 2.34.1"）。解析出 2.34.1 会被误判为"版本正常"
			// （>= 1.0.0），偏好设置显示假版本、掩盖配置问题——识别该形态返回 null（Unknown）。
			if (LooksLikeGitProxyVersionOutput(result.Result))
			{
				return null;
			}
			return ParseVersion(result.Result);
		}

		/// <summary>
		/// 检测版本输出是否来自 git 代理转发（形如 "git version 2.34.1"，git-ai 原生 --version
		/// 只输出纯版本号如 "1.7.2"）。internal 供回归测试直接验证。
		/// </summary>
		internal static bool LooksLikeGitProxyVersionOutput([Null] string raw)
		{
			return raw != null && raw.TrimStart().StartsWith("git version", StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// 解析 git-ai --version 输出。兼容 "git-ai version 1.7.0"、"git-ai 1.7.0"、"1.7.0" 等格式。
		/// </summary>
		public static Version ParseVersion(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				return null;
			}
			Match match = Regex.Match(raw, @"(\d+)\.(\d+)(?:\.(\d+))?", RegexOptions.None);
			if (!match.Success)
			{
				return null;
			}
			int major = int.Parse(match.Groups[1].Value);
			int minor = int.Parse(match.Groups[2].Value);
			int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
			return new Version(major, minor, build);
		}

		/// <summary>
		/// 检查指定 git-ai 路径的版本，返回检查结果。
		/// </summary>
		public static GitAiVersionCheckResult Check(string gitAiPath)
		{
			if (string.IsNullOrWhiteSpace(gitAiPath) || !File.Exists(gitAiPath))
			{
				return new GitAiVersionCheckResult(null, GitAiVersionStatus.NotFound);
			}
			Version version = GetVersion(gitAiPath);
			if (version == null)
			{
				return new GitAiVersionCheckResult(null, GitAiVersionStatus.Unknown);
			}
			if (version < MinimumRequiredVersion)
			{
				return new GitAiVersionCheckResult(version, GitAiVersionStatus.Unsupported);
			}
			return new GitAiVersionCheckResult(version, GitAiVersionStatus.Ok);
		}
	}

	public enum GitAiVersionStatus
	{
		Ok,
		Unsupported,
		NotFound,
		Unknown
	}

	public struct GitAiVersionCheckResult
	{
		public Version Version { get; }

		public GitAiVersionStatus Status { get; }

		public GitAiVersionCheckResult(Version version, GitAiVersionStatus status)
		{
			Version = version;
			Status = status;
		}
	}
}
