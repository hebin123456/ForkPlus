using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class NotificationBarUserControl : UserControl, INotifyPropertyChanged
	{
		private bool _showingGitignoreSuggestion;

		private bool _isControlVisible;

		private RepositoryUserControl _repositoryUserControl { get; set; }

		public bool IsControlVisible
		{
			get
			{
				return _isControlVisible;
			}
			set
			{
				if (value != _isControlVisible)
				{
					_isControlVisible = value;
					NotifyPropertyChanged("IsControlVisible");
				}
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public NotificationBarUserControl()
		{
			InitializeComponent();
			Button1.Click += Button1_Click;
			Button2.Click += Button2_Click;
			Button3.Click += Button3_Click;
			AbortButton.Click += AbortButton_Click;
			AbortButton.Content = Translate("Abort");
		}

		public void Initialize(RepositoryUserControl repositoryUserControl)
		{
			_repositoryUserControl = repositoryUserControl;
		}

		public void Refresh()
		{
			_showingGitignoreSuggestion = false;
			Button1.Collapse();
			Button2.Collapse();
			Button3.Collapse();
			AbortButton.Show();
			AbortButton.Content = Translate("Abort");
			bool show = _repositoryUserControl.ViewMode == RepositoryViewMode.RevisionViewMode;
			RepositoryState repositoryState = _repositoryUserControl.RepositoryStatus?.RepositoryState;
			if (repositoryState is RepositoryState.OK)
			{
				UpdateGitignoreSuggestion();
				return;
			}
			RepositoryState.MergeInProgress mergeInProgress = repositoryState as RepositoryState.MergeInProgress;
			if (mergeInProgress != null)
			{
				string arg = Translate(mergeInProgress.UnmergedFiles.Length == 1 ? "conflict" : "conflicts");
				List<Inline> list = new List<Inline>(5);
				list.Add(new Run(Translate("Merging branch '")));
				list.Add(new CommandHyperlink(_repositoryUserControl, mergeInProgress.Remote.Sha, mergeInProgress.Remote.Name, delegate
				{
					_repositoryUserControl.SelectRevision(mergeInProgress.Remote.Sha);
				}));
				list.Add(new Run(Translate("' into '")));
				list.Add(new CommandHyperlink(_repositoryUserControl, mergeInProgress.Local.Sha, mergeInProgress.Local.Name, delegate
				{
					_repositoryUserControl.SelectRevision(mergeInProgress.Local.Sha);
				}));
				if (mergeInProgress.UnmergedFiles.Length != 0)
				{
					list.Add(new Run(string.Format(Translate("'. Fix {0} {1} and then continue."), mergeInProgress.UnmergedFiles.Length, arg)));
				}
				else
				{
					list.Add(new Run(Translate("'. All conflicts fixed.")));
				}
				ShowNotificationBar(list);
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			RepositoryState.RebaseInProgress rebaseInProgress = repositoryState as RepositoryState.RebaseInProgress;
			Sha? activeSha2;
			if (rebaseInProgress != null)
			{
				List<Inline> list2 = new List<Inline>(5);
				list2.Add(new Run(Translate("Rebasing '")));
				list2.Add(new CommandHyperlink(_repositoryUserControl, rebaseInProgress.Remote.Sha, rebaseInProgress.Remote.Name, delegate
				{
					_repositoryUserControl.SelectRevision(rebaseInProgress.Remote.Sha);
				}));
				list2.Add(new Run(Translate("' → '")));
				list2.Add(new CommandHyperlink(_repositoryUserControl, rebaseInProgress.Local.Sha, rebaseInProgress.Local.Name, delegate
				{
					_repositoryUserControl.SelectRevision(rebaseInProgress.Local.Sha);
				}));
				list2.Add(new Run(string.Format(Translate("' (rebased {0}/{1} commits)"), rebaseInProgress.Done, rebaseInProgress.Total)));
				List<Inline> list3 = new List<Inline>(4);
				if (rebaseInProgress.UnmergedFiles.Length != 0)
				{
					string arg2 = Translate(rebaseInProgress.UnmergedFiles.Length == 1 ? "conflict" : "conflicts");
					list3.Add(new Run(string.Format(Translate("Fix {0} {1} with '"), rebaseInProgress.UnmergedFiles.Length, arg2)));
					activeSha2 = rebaseInProgress.ActiveSha;
					if (activeSha2.HasValue)
					{
						Sha activeSha = activeSha2.GetValueOrDefault();
						list3.Add(new CommandHyperlink(_repositoryUserControl, activeSha, activeSha.ToAbbreviatedString(), delegate
						{
							_repositoryUserControl.SelectRevision(activeSha);
						}));
					}
					list3.Add(new Run(Translate("' and then continue.")));
				}
				string amendSha = rebaseInProgress.AmendSha;
				if (amendSha != null)
				{
					list3.Add(new Run(string.Format(Translate(" Amending '{0}'."), amendSha.Abbreviated())));
				}
				ShowRebaseNotificationBar(list2, rebaseInProgress.Done + 1, rebaseInProgress.Total, list3);
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			RepositoryState.CherryPickInProgress cherryPickInProgress = repositoryState as RepositoryState.CherryPickInProgress;
			if (cherryPickInProgress != null)
			{
				List<Inline> list4 = new List<Inline>(3);
				string arg3 = Translate(cherryPickInProgress.UnmergedFiles.Length == 1 ? "conflict" : "conflicts");
				list4.Add(new Run(Translate("Cherry-picking commit '")));
				list4.Add(new CommandHyperlink(_repositoryUserControl, cherryPickInProgress.CherryPickHead.Sha, cherryPickInProgress.CherryPickHead.Sha.ToAbbreviatedString(), delegate
				{
					_repositoryUserControl.SelectRevision(cherryPickInProgress.CherryPickHead.Sha);
				}));
				if (cherryPickInProgress.UnmergedFiles.Length != 0)
				{
					list4.Add(new Run(string.Format(Translate("'. Fix {0} {1} and then continue."), cherryPickInProgress.UnmergedFiles.Length, arg3)));
				}
				else
				{
					list4.Add(new Run(Translate("'. All conflicts fixed.")));
				}
				ShowNotificationBar(list4);
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			if (repositoryState is RepositoryState.SequencerInProgress)
			{
				ShowNotificationBar("Cherry-pick in progress");
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			RepositoryState.RevertInProgress revertInProgress = repositoryState as RepositoryState.RevertInProgress;
			if (revertInProgress != null)
			{
				string arg4 = Translate(revertInProgress.UnmergedFiles.Length == 1 ? "conflict" : "conflicts");
				List<Inline> list5 = new List<Inline>(3);
				list5.Add(new Run(Translate("Reverting commit '")));
				list5.Add(new CommandHyperlink(_repositoryUserControl, revertInProgress.RevertHead, revertInProgress.RevertHead.ToAbbreviatedString(), delegate
				{
					_repositoryUserControl.SelectRevision(revertInProgress.RevertHead);
				}));
				if (revertInProgress.UnmergedFiles.Length != 0)
				{
					list5.Add(new Run(string.Format(Translate("'. Fix {0} {1} and then continue."), revertInProgress.UnmergedFiles.Length, arg4)));
				}
				else
				{
					list5.Add(new Run(Translate("'. All conflicts fixed.")));
				}
				ShowNotificationBar(list5);
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			if (repositoryState is RepositoryState.SquashInProgress squashInProgress)
			{
				string arg5 = Translate(squashInProgress.UnmergedFiles.Length == 1 ? "conflict" : "conflicts");
				string message = ((squashInProgress.UnmergedFiles.Length != 0) ? string.Format(Translate("Squashing commits. Fix {0} {1} and continue."), squashInProgress.UnmergedFiles.Length, arg5) : Translate("Squashing commits"));
				ShowNotificationBar(message);
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			if (repositoryState is RepositoryState.AmInProgress amInProgress)
			{
				List<Inline> list6 = new List<Inline>(2);
				list6.Add(new Run(Translate("Applying a series of patches (AM) in progress")));
				list6.Add(new Run(string.Format(Translate(" (applied {0}/{1} commits)"), amInProgress.Done, amInProgress.Total)));
				List<Inline> list7 = new List<Inline>(2);
				if (amInProgress.UnmergedFiles.Length != 0)
				{
					string arg6 = Translate(amInProgress.UnmergedFiles.Length == 1 ? "conflict" : "conflicts");
					list7.Add(new Run(string.Format(Translate("Fix {0} {1} and then continue."), amInProgress.UnmergedFiles.Length, arg6)));
				}
				ShowRebaseNotificationBar(list6, amInProgress.Done + 1, amInProgress.Total, list7);
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			if (repositoryState is RepositoryState.UnmergedIndex unmergedIndex)
			{
				string arg7 = Translate(unmergedIndex.UnmergedFiles.Length == 1 ? "file" : "files");
				string message2 = string.Format(Translate("Working directory contains {0} unmerged {1}."), unmergedIndex.UnmergedFiles.Length, arg7);
				ShowNotificationBar(message2);
				UpdateButton(Button1, "Resolve", show);
				return;
			}
			RepositoryState.BisectInProgress bisectInProgress = repositoryState as RepositoryState.BisectInProgress;
			if (bisectInProgress == null)
			{
				return;
			}
			List<Inline> list8 = new List<Inline>();
			list8.Add(new Run(Translate("Bisecting, started from '")));
			list8.Add(new CommandHyperlink(_repositoryUserControl, bisectInProgress.Start.Sha, bisectInProgress.Start.Name, delegate
			{
				_repositoryUserControl.SelectRevision(bisectInProgress.Start.Sha);
			}));
			activeSha2 = bisectInProgress.Sha;
			if (activeSha2.HasValue)
			{
				Sha sha = activeSha2.GetValueOrDefault();
				list8.Add(new Run(Translate("'. Is '")));
				list8.Add(new CommandHyperlink(_repositoryUserControl, sha, sha.ToAbbreviatedString(), delegate
				{
					_repositoryUserControl.SelectRevision(sha);
				}));
				list8.Add(new Run(Translate("' good or bad?")));
			}
			else
			{
				list8.Add(new Run(Translate("'. Mark current commit as good or bad and checkout another one")));
			}
			ShowNotificationBar(list8);
			UpdateButton(Button1, "Good");
			UpdateButton(Button2, "Bad");
			UpdateButton(Button3, "Skip");
		}

		private void Button1_Click(object sender, RoutedEventArgs e)
		{
			if (_showingGitignoreSuggestion)
			{
				ShowGitignoreTemplateWindow();
				return;
			}
			RepositoryState repositoryState = _repositoryUserControl.RepositoryStatus?.RepositoryState;
			if (repositoryState is RepositoryState.MergeInProgress || repositoryState is RepositoryState.RebaseInProgress || repositoryState is RepositoryState.CherryPickInProgress || repositoryState is RepositoryState.RevertInProgress || repositoryState is RepositoryState.SequencerInProgress || repositoryState is RepositoryState.SquashInProgress || repositoryState is RepositoryState.AmInProgress || repositoryState is RepositoryState.UnmergedIndex)
			{
				MainWindow.Commands.ActivateCommitView.Execute();
			}
			else if (repositoryState is RepositoryState.BisectInProgress)
			{
				RepositoryUserControl.Commands.Bisect.Execute(_repositoryUserControl, BisectGitCommand.BisectCommand.Good);
			}
		}

		private void Button2_Click(object sender, RoutedEventArgs e)
		{
			if (_showingGitignoreSuggestion)
			{
				DismissGitignoreSuggestion();
			}
			else if (_repositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.BisectInProgress)
			{
				RepositoryUserControl.Commands.Bisect.Execute(_repositoryUserControl, BisectGitCommand.BisectCommand.Bad);
			}
		}

		private void Button3_Click(object sender, RoutedEventArgs e)
		{
			if (_repositoryUserControl.RepositoryStatus?.RepositoryState is RepositoryState.BisectInProgress)
			{
				RepositoryUserControl.Commands.Bisect.Execute(_repositoryUserControl, BisectGitCommand.BisectCommand.Skip);
			}
		}

		private void AbortButton_Click(object sender, RoutedEventArgs e)
		{
			new ShowAbortConflictWindowCommand().Execute(_repositoryUserControl);
		}

		private void UpdateGitignoreSuggestion()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule != null && !File.Exists(gitModule.MakePath(".gitignore")) && !gitModule.Settings.GitignoreSuggestionDismissed)
			{
				RepositoryStatus repositoryStatus = _repositoryUserControl.RepositoryStatus;
				if (repositoryStatus == null || repositoryStatus.FilesCount != 0)
				{
					_showingGitignoreSuggestion = true;
					AbortButton.Collapse();
					UpdateButton(Button1, "Add .gitignore…");
					UpdateButton(Button2, "Close");
					ShowNotificationBar("Consider adding a .gitignore file to your repository");
					return;
				}
			}
			_showingGitignoreSuggestion = false;
			HideNotificationBar();
		}

		private void DismissGitignoreSuggestion()
		{
			GitModule gitModule = _repositoryUserControl.GitModule;
			if (gitModule != null)
			{
				gitModule.Settings.GitignoreSuggestionDismissed = true;
				gitModule.Settings.Save();
			}
			_showingGitignoreSuggestion = false;
			HideNotificationBar();
		}

		private void ShowGitignoreTemplateWindow()
		{
			RepositoryUserControl.Commands.ShowAddGitignoreTemplateWindow.Execute(_repositoryUserControl);
		}

		private void UpdateButton(Button button, string title, bool show = true)
		{
			if (show)
			{
				button.Show();
			}
			else
			{
				button.Collapse();
			}
			button.Content = Translate(title);
		}

		private void HideNotificationBar()
		{
			IsControlVisible = false;
		}

		private void ShowNotificationBar(string message)
		{
			ShowNotificationBar(new Run[1]
			{
				new Run(Translate(message))
			});
		}

		private void ShowNotificationBar(IEnumerable<Inline> inlines)
		{
			NotificationTextBlock.Show();
			RebaseContainer.Collapse();
			NotificationTextBlock.Inlines.Clear();
			NotificationTextBlock.Inlines.AddRange(inlines);
			IsControlVisible = true;
		}

		private void ShowRebaseNotificationBar(IEnumerable<Inline> inlines, int done, int total, IEnumerable<Inline> additionalInlines)
		{
			RebaseContainer.Show();
			NotificationTextBlock.Collapse();
			RebaseNotificationTextBlock.Inlines.Clear();
			RebaseNotificationTextBlock.Inlines.AddRange(inlines);
			RebaseProgressBar.Value = done;
			RebaseProgressBar.Maximum = total;
			RebaseAdditionalNotificationTextBlock.Inlines.Clear();
			RebaseAdditionalNotificationTextBlock.Inlines.AddRange(additionalInlines);
			IsControlVisible = true;
		}

		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
