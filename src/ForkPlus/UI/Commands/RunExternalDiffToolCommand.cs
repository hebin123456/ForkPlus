using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Commands
{
	public class RunExternalDiffToolCommand : IUICommand, IForkPlusCommand
	{
		public abstract class DiffTarget
		{
			public class Revision : DiffTarget
			{
				private readonly Sha _sha;

				private readonly Sha? _otherSha;

				public Revision(Sha sha, Sha? otherSha, ChangedFile changedFile)
					: base(changedFile)
				{
					_sha = sha;
					_otherSha = otherSha;
				}

				public override GitCommandResult<(string, string)> DumpChanges(GitModule gitModule, TempFileManager tempFileManager, JobMonitor monitor)
				{
					GitCommandResult<string> gitCommandResult = new RestoreFileGitCommand().Execute(gitModule, _otherSha?.ToString() ?? (_sha.ToString() + "~"), base.ChangedFile.OldPath ?? base.ChangedFile.Path, TempDestination(_sha, _otherSha), monitor);
					if (!gitCommandResult.Succeeded)
					{
						return GitCommandResult<(string, string)>.Failure(gitCommandResult.Error);
					}
					tempFileManager.AddFilePath(gitCommandResult.Result);
					GitCommandResult<string> gitCommandResult2 = new RestoreFileGitCommand().Execute(gitModule, _sha.ToString(), base.ChangedFile.Path, TempDestination(_sha.ToAbbreviatedString()), monitor);
					if (!gitCommandResult2.Succeeded)
					{
						return GitCommandResult<(string, string)>.Failure(gitCommandResult2.Error);
					}
					tempFileManager.AddFilePath(gitCommandResult2.Result);
					return GitCommandResult<(string, string)>.Success((gitCommandResult.Result, gitCommandResult2.Result));
				}
			}

			public class WorkingDirectory : DiffTarget
			{
				private readonly bool _amend;

				public WorkingDirectory(ChangedFile changedFile, bool amend)
					: base(changedFile)
				{
					_amend = amend;
				}

				public override GitCommandResult<(string, string)> DumpChanges(GitModule gitModule, TempFileManager tempFileManager, JobMonitor monitor)
				{
					string result;
					string item;
					if (base.ChangedFile.Staged)
					{
						if (_amend)
						{
							GitCommandResult<string> gitCommandResult = new RestoreFileGitCommand().Execute(gitModule, "HEAD~", base.ChangedFile.OldPath ?? base.ChangedFile.Path, TempDestination("HEAD~"), monitor);
							if (!gitCommandResult.Succeeded)
							{
								return GitCommandResult<(string, string)>.Failure(gitCommandResult.Error);
							}
							tempFileManager.AddFilePath(gitCommandResult.Result);
							result = gitCommandResult.Result;
						}
						else
						{
							GitCommandResult<string> gitCommandResult2 = new RestoreFileGitCommand().Execute(gitModule, "HEAD", base.ChangedFile.OldPath ?? base.ChangedFile.Path, TempDestination("HEAD"), monitor);
							if (!gitCommandResult2.Succeeded)
							{
								return GitCommandResult<(string, string)>.Failure(gitCommandResult2.Error);
							}
							tempFileManager.AddFilePath(gitCommandResult2.Result);
							result = gitCommandResult2.Result;
						}
						GitCommandResult<string> gitCommandResult3 = new RestoreFileGitCommand().Execute(gitModule, "", base.ChangedFile.Path, TempDestination("staged"), monitor);
						if (!gitCommandResult3.Succeeded)
						{
							return GitCommandResult<(string, string)>.Failure(gitCommandResult3.Error);
						}
						tempFileManager.AddFilePath(gitCommandResult3.Result);
						item = gitCommandResult3.Result;
					}
					else
					{
						GitCommandResult<string> gitCommandResult4 = new RestoreFileGitCommand().Execute(gitModule, "", base.ChangedFile.OldPath ?? base.ChangedFile.Path, TempDestination("staged"), monitor);
						if (!gitCommandResult4.Succeeded)
						{
							return GitCommandResult<(string, string)>.Failure(gitCommandResult4.Error);
						}
						tempFileManager.AddFilePath(gitCommandResult4.Result);
						result = gitCommandResult4.Result;
						item = gitModule.MakePath(base.ChangedFile.Path);
					}
					return GitCommandResult<(string, string)>.Success((result, item));
				}
			}

			public class WorkingDirectorySha : DiffTarget
			{
				private readonly string _sha;

				public WorkingDirectorySha(ChangedFile changedFile, string sha)
					: base(changedFile)
				{
					_sha = sha;
				}

				public override GitCommandResult<(string, string)> DumpChanges(GitModule gitModule, TempFileManager tempFileManager, JobMonitor monitor)
				{
					GitCommandResult<string> gitCommandResult = new RestoreFileGitCommand().Execute(gitModule, _sha, base.ChangedFile.Path, TempDestination(_sha), monitor);
					if (!gitCommandResult.Succeeded)
					{
						return GitCommandResult<(string, string)>.Failure(gitCommandResult.Error);
					}
					tempFileManager.AddFilePath(gitCommandResult.Result);
					return GitCommandResult<(string, string)>.Success((gitCommandResult.Result, gitModule.MakePath(base.ChangedFile.Path)));
				}
			}

			public ChangedFile ChangedFile { get; }

			public DiffTarget(ChangedFile changedFile)
			{
				ChangedFile = changedFile;
			}

			public abstract GitCommandResult<(string, string)> DumpChanges(GitModule gitModule, TempFileManager tempFileManager, JobMonitor monitor);

			protected static string TempDestination(string filename)
			{
				return TempFileManager.MakeFilePath(filename);
			}

			protected static string TempDestination(Sha sha, Sha? otherSha)
			{
				if (otherSha.HasValue)
				{
					return TempFileManager.MakeFilePath(otherSha.Value.ToAbbreviatedString());
				}
				return TempFileManager.MakeFilePath(sha.ToAbbreviatedString() + "~");
			}
		}

		public string Title => "External Diff";

		public KeyGesture Shortcut { get; } = new KeyGesture(Key.D, ModifierKeys.Control);


		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, DiffTarget diffTarget, ExternalTool diffTool)
		{
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			TempFileManager tempFileManager = repositoryUserControl.TempFileManager;
			if (tempFileManager == null)
			{
				return;
			}
			string externalDiffToolPath = Environment.ExpandEnvironmentVariables(diffTool.Path);
			if (!File.Exists(externalDiffToolPath))
			{
				Log.Error("Cannot find external diff tool at '" + externalDiffToolPath + "'");
				new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find external diff tool at '{0}'", externalDiffToolPath)).ShowDialog();
				return;
			}
			repositoryUserControl.JobQueue.Add(PreferencesLocalization.Current("External diff"), delegate(JobMonitor monitor)
			{
				GitCommandResult<(string, string)> dumpResult = diffTarget.DumpChanges(gitModule, tempFileManager, monitor);
				if (!dumpResult.Succeeded)
				{
					Log.Error(dumpResult.Error.FriendlyDescription);
					repositoryUserControl.Dispatcher.Invoke(delegate
					{
						if (!monitor.IsCanceled)
						{
							new ErrorWindow(repositoryUserControl, dumpResult.Error).ShowDialog();
						}
					});
					return;
				}
				string text = string.Join(" ", diffTool.Arguments.Map((string x) => x.Replace("$REMOTE", dumpResult.Result.Item1).Replace("$LOCAL", dumpResult.Result.Item2)));
				Process process = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = externalDiffToolPath,
						Arguments = text
					}
				};
				Log.Info("Running '" + externalDiffToolPath + " " + text + "'");
				monitor.AppendOutputLine("$ " + externalDiffToolPath + " " + text);
				try
				{
					process.Start();
				}
				catch (Exception ex)
				{
					Log.Error("Failed to start external diff tool '" + externalDiffToolPath + " " + text + "'", ex);
					new ErrorWindow($"Cannot run '{externalDiffToolPath}'.\n{ex}").ShowDialog();
				}
			});
		}
	}
}
