using System;
using System.IO;
using System.IO.Compression;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 把一次提交的完整文件树导出到目标目录（git archive --format=zip + 逐条目解压）。
	/// 供"用外部比对工具比较两个提交的完整文件树"功能使用。
	/// 解压采用与 GetCodeLineStatsGitCommand 相同的长路径（\\?\ 前缀）策略，
	/// 避免深嵌套仓库导出时路径超过 MAX_PATH(260) 报 DirectoryNotFoundException。
	/// </summary>
	public class ExportRevisionTreeGitCommand
	{
		public GitCommandResult<string> Execute(GitModule gitModule, Sha sha, string destinationDir, JobMonitor monitor)
		{
			Log.Info("Export revision tree of " + sha.ToAbbreviatedString() + " to " + destinationDir);
			try
			{
				Directory.CreateDirectory(destinationDir);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create directory at '" + destinationDir + "'", ex);
				return GitCommandResult<string>.Failure(ex);
			}
			string zipFile = Path.Combine(Path.GetDirectoryName(destinationDir), "tree_export_" + Guid.NewGuid().ToString("N") + ".zip");
			zipFile = Path.GetFullPath(zipFile);
			try
			{
				GitRequestResult archiveResult = new GitRequest(gitModule).Command("-c", "core.quotePath=false", "archive", "--format=zip", "-o", zipFile, sha.ToString()).Execute(monitor, silent: true);
				if (!archiveResult.Success || archiveResult.ExitCode != 0)
				{
					string stderr = archiveResult.Stderr ?? "";
					Log.Warn("git archive failed: " + stderr);
					return GitCommandResult<string>.Failure(archiveResult.ToGitCommandError());
				}
				if (monitor != null && monitor.IsCanceled)
				{
					return GitCommandResult<string>.Failure(new GitCommandError.GenericError("canceled"));
				}
				try
				{
					ExtractZipWithLongPathSupport(zipFile, destinationDir);
				}
				catch (Exception ex2)
				{
					Log.Error("zip extract failed", ex2);
					return GitCommandResult<string>.Failure(new GitCommandError.GenericError("zip extract failed: " + ex2.Message));
				}
				return GitCommandResult<string>.Success(destinationDir);
			}
			finally
			{
				try
				{
					if (File.Exists(zipFile))
					{
						File.Delete(zipFile);
					}
				}
				catch
				{
				}
			}
		}

		/// <summary>逐条目解压 zip。对超过 MAX_PATH(260) 的条目加 \\?\ 前缀以支持长路径
		/// （\\?\ 前缀要求绝对路径且无相对组件，git archive 的条目均为相对路径，
		/// 与已规范化的 destDir 拼接后满足该要求，支持到 ~32767 字符）。</summary>
		private static void ExtractZipWithLongPathSupport(string zipFile, string destDir)
		{
			using (var archive = System.IO.Compression.ZipFile.OpenRead(zipFile))
			{
				foreach (var entry in archive.Entries)
				{
					if (entry.FullName.EndsWith("/") || string.IsNullOrEmpty(entry.Name))
					{
						continue;
					}
					string relativePath = entry.FullName.Replace('/', '\\');
					string destPath = Path.Combine(destDir, relativePath);
					string longPath = destPath;
					if (longPath.Length > 248 && !longPath.StartsWith(@"\\?\"))
					{
						longPath = @"\\?\" + longPath;
					}
					string parentDir = Path.GetDirectoryName(longPath);
					if (!string.IsNullOrEmpty(parentDir))
					{
						Directory.CreateDirectory(parentDir);
					}
					entry.ExtractToFile(longPath, overwrite: true);
				}
			}
		}
	}
}
