using System.Collections.Generic;
using ForkPlus.Git.Interaction;

namespace ForkPlus.Git.Commands
{
	public class GetChangedFilesGitCommand
	{
		public GitCommandResult<ChangedFilesCollection> Execute(GitModule gitModule, [Null] Submodule[] submodules = null, bool excludeUntrackedFiles = false, bool includeIgnoredFiles = false)
		{
			submodules = submodules ?? new Submodule[0];
			GitCommand gitCommand = CreateReliableStatusCommand("status", "--porcelain", "-z");
			if (excludeUntrackedFiles)
			{
				gitCommand.Add("--untracked-files=no");
			}
			else
			{
				gitCommand.Add("--untracked-files=all");
			}
			if (includeIgnoredFiles)
			{
				gitCommand.Add("--ignored");
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<ChangedFilesCollection>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.Nul);
			List<ChangedFile> list = new List<ChangedFile>(128);
			int num = 0;
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i];
				if (text.Length <= 0 || !SplitStatusLine(array[i], out var status, out var workingDirectoryStatus, out var staged, out var unstaged, out var filepath))
				{
					continue;
				}
				num++;
				if (staged.HasValue && unstaged.HasValue)
				{
					if (staged == ChangeType.Untracked && unstaged == ChangeType.Untracked)
					{
						list.Add(new ChangedFile(filepath, status, workingDirectoryStatus));
						continue;
					}
					if (staged == ChangeType.Ignored && unstaged == ChangeType.Ignored)
					{
						list.Add(new ChangedFile(filepath, status, workingDirectoryStatus));
						continue;
					}
					if (staged == ChangeType.Unmerged || unstaged == ChangeType.Unmerged || (staged == ChangeType.Added && unstaged == ChangeType.Added) || (staged == ChangeType.Deleted && unstaged == ChangeType.Deleted))
					{
						Submodule submodule = submodules.FirstItem(filepath, (Submodule x, string p) => x.Path == p);
						if (submodule != null)
						{
							list.Add(new SubmoduleChangedFile(submodule, filepath, status, workingDirectoryStatus));
						}
						else
						{
							list.Add(new ChangedFile(filepath, status, workingDirectoryStatus));
						}
						continue;
					}
				}
				if (unstaged.HasValue)
				{
					switch (unstaged)
					{
					case ChangeType.Modified:
					{
						Submodule submodule2 = submodules.FirstItem(filepath, (Submodule x, string p) => x.Path == p);
						if (submodule2 != null)
						{
							list.Add(new SubmoduleChangedFile(submodule2, filepath, StatusType.None, StatusType.Modified));
						}
						else
						{
							list.Add(new ChangedFile(filepath, StatusType.None, StatusType.Modified));
						}
						break;
					}
					case ChangeType.Added:
						list.Add(new ChangedFile(filepath, StatusType.None, StatusType.Added));
						break;
					case ChangeType.Deleted:
						list.Add(new ChangedFile(filepath, StatusType.None, StatusType.Deleted));
						break;
					case ChangeType.TypeChanged:
						list.Add(new ChangedFile(filepath, StatusType.None, StatusType.TypeChanged));
						break;
					default:
						Log.Error("Unhandled unstaged case: '" + text + "'");
						break;
					}
				}
				if (!staged.HasValue)
				{
					continue;
				}
				switch (staged)
				{
				case ChangeType.Modified:
				{
					Submodule submodule3 = submodules.FirstItem(filepath, (Submodule x, string p) => x.Path == p);
					if (submodule3 != null)
					{
						list.Add(new SubmoduleChangedFile(submodule3, filepath, StatusType.Modified));
					}
					else
					{
						list.Add(new ChangedFile(filepath, StatusType.Modified));
					}
					break;
				}
				case ChangeType.Deleted:
					list.Add(new ChangedFile(filepath, StatusType.Deleted));
					break;
				case ChangeType.Added:
					list.Add(new ChangedFile(filepath, StatusType.Added));
					break;
				case ChangeType.Copied:
			{
				if (i + 1 >= array.Length || string.IsNullOrEmpty(array[i + 1]))
				{
					Log.Warn("Truncated status output for copied file: '" + text + "'");
					break;
				}
				string oldPath2 = array[i + 1];
				list.Add(new ChangedFile(filepath, StatusType.Copied, StatusType.None, oldPath2));
				i++;
				break;
			}
			case ChangeType.Renamed:
			{
				if (i + 1 >= array.Length || string.IsNullOrEmpty(array[i + 1]))
				{
					Log.Warn("Truncated status output for renamed file: '" + text + "'");
					break;
				}
				string oldPath = array[i + 1];
				list.Add(new ChangedFile(filepath, StatusType.Renamed, StatusType.None, oldPath));
				i++;
				break;
			}
				case ChangeType.TypeChanged:
					list.Add(new ChangedFile(filepath, StatusType.TypeChanged));
					break;
				default:
					Log.Error("Unhandled staged case: '" + text + "'");
					break;
				}
			}
			list.Sort(ChangedFile.Comparer);
			return GitCommandResult<ChangedFilesCollection>.Success(new ChangedFilesCollection(num, list.ToArray()));
		}

		public GitCommandResult<ChangedFilesCollection> Execute(GitModule gitModule, string[] paths, [Null] Submodule[] submodules, bool includeIgnoredFiles)
		{
			submodules = submodules ?? new Submodule[0];
			GitCommand gitCommand = CreateReliableStatusCommand("status", "--porcelain", "--renames", "--untracked-files=all", "-z");
			if (includeIgnoredFiles)
			{
				gitCommand.Add("--ignored");
			}
			gitCommand.Add("--");
			foreach (string input in paths)
			{
				gitCommand.Add(input.Quotify());
			}
			GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
			if (!gitRequestResult.Success)
			{
				return GitCommandResult<ChangedFilesCollection>.Failure(gitRequestResult.ToGitCommandError());
			}
			string[] array = gitRequestResult.Stdout.Split(Consts.Chars.Nul);
			List<ChangedFile> list = new List<ChangedFile>(128);
			int num = 0;
			for (int j = 0; j < array.Length; j++)
			{
				string text = array[j];
				if (text.Length <= 0 || !SplitStatusLine(array[j], out var status, out var workingDirectoryStatus, out var staged, out var unstaged, out var filepath))
				{
					continue;
				}
				num++;
				if (staged.HasValue && unstaged.HasValue)
				{
					if (staged == ChangeType.Untracked && unstaged == ChangeType.Untracked)
					{
						list.Add(new ChangedFile(filepath, status, workingDirectoryStatus));
						continue;
					}
					if (staged == ChangeType.Ignored && unstaged == ChangeType.Ignored)
					{
						list.Add(new ChangedFile(filepath, status, workingDirectoryStatus));
						continue;
					}
					if (staged == ChangeType.Unmerged || unstaged == ChangeType.Unmerged || (staged == ChangeType.Added && unstaged == ChangeType.Added) || (staged == ChangeType.Deleted && unstaged == ChangeType.Deleted))
					{
						list.Add(new ChangedFile(filepath, status, workingDirectoryStatus));
						continue;
					}
				}
				if (unstaged.HasValue)
				{
					switch (unstaged)
					{
					case ChangeType.Modified:
					{
						Submodule submodule = submodules.FirstItem(filepath, (Submodule x, string p) => x.Path == p);
						if (submodule != null)
						{
							list.Add(new SubmoduleChangedFile(submodule, filepath, StatusType.None, StatusType.Modified));
						}
						else
						{
							list.Add(new ChangedFile(filepath, StatusType.None, StatusType.Modified, ChangeType.Modified, staged: false));
						}
						break;
					}
					case ChangeType.Deleted:
						list.Add(new ChangedFile(filepath, StatusType.None, StatusType.Deleted, ChangeType.Deleted, staged: false));
						break;
					default:
						Log.Error("Unhandled unstaged case: '" + text + "'");
						break;
					}
				}
				if (!staged.HasValue)
				{
					continue;
				}
				switch (staged)
				{
				case ChangeType.Modified:
				{
					Submodule submodule2 = submodules.FirstItem(filepath, (Submodule x, string p) => x.Path == p);
					if (submodule2 != null)
					{
						list.Add(new SubmoduleChangedFile(submodule2, filepath, StatusType.Modified));
					}
					else
					{
						list.Add(new ChangedFile(filepath, StatusType.Modified));
					}
					break;
				}
				case ChangeType.Deleted:
					list.Add(new ChangedFile(filepath, StatusType.Deleted));
					break;
				case ChangeType.Added:
					list.Add(new ChangedFile(filepath, StatusType.Added));
					break;
				case ChangeType.Copied:
			{
				if (j + 1 >= array.Length || string.IsNullOrEmpty(array[j + 1]))
				{
					Log.Warn("Truncated status output for copied file: '" + text + "'");
					break;
				}
				string oldPath2 = array[j + 1];
				list.Add(new ChangedFile(filepath, StatusType.Copied, StatusType.None, oldPath2));
				j++;
				break;
			}
			case ChangeType.Renamed:
			{
				if (j + 1 >= array.Length || string.IsNullOrEmpty(array[j + 1]))
				{
					Log.Warn("Truncated status output for renamed file: '" + text + "'");
					break;
				}
				string oldPath = array[j + 1];
				list.Add(new ChangedFile(filepath, StatusType.Renamed, StatusType.None, oldPath));
				j++;
				break;
			}
				default:
					Log.Error("Unhandled staged case: '" + text + "'");
					break;
				}
			}
			list.Sort(ChangedFile.Comparer);
			return GitCommandResult<ChangedFilesCollection>.Success(new ChangedFilesCollection(num, list.ToArray()));
		}

		private static bool SplitStatusLine(string line, out StatusType status, out StatusType workingDirectoryStatus, out ChangeType? staged, out ChangeType? unstaged, out string filepath)
		{
			if (line.Length < 4)
			{
				Log.Error("Invalid format of status string: '" + line + "'");
				status = StatusType.None;
				workingDirectoryStatus = StatusType.None;
				staged = (unstaged = null);
				filepath = null;
				return false;
			}
			if (!(line.Substring(2, 1) == " "))
			{
				Log.Error("Invalid format of status string: '" + line + "'");
				status = StatusType.None;
				workingDirectoryStatus = StatusType.None;
				staged = (unstaged = null);
				filepath = null;
				return false;
			}
			string text = line.Substring(0, 1);
			string text2 = line.Substring(1, 1);
			status = StatusTypeHelper.Parse(text[0]);
			workingDirectoryStatus = StatusTypeHelper.Parse(text2[0]);
			staged = ParseChangeType(text);
			unstaged = ParseChangeType(text2);
			filepath = line.Substring(3);
			return true;
		}

		private static ChangeType? ParseChangeType(string changeTypeChar)
		{
			return changeTypeChar switch
			{
				"A" => ChangeType.Added, 
				"C" => ChangeType.Copied, 
				"D" => ChangeType.Deleted, 
				"!" => ChangeType.Ignored, 
				"M" => ChangeType.Modified, 
				"R" => ChangeType.Renamed, 
				"T" => ChangeType.TypeChanged, 
				"U" => ChangeType.Unmerged, 
				"?" => ChangeType.Untracked, 
				"X" => ChangeType.Unknown, 
				_ => null, 
			};
		}

		private static GitCommand CreateReliableStatusCommand(params string[] args)
		{
			GitCommand command = new GitCommand(
				"-c", "core.fsmonitor=false",
				"-c", "core.untrackedCache=false",
				"-c", "core.checkStat=default",
				"--no-optional-locks");
			command.AddRange(args);
			return command;
		}

	}
}
