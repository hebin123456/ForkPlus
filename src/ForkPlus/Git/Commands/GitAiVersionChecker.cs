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
			return ParseVersion(result.Result);
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
