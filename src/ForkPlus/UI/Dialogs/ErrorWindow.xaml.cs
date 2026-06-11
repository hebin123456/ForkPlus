using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.UI.Controls.Editor;
using ForkPlus.UI.UserControls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class ErrorWindow : ForkPlusDialogWindow
	{
		[Null]
		private RepositoryUserControl _repositoryUserControl;

		[Null]
		private GitCommandError GitCommandError { get; }

		public ErrorWindow()
		{
			InitializeComponent();
			base.DialogTitle = "Git Error";
			base.DialogDescription = "An unexpected error occured while performing the git request";
			base.CancelButtonTitle = "Close";
			base.ShowSubmitButton = false;
			base.ShowWarningIcon = true;
			MessageEditor.Options.EnableHyperlinks = true;
			MessageEditor.Options.RequireControlModifierForHyperlinkClick = false;
			MessageEditor.WordWrap = true;
			MessageEditor.TextArea.TextView.LineTransformers.Add(new GitOutputColorizer());
			RefreshTheme();
		}

		public ErrorWindow(string message)
			: this()
		{
			MessageEditor.Text = message;
			MessageEditor.ScrollToEnd();
		}

		public ErrorWindow([Null] RepositoryUserControl repositoryUserControl, GitCommandError gitCommandError)
			: this()
		{
			_repositoryUserControl = repositoryUserControl;
			GitCommandError = gitCommandError;
			string text;
			if (gitCommandError is GitCommandError.GitError gitError)
			{
				text = gitError.FriendlyDescription;
				if (gitCommandError is GitCommandError.AutomaticMergeFailed)
				{
					base.DialogTitle = Translate("Merge Conflict");
					base.DialogDescription = Translate("Automatic merge failed. Fix conflicts and then commit the result");
					base.ShowWarningIcon = false;
				}
				else if (gitCommandError is GitCommandError.TagMismatch)
				{
					FirstButton.Content = Translate("Force Fetch");
					FirstButton.Show();
				}
				else if (gitCommandError is GitCommandError.RepositoryIsLocked)
				{
					FirstButton.Content = Translate("Remove .git/index.lock");
					FirstButton.Show();
				}
				else if (gitCommandError is GitCommandError.LfsFileIsLocked lfsFileIsLocked)
				{
					int count = lfsFileIsLocked.Paths.Count;
					string content = ((count > 1) ? string.Format(Translate("Force Unlock {0} Files"), count) : Translate("Force Unlock"));
					FirstButton.Content = content;
					FirstButton.Show();
				}
				else if (gitCommandError is GitCommandError.PatchDoesNotApply)
				{
					text += "\n" + Translate("fork: try to disable 'ignore whitespaces'");
				}
				else
				{
					GitCommandError.AuthenticationFailed authenticationFailed = GitCommandError.AuthenticationFailed.Test(gitError.Stderr);
					if (authenticationFailed != null)
					{
						switch (authenticationFailed.ErrorKind)
						{
						case GitCommandError.AuthenticationFailed.Kind.Generic:
							FirstButton.Content = Translate("Credential Manager");
							FirstButton.Show();
							break;
						case GitCommandError.AuthenticationFailed.Kind.GitHubConnectionError:
						{
							text = gitError.FriendlyDescription;
							int count2 = AccountManager.Current.Accounts.Filter((Account x) => x.ServiceType == RemoteType.Github).Count;
							if (count2 > 0)
							{
								text += "\n" + Translate("fork: * ensure Fork has access to your organization") + "\nfork:   https://github.com/settings/connections/applications/debde513eaa447b74d51";
							}
							if (count2 > 1)
							{
								text += "\n" + Translate("fork: * ensure Fork uses correct account for the remote") + "\nfork:   (right click on the remote on sidebar and select Edit)";
							}
							break;
						}
						}
					}
					else if (gitCommandError is GitCommandError.UnsafeRepository)
					{
						FirstButton.Content = Translate("Mark repository as safe");
						FirstButton.Show();
					}
					else if (gitCommandError is GitCommandError.MergeUnrelatedHistory)
					{
						FirstButton.Content = Translate("Merge unrelated history");
						FirstButton.Show();
					}
				}
			}
			else if (gitCommandError is GitCommandError.CommitFailed commitFailed)
			{
				text = commitFailed.Stderr;
				if (ContainsHooks(repositoryUserControl.GitModule, repositoryUserControl.RepositoryData.GitConfig))
				{
					FirstButton.Content = Translate("Skip pre-commit hooks and commit");
					FirstButton.Show();
				}
			}
			else
			{
				text = gitCommandError.FriendlyDescription;
			}
			MessageEditor.Text = text;
			MessageEditor.ScrollToEnd();
		}

		private void FirstButton_Click(object sender, RoutedEventArgs e)
		{
			if (GitCommandError is GitCommandError.GitError gitError)
			{
				if (GitCommandError is GitCommandError.TagMismatch tagMismatch)
				{
					ForceFetch(tagMismatch.Remote);
				}
				else if (GitCommandError is GitCommandError.RepositoryIsLocked)
				{
					RemoveLockIndexFile();
				}
				else if (GitCommandError is GitCommandError.LfsFileIsLocked lfsFileIsLocked)
				{
					ForceUnlock(lfsFileIsLocked.Paths);
				}
				else if (GitCommandError is GitCommandError.UnsafeRepository unsafeRepositoryError)
				{
					AddRepositoryToSafeDirectoriesList(unsafeRepositoryError);
				}
				else if (GitCommandError is GitCommandError.MergeUnrelatedHistory mergeUnrelatedHistory)
				{
					MergeUnrelatedHistory(mergeUnrelatedHistory.Source, mergeUnrelatedHistory.MergeType);
				}
				else if (GitCommandError.AuthenticationFailed.Test(gitError.Stderr) != null)
				{
					OpenCredentialManager();
				}
			}
			else if (GitCommandError is GitCommandError.CommitFailed commitFailed)
			{
				CommitWithoutHooksAndClose(commitFailed.Amend, commitFailed.CommitAndPush, commitFailed.Message);
			}
		}

		private void RemoveLockIndexFile()
		{
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				try
				{
					File.Delete(Path.Combine(gitModule.GitDir(), "index.lock"));
				}
				catch (Exception ex)
				{
					Log.Warn("Failed to delete 'index.lock' file", ex);
				}
				Close();
			}
		}

		private void RefreshTheme()
		{
			MessageEditor.TextArea.TextView.LinkTextForegroundBrush = Application.Current.TryFindResource("CodeEditorLinkForeground") as Brush;
		}

		private static void OpenCredentialManager()
		{
			try
			{
				using Process process = new Process();
				process.StartInfo = new ProcessStartInfo
				{
					FileName = "rundll32.exe",
					Arguments = "keymgr.dll, KRShowKeyMgr",
					UseShellExecute = false,
					ErrorDialog = false
				};
				process.Start();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open Windows Credential Manager", ex);
			}
		}

		private void AddRepositoryToSafeDirectoriesList(GitCommandError.UnsafeRepository unsafeRepositoryError)
		{
			GitCommandResult gitCommandResult = new AddRepositoryToSafeDirectoriesListGitCommand().Execute(unsafeRepositoryError.ProposedRepositoryPath);
			if (!gitCommandResult.Succeeded)
			{
				new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
			}
			if (MainWindow.Instance.TabManager.OpenRepository(unsafeRepositoryError.RepositoryPath))
			{
				MainWindow.ActiveRepositoryUserControl?.InvalidateAndRefresh(SubDomain.All);
			}
			Close();
		}

		private void CommitWithoutHooksAndClose(bool amend, bool commitAndPush, string message)
		{
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			string name = Translate(amend ? "Amend" : "Commit");
			repositoryUserControl.JobQueue.Add(name, delegate(JobMonitor monitor)
			{
				GitCommandResult gitResult = new CommitGitCommand().Execute(gitModule, message, amend, commitAndPush, monitor, noVerify: true);
				repositoryUserControl.Dispatcher.Async(delegate
				{
					monitor.Update(0.0, Translate("Done"));
					if (gitResult.Succeeded)
					{
						repositoryUserControl.UncheckAmendCheckBox();
						repositoryUserControl.EraseSavedCommitMessage();
						if (commitAndPush)
						{
							MainWindow.Commands.QuickPush.Execute(repositoryUserControl);
						}
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Status | SubDomain.Revisions | SubDomain.Head | SubDomain.References);
					Close(gitResult);
				});
			}, JobFlags.Default, showMessageWhenDone: false);
		}

		private void ForceFetch(Remote remote)
		{
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			bool tags = true;
			bool force = true;
			repositoryUserControl.JobQueue.Add(Translate("Force Fetch"), delegate(JobMonitor monitor)
			{
				GitCommandResult fetchResult = new FetchGitCommand().Execute(gitModule, remote, fetchAllRemotes: false, monitor, noPrompt: false, tags, force);
				base.Dispatcher.Async(delegate
				{
					if (!fetchResult.Succeeded && !(fetchResult.Error is GitCommandError.Cancelled))
					{
						new ErrorWindow(repositoryUserControl, fetchResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.Revisions | SubDomain.References);
				});
			});
			Close();
		}

		private void ForceUnlock(IReadOnlyList<string> paths)
		{
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			DisableEditableControls();
			bool force = true;
			repositoryUserControl.JobQueue.Add(Translate("Force LFS Unlock"), delegate(JobMonitor monitor)
			{
				GitCommandResult unlockResult = new GitLfsUnlockGitCommand().Execute(gitModule, paths, monitor, force);
				base.Dispatcher.Async(delegate
				{
					EnableEditableControls();
					SetStatus(ForkPlusDialogStatus.None, "");
					if (!unlockResult.Succeeded && !(unlockResult.Error is GitCommandError.Cancelled))
					{
						new ErrorWindow(repositoryUserControl, unlockResult.Error).ShowDialog();
					}
					Close();
				});
			});
		}

		private void MergeUnrelatedHistory(ForkPlus.Git.Reference source, MergeType mergeType)
		{
			RepositoryUserControl repositoryUserControl = _repositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			bool mergeUnrelatedHistory = true;
			repositoryUserControl.JobQueue.Add(string.Format(Translate("Merge unrelated '{0}'"), source.Name), delegate(JobMonitor monitor)
			{
				GitCommandResult mergeUnrelatedHistoryResult = new MergeGitCommand().Execute(gitModule, source, mergeType, repositoryData.References, monitor, mergeUnrelatedHistory);
				base.Dispatcher.Async(delegate
				{
					if (!mergeUnrelatedHistoryResult.Succeeded)
					{
						new ErrorWindow(repositoryUserControl, mergeUnrelatedHistoryResult.Error).ShowDialog();
					}
					repositoryUserControl.InvalidateAndRefresh(SubDomain.All);
				});
			});
			Close();
		}

		private static bool ContainsHooks(GitModule gitModule, GitConfig gitConfig)
		{
			string hooksPath = GetGitConfigHooksPath(gitModule, gitConfig) ?? gitModule.HooksDirectoryPath();
			if (HookExists(hooksPath, "pre-commit") || HookExists(hooksPath, "commit-msg") || HookExists(hooksPath, "post-commit"))
			{
				return true;
			}
			return false;
		}

		[Null]
		private static string GetGitConfigHooksPath(GitModule gitModule, GitConfig gitConfig)
		{
			GitConfig.Section[] sections = gitConfig.Sections;
			for (int i = 0; i < sections.Length; i++)
			{
				GitConfig.Section section = sections[i];
				if (section.Name != "core")
				{
					continue;
				}
				GitConfig.Variable[] variables = section.Variables;
				for (int j = 0; j < variables.Length; j++)
				{
					GitConfig.Variable variable = variables[j];
					if (!(variable.Name != "hooksPath"))
					{
						string value = variable.Value;
						if (!Path.IsPathRooted(value))
						{
							return PathHelper.Normalize(Path.GetFullPath(gitModule.MakePath(value)));
						}
						return PathHelper.Normalize(value);
					}
				}
			}
			return null;
		}

		private static bool HookExists(string hooksPath, string hookName)
		{
			return File.Exists(Path.Combine(hooksPath, hookName));
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
