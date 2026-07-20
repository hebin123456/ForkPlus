using System;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 检测 git 可执行文件版本是否满足最低要求。
	/// </summary>
	public static class GitVersionChecker
	{
		/// <summary>
		/// ForkPlus 依赖的最低 git 版本（含 --diff-merges、--no-show-signature、--no-optional-locks、core.checkStat 等特性）。
		/// </summary>
		public static readonly Version MinimumRequiredVersion = new Version(2, 31, 0);

		/// <summary>
		/// 推荐的 git 版本，低于此版本会给出建议升级提示但不阻止使用。
		/// </summary>
		public static readonly Version RecommendedVersion = new Version(2, 40, 0);

		/// <summary>
		/// 获取指定 git 可执行文件的版本号；失败返回 null。
		/// </summary>
		public static Version GetVersion(string gitPath)
		{
			if (string.IsNullOrWhiteSpace(gitPath))
			{
				return null;
			}
			GitCommandResult<string> result = new GetGitVersionGitCommand().Execute(gitPath);
			if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Result))
			{
				return null;
			}
			return ParseVersion(result.Result);
		}

		/// <summary>
		/// 解析 git version 命令输出（如 "git version 2.43.0.windows.1"）为 Version。
		/// </summary>
		public static Version ParseVersion(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
			{
				return null;
			}
			string text = raw.Trim();
			int idx = text.IndexOf("version", StringComparison.OrdinalIgnoreCase);
			if (idx >= 0)
			{
				text = text.Substring(idx + "version".Length).Trim();
			}
			string[] parts = text.Split(new[] { ' ', '.' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0)
			{
				return null;
			}
			int major = 0, minor = 0, build = 0;
			if (!int.TryParse(parts[0], out major))
			{
				return null;
			}
			if (parts.Length >= 2 && int.TryParse(parts[1], out minor))
			{
				if (parts.Length >= 3 && int.TryParse(parts[2], out build))
				{
				}
			}
			return new Version(major, minor, build);
		}

		/// <summary>
		/// 检查指定 git 路径的版本，返回检查结果。
		/// </summary>
		public static GitVersionCheckResult Check(string gitPath)
		{
			Version version = GetVersion(gitPath);
			if (version == null)
			{
				return new GitVersionCheckResult(null, GitVersionStatus.Unknown);
			}
			if (version < MinimumRequiredVersion)
			{
				return new GitVersionCheckResult(version, GitVersionStatus.Unsupported);
			}
			if (version < RecommendedVersion)
			{
				return new GitVersionCheckResult(version, GitVersionStatus.Outdated);
			}
			return new GitVersionCheckResult(version, GitVersionStatus.Ok);
		}
	}

	public enum GitVersionStatus
	{
		Ok,
		Outdated,
		Unsupported,
		Unknown
	}

	public struct GitVersionCheckResult
	{
		public Version Version { get; }

		public GitVersionStatus Status { get; }

		public GitVersionCheckResult(Version version, GitVersionStatus status)
		{
			Version = version;
			Status = status;
		}
	}
}
