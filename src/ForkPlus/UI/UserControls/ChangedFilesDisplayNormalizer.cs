using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using Microsoft.Win32.SafeHandles;

namespace ForkPlus.UI.UserControls
{
	internal static class ChangedFilesDisplayNormalizer
	{
		public static ChangedFile[] NormalizeForDisplay(GitModule gitModule, ChangedFile[] changedFiles, GitMmUserControl gitMmUserControl)
		{
			if (gitModule == null || gitMmUserControl == null || changedFiles == null || changedFiles.Length == 0)
			{
				return changedFiles ?? new ChangedFile[0];
			}
			Dictionary<string, string> gitLinkTargets = GetGitStyleLinkTargets(gitModule, changedFiles);
			return changedFiles.Filter((ChangedFile changedFile) => !IsGitMmManagedSubrepoChange(gitModule, gitMmUserControl, changedFile, gitLinkTargets)).ToArray();
		}

		private static bool IsGitMmManagedSubrepoChange(GitModule gitModule, GitMmUserControl gitMmUserControl, ChangedFile changedFile, Dictionary<string, string> gitLinkTargets)
		{
		 string changedFilePath = null;
		 if (changedFile is SubmoduleChangedFile submoduleChangedFile)
		 {
		  changedFilePath = submoduleChangedFile.Submodule.Path;
		 }
		 else if (changedFile.FileMode == "160000" || IsGitWorkTreePath(gitModule, changedFile.Path))
		 {
		  changedFilePath = changedFile.Path;
		 }
		 if (!string.IsNullOrWhiteSpace(changedFilePath))
		 {
		  return gitMmUserControl.ContainsSubrepoPath(gitModule.MakePath(changedFilePath));
		 }
		 // Fallback: for regular ChangedFile entries without a submodule/gitlink marker,
		 // check if the file's absolute path is inside a git mm managed subrepo directory.
		 // This catches cases where git mm subrepos are not registered as .gitmodules entries
		 // and git status --porcelain doesn't report file modes (FileMode is null).
		 if (changedFile != null && !string.IsNullOrWhiteSpace(changedFile.Path))
		 {
		  string fullPath = gitModule.MakePath(changedFile.Path);
		  if (gitMmUserControl.ContainsSubrepoPath(fullPath))
		  {
		   return true;
		  }
		 }
		 return IsGitMmManagedLinkChange(gitModule, gitMmUserControl, changedFile, gitLinkTargets);
		}

		private static bool IsGitMmManagedLinkChange(GitModule gitModule, GitMmUserControl gitMmUserControl, ChangedFile changedFile, Dictionary<string, string> gitLinkTargets)
		{
			if (changedFile == null || string.IsNullOrWhiteSpace(changedFile.Path))
			{
				return false;
			}
			string fullPath = gitModule.MakePath(changedFile.Path);
			string targetPath = TryResolveReparsePointTarget(fullPath);
			if (string.IsNullOrWhiteSpace(targetPath) && !gitLinkTargets.TryGetValue(changedFile.Path, out targetPath))
			{
				return false;
			}
			return !string.IsNullOrWhiteSpace(targetPath) && gitMmUserControl.ContainsSubrepoPath(targetPath);
		}

		private static Dictionary<string, string> GetGitStyleLinkTargets(GitModule gitModule, ChangedFile[] changedFiles)
		{
			Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
			List<string> unstagedCandidates = new List<string>();
			List<string> stagedCandidates = new List<string>();
			foreach (ChangedFile changedFile in changedFiles)
			{
				if (!IsGitStyleLinkCandidate(gitModule, changedFile))
				{
					continue;
				}
				string fullPath = gitModule.MakePath(changedFile.Path);
				string targetPath = TryResolveGitStyleLinkText(fullPath);
				if (!string.IsNullOrWhiteSpace(targetPath))
				{
					result[changedFile.Path] = targetPath;
					continue;
				}
				if (changedFile.Staged)
				{
					stagedCandidates.Add(changedFile.Path);
				}
				else
				{
					unstagedCandidates.Add(changedFile.Path);
				}
			}
			AddGitStyleLinkTargetsFromDiff(gitModule, result, unstagedCandidates, staged: false);
			AddGitStyleLinkTargetsFromDiff(gitModule, result, stagedCandidates, staged: true);
			return result;
		}

		private static bool IsGitStyleLinkCandidate(GitModule gitModule, ChangedFile changedFile)
		{
			return changedFile != null
				&& !changedFile.IsDirectory
				&& !string.IsNullOrWhiteSpace(changedFile.Path)
				&& !(changedFile is SubmoduleChangedFile)
				&& changedFile.FileMode != "160000"
				&& !IsGitWorkTreePath(gitModule, changedFile.Path);
		}

		private static void AddGitStyleLinkTargetsFromDiff(GitModule gitModule, Dictionary<string, string> result, List<string> relativePaths, bool staged)
		{
			if (relativePaths.Count == 0)
			{
				return;
			}
			GitCommand command = new GitCommand("diff", "--no-ext-diff", "--no-color");
			if (staged)
			{
				command.Add("--cached");
			}
			command.Add("--");
			foreach (string relativePath in relativePaths.Take(200))
			{
				command.Add(relativePath.Quotify());
			}
			try
			{
				GitRequestResult diffResult = new GitRequest(gitModule).Command(command).Execute(silent: true);
				if (diffResult.Success && !string.IsNullOrWhiteSpace(diffResult.Stdout))
				{
					AddGitStyleLinkTargetsFromDiffOutput(gitModule, result, diffResult.Stdout);
				}
			}
			catch (Exception ex)
			{
				Log.Debug("Failed to read symlink targets from git diff: " + ex);
			}
		}

		private static void AddGitStyleLinkTargetsFromDiffOutput(GitModule gitModule, Dictionary<string, string> result, string diffOutput)
		{
			string currentPath = null;
			string linkTargetCandidate = null;
			bool hasNonLinkAddedLine = false;
			void FlushCandidate()
			{
				if (currentPath == null || string.IsNullOrWhiteSpace(linkTargetCandidate) || hasNonLinkAddedLine)
				{
					return;
				}
				try
				{
					result[currentPath] = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(gitModule.MakePath(currentPath)), linkTargetCandidate));
				}
				catch
				{
					result[currentPath] = linkTargetCandidate;
				}
			}
			foreach (string line in diffOutput.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
			{
				if (line.StartsWith("diff --git ", StringComparison.Ordinal))
				{
					FlushCandidate();
					currentPath = ParseDiffPath(line);
					linkTargetCandidate = null;
					hasNonLinkAddedLine = false;
					continue;
				}
				if (currentPath == null || line.StartsWith("+++", StringComparison.Ordinal) || !line.StartsWith("+", StringComparison.Ordinal))
				{
					continue;
				}
				string target = line.Substring(1).Trim();
				if (LooksLikeLinkTarget(target))
				{
					if (linkTargetCandidate == null)
					{
						linkTargetCandidate = target;
					}
					else if (!string.Equals(linkTargetCandidate, target, StringComparison.Ordinal))
					{
						hasNonLinkAddedLine = true;
					}
				}
				else if (!string.IsNullOrWhiteSpace(target))
				{
					hasNonLinkAddedLine = true;
				}
			}
			FlushCandidate();
		}

		[Null]
		private static string ParseDiffPath(string diffHeader)
		{
			const string prefix = "diff --git a/";
			int start = diffHeader.IndexOf(prefix, StringComparison.Ordinal);
			if (start < 0)
			{
				return null;
			}
			start += prefix.Length;
			int end = diffHeader.IndexOf(" b/", start, StringComparison.Ordinal);
			if (end <= start)
			{
				return null;
			}
			return diffHeader.Substring(start, end - start);
		}

		[Null]
		private static string TryResolveGitStyleLinkText(string linkPath)
		{
			try
			{
				string target = TryReadGitSymlinkText(linkPath);
				if (string.IsNullOrWhiteSpace(target))
				{
					return null;
				}
				return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(linkPath), target));
			}
			catch
			{
				return null;
			}
		}

		[Null]
		private static string TryReadGitSymlinkText(string linkPath)
		{
			try
			{
				if (!File.Exists(linkPath))
				{
					return null;
				}
				FileInfo fileInfo = new FileInfo(linkPath);
				if (fileInfo.Length > 4096)
				{
					return null;
				}
				string value = File.ReadAllText(linkPath).Trim();
				return LooksLikeLinkTarget(value) ? value : null;
			}
			catch
			{
				return null;
			}
		}

		internal static bool LooksLikeLinkTarget(string value)
		{
			if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new char[2] { '\0', '\r' }) >= 0 || value.Contains("\n"))
			{
				return false;
			}
			return value.Contains("/") || value.Contains("\\");
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder filePath, uint filePathLength, uint flags);

		[Null]
		private static string TryResolveReparsePointTarget(string path)
		{
			try
			{
				if (!File.Exists(path) && !Directory.Exists(path))
				{
					return null;
				}
				if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0)
				{
					return null;
				}
				const uint fileShareReadWriteDelete = 0x00000001 | 0x00000002 | 0x00000004;
				const uint openExisting = 3;
				const uint fileFlagBackupSemantics = 0x02000000;
				using (SafeFileHandle handle = CreateFile(path, 0, fileShareReadWriteDelete, IntPtr.Zero, openExisting, fileFlagBackupSemantics, IntPtr.Zero))
				{
					if (handle.IsInvalid)
					{
						return null;
					}
					StringBuilder buffer = new StringBuilder(1024);
					uint length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
					if (length == 0)
					{
						return null;
					}
					if (length >= buffer.Capacity)
					{
						buffer = new StringBuilder((int)length + 1);
						length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
						if (length == 0)
						{
							return null;
						}
					}
					return NormalizeWin32FinalPath(buffer.ToString());
				}
			}
			catch (Exception ex)
			{
				Log.Debug("Failed to resolve reparse point target for '" + path + "': " + ex);
				return null;
			}
		}

		private static string NormalizeWin32FinalPath(string path)
		{
			if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
			{
				return @"\\" + path.Substring(@"\\?\UNC\".Length);
			}
			if (path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
			{
				return path.Substring(@"\\?\".Length);
			}
			return path;
		}

		private static bool IsGitWorkTreePath(GitModule gitModule, string relativePath)
		{
			if (gitModule == null || string.IsNullOrWhiteSpace(relativePath))
			{
				return false;
			}
			string path = gitModule.MakePath(relativePath);
			return Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));
		}
	}
}
