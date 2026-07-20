using System;
using System.IO;
using System.Text.RegularExpressions;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 检测 git-mm 可执行文件版本是否满足最低要求（3.x）。
	/// </summary>
	public static class GitMmVersionChecker
	{
		/// <summary>
		/// ForkPlus 依赖的最低 git-mm 版本。低于此版本时启动与偏好设置中都会警告。
		/// </summary>
		public static readonly Version MinimumRequiredVersion = new Version(3, 0, 0);

		/// <summary>
		/// 获取指定 git-mm 可执行文件的版本号；失败返回 null。
		/// </summary>
		public static Version GetVersion(string gitMmPath)
		{
			if (string.IsNullOrWhiteSpace(gitMmPath))
			{
				return null;
			}
			GitCommandResult<string> result = new GetGitMmVersionShellCommand().Execute(gitMmPath);
			if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Result))
			{
				return null;
			}
			return ParseVersion(result.Result);
		}

		/// <summary>
		/// 解析 git-mm --version 输出。兼容 "git-mm version 3.0.0"、"git-mm 3.0.0"、"3.0.0" 等格式。
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
		/// 检查指定 git-mm 路径的版本，返回检查结果。
		/// </summary>
		public static GitMmVersionCheckResult Check(string gitMmPath)
		{
			if (string.IsNullOrWhiteSpace(gitMmPath) || !File.Exists(gitMmPath))
			{
				return new GitMmVersionCheckResult(null, GitMmVersionStatus.NotFound);
			}
			Version version = GetVersion(gitMmPath);
			if (version == null)
			{
				return new GitMmVersionCheckResult(null, GitMmVersionStatus.Unknown);
			}
			if (version < MinimumRequiredVersion)
			{
				return new GitMmVersionCheckResult(version, GitMmVersionStatus.Unsupported);
			}
			return new GitMmVersionCheckResult(version, GitMmVersionStatus.Ok);
		}
	}

	public enum GitMmVersionStatus
	{
		Ok,
		Unsupported,
		NotFound,
		Unknown
	}

	public struct GitMmVersionCheckResult
	{
		public Version Version { get; }

		public GitMmVersionStatus Status { get; }

		public GitMmVersionCheckResult(Version version, GitMmVersionStatus status)
		{
			Version = version;
			Status = status;
		}
	}
}
