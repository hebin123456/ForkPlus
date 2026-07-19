using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionSummaryUserControl : UserControl
	{
		private static Style ParentButtonStyle => Application.Current?.TryFindResource("TextButtonStyle") as Style;

		[Null]
		private RevisionSearchQuery _searchQuery;

		private BugtrackerLinkDefinition[] _bugtrackers;

		private UserColors _userColors;

		private Sha? _sha;

		public RevisionDetailsUserControl RevisionDetailsUserControl { get; set; }

		public RepositoryUserControl RepositoryUserControl => RevisionDetailsUserControl.RepositoryUserControl;

		public RevisionSummaryUserControl()
		{
			InitializeComponent();
			DescriptionTextBlock.FontFamily = FontConstants.MonospaceFontFamily;
		}

		public void Refresh(Sha sha, BugtrackerLinkDefinition[] bugtrackers, UserColors userColors)
		{
			_sha = sha;
			_bugtrackers = bugtrackers;
			_userColors = userColors;
			// AI Explain 按钮：仅在 AI 配置完毕时显示
		if (OpenAiService.IsAiReviewConfigured())
		{
			AiExplainCommitButton.Show();
			AiExplainCommitButton.ToolTip = Preferences.PreferencesLocalization.Translate("Use AI to explain this commit", ForkPlusSettings.Default.UiLanguage);
		}
		else
		{
			AiExplainCommitButton.Collapse();
		}
			FullRevisionDetails fullRevisionDetails = RevisionDetailsUserControl.FullRevisionDetails;
			RevisionDetails revisionDetails = fullRevisionDetails.RevisionDetails;
			AuthorAvatarImage.UserIdentity = revisionDetails.Author;
			AuthorTextBlock.Text = revisionDetails.Author.Name;
			AuthorEmailTextBlock.Text = revisionDetails.Author.Email;
			AuthorDateTextBlock.Text = revisionDetails.AuthorDate.ToString(Consts.FullDateTimeFormat);
			AuthorColorsToggleButton.Show();
			if (IsCommitterSectionVisible(revisionDetails))
			{
				Grid.SetColumnSpan(AuthorDetailsContainer, 2);
				CommitterDetailsContainer.Show();
				CommitterAvatarImage.UserIdentity = revisionDetails.Committer;
				CommitterTextBlock.Text = revisionDetails.Committer.Name;
				CommitterEmailTextBlock.Text = revisionDetails.Committer.Email;
				CommitterDateTextBlock.Text = revisionDetails.CommitterDate.ToString(Consts.FullDateTimeFormat);
				CommitterColorsToggleButton.Show();
			}
			else
			{
				Grid.SetColumnSpan(AuthorDetailsContainer, 4);
				CommitterDetailsContainer.Collapse();
			}
			UpdateReferences(revisionDetails.Sha);
			ShaTextBlock.Text = revisionDetails.Sha.ToString();
			RevisionRemoteButtonsContainer.Children.Clear();
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null && !revisionDetails.IsStash(repositoryData.Stashes))
			{
				Remote[] items = repositoryData.Remotes.Items;
				foreach (Remote remote in items)
				{
					string text = new RepositoryUrlBuilder(remote).CreateRevisionShaUrl(revisionDetails.Sha.ToString());
					if (text != null)
					{
						RevisionRemoteButtonsContainer.Children.Add(CreateRevisionRemoteButton(remote, sha, text));
					}
				}
			}
			ParentsContainer.Children.Clear();
			Sha[] parents = revisionDetails.Parents;
			foreach (Sha parent in parents)
			{
				ParentsContainer.Children.Add(CreateParentButton(RepositoryUserControl, parent, delegate
				{
					RepositoryUserControl.SelectRevision(parent);
				}));
			}
			revisionDetails.MessageParts(out var subject, out var description);
			SubjectTextBlock.Text = subject;
			DescriptionTextBlock.Text = description;
			DescriptionTextBlock.Visibility = (string.IsNullOrEmpty(description) ? Visibility.Collapsed : Visibility.Visible);
			ApplySearch();
			int num = 100;
			RebuildDiffRows(fullRevisionDetails.ChangedFiles.Take(num));
			UpdateExpandAllButtonTitle();
			UpdateDiffListLimitText(fullRevisionDetails.ChangedFiles.Length, num);
		}

		public void ApplyLocalization()
		{
			Preferences.PreferencesLocalization.ApplyCurrent(this);
			UpdateExpandAllButtonTitle();
			var fullRevisionDetails = RevisionDetailsUserControl?.FullRevisionDetails;
			if (fullRevisionDetails != null)
			{
				UpdateDiffListLimitText(fullRevisionDetails.ChangedFiles.Length, 100);
			}
		}

		private void UpdateDiffListLimitText(int changedFilesCount, int limit)
		{
			if (changedFilesCount >= limit)
			{
				DiffListLimitTextBlock.Text = Preferences.PreferencesLocalization.FormatCurrent("See full list of changed files ({0}) in the Changes tab", changedFilesCount);
				DiffListLimitTextBlock.Show();
			}
			else
			{
				DiffListLimitTextBlock.Collapse();
			}
		}

		private void RebuildDiffRows(IEnumerable<ChangedFile> changedFiles)
		{
			ClearDiffRows();
			DiffList.ItemsSource = changedFiles.Select((ChangedFile x) => new DiffEntry(RepositoryUserControl, x)).ToArray();
		}

		private void ClearDiffRows()
		{
			DiffList.ItemsSource = null;
		}

		public void HighlightSearchMatches([Null] RevisionSearchQuery searchQuery)
		{
			if (!RevisionSearchQuery.Equals(_searchQuery, searchQuery))
			{
				_searchQuery = searchQuery;
				ApplySearch();
			}
		}

		private void ExpandButton_Click(object sender, RoutedEventArgs e)
		{
			UpdateExpandAllButtonTitle(AllItemsExpanded());
			if (!(sender is Expander { DataContext: DiffEntry diffEntry } expander) || !expander.IsExpanded || diffEntry.Content != null)
			{
				return;
			}
			LoadDiffEntryContent(diffEntry);
		}

		private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
		{
			bool expand = !AllItemsExpanded();
			foreach (DiffEntry diffEntry in DiffList.Items.OfType<DiffEntry>())
			{
				diffEntry.IsExpanded = expand;
				if (expand && diffEntry.Content == null)
				{
					LoadDiffEntryContent(diffEntry);
				}
			}
			UpdateExpandAllButtonTitle(expand);
		}

		private void LoadDiffEntryContent(DiffEntry diffEntry)
		{
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			var fullRevisionDetails = RevisionDetailsUserControl.FullRevisionDetails;
			if (gitModule == null || fullRevisionDetails == null || diffEntry == null || diffEntry.ChangedFile == null)
			{
				return;
			}
			Sha sha = fullRevisionDetails.RevisionDetails.Sha;
			diffEntry.Content = new GetRevisionFileChangesGitCommand().Execute(gitModule, new RevisionDiffTarget.Revision(sha), diffEntry.ChangedFile, ForkPlusSettings.Default.DiffContextSize, gitModule.Settings.TabWidth, ForkPlusSettings.Default.DiffIgnoreWhitespaces, showEntireFile: false);
		}

		private void AuthorColorsToggleButton_Changed(object sender, RoutedEventArgs args)
		{
			if (sender == AuthorColorsToggleButton)
			{
				string text = AuthorEmailTextBlock.Text;
				byte colorId = _userColors.GetColorId(text);
				CreateColorsPopup(AuthorColorsToggleButton, text, colorId);
			}
		}

		private void CommitterColorsToggleButton_Changed(object sender, RoutedEventArgs args)
		{
			if (sender == CommitterColorsToggleButton)
			{
				string text = CommitterEmailTextBlock.Text;
				byte colorId = _userColors.GetColorId(text);
				CreateColorsPopup(CommitterColorsToggleButton, text, colorId);
			}
		}

		private static Button CreateRevisionRemoteButton(Remote remote, Sha sha, string revisionShaUrl)
		{
			Image content = new Image
			{
				Height = 14.0,
				Width = 14.0,
				Source = remote.RemoteType.Icon()
			};
			Button button = new Button();
			button.Height = 16.0;
			button.Width = 16.0;
			button.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
			button.Padding = new Thickness(0.0);
			button.IsTabStop = false;
			button.BorderThickness = new Thickness(0.0);
			button.Style = Theme.TransparentButtonStyle;
			button.ToolTip = Preferences.PreferencesLocalization.FormatCurrent("Open '{0}' on {1} ({2})", sha.ToAbbreviatedString(), remote.Name, remote.RemoteType.FriendlyName());
			button.Content = content;
			button.Click += delegate
			{
				new Uri(revisionShaUrl).OpenInBrowser();
			};
			return button;
		}

		private void CreateColorsPopup(ToggleButton parentButton, string email, byte userBrushIndex)
		{
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			if (gitModule != null)
			{
				Popup popup = new Popup();
				popup.HorizontalOffset = -270.0;
				popup.StaysOpen = false;
				popup.AllowsTransparency = true;
				popup.PopupAnimation = PopupAnimation.None;
				popup.PlacementTarget = parentButton;
				popup.Opened += delegate
				{
					parentButton.Disable();
				};
				popup.Closed += delegate
				{
					BindingOperations.ClearBinding(popup, Popup.IsOpenProperty);
					parentButton.Enable();
				};
				BindingOperations.SetBinding(popup, Popup.IsOpenProperty, new Binding("IsChecked")
				{
					Source = parentButton
				});
				UserColorsUserControl userColorsUserControl = new UserColorsUserControl(parentButton, email, userBrushIndex);
				userColorsUserControl.SelectedColorChanged += delegate(object s, (string, byte) colormap)
				{
					new UpdateUserColorGitCommand().Execute(gitModule, colormap.Item1, colormap.Item2);
					RepositoryUserControl.InvalidateAndRefresh(SubDomain.UserColors);
				};
				VisualTreeAttachmentHelper.TrySetPopupChild(popup, userColorsUserControl, GetType().Name + ".Popup");
			}
		}

		private UIElement CreateParentButton(RepositoryUserControl repositoryUserControl, Sha parent, Action action)
		{
			return new AdvancedTooltipButton(repositoryUserControl, parent, action)
			{
				Content = parent.ToAbbreviatedString(),
				Margin = new Thickness(0.0, 0.0, 3.0, 0.0),
				Padding = new Thickness(0.0),
				FontSize = 12.0,
				Style = ParentButtonStyle
			};
		}

		private void ApplySearch()
		{
			RevisionSearchQuery searchQuery = _searchQuery;
			if (searchQuery != null && searchQuery.Type == RevisionSearchType.All)
			{
				ShaTextBlock.ApplySearchHighlighting(_searchQuery?.SearchString);
			}
			else
			{
				ShaTextBlock.ApplySearchHighlighting(null);
			}
			RevisionSearchQuery searchQuery2 = _searchQuery;
			if (searchQuery2 != null && searchQuery2.Type == RevisionSearchType.Message)
			{
				AuthorTextBlock.ApplySearchHighlighting(null);
				AuthorEmailTextBlock.ApplySearchHighlighting(null);
			}
			else
			{
				AuthorTextBlock.ApplySearchHighlighting(_searchQuery?.SearchString);
				AuthorEmailTextBlock.ApplySearchHighlighting(_searchQuery?.SearchString);
			}
			SubjectTextBlock.ApplySearchAndButrackerHighlighting(_searchQuery?.SearchString, _bugtrackers);
			DescriptionTextBlock.ApplySearchAndButrackerHighlighting(_searchQuery?.SearchString, _bugtrackers);
		}

		private void UpdateExpandAllButtonTitle(bool unused = false)
		{
			string title = AllItemsExpanded() ? "Collapse All" : "Expand All";
			ExpandAllButton.Content = Preferences.PreferencesLocalization.Translate(title, ForkPlusSettings.Default.UiLanguage);
		}

		private bool AllItemsExpanded()
		{
			bool hasRows = false;
			foreach (DiffEntry diffEntry in DiffList.Items.OfType<DiffEntry>())
			{
				hasRows = true;
				if (!diffEntry.IsExpanded)
				{
					return false;
				}
			}
			return hasRows;
		}

		private void UpdateReferences(Sha sha)
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				List<ForkPlus.Git.Reference> list = repositoryData.References.Items.Filter((ForkPlus.Git.Reference x) => x.Sha == sha);
				if (list.Count > 0)
				{
					ReferencesTextBlock.Show();
					ReferencePanel.Refresh(list, repositoryData.Remotes.Items);
					ReferencePanel.Show();
				}
				else
				{
					ReferencesTextBlock.Collapse();
					ReferencePanel.Refresh(new ForkPlus.Git.Reference[0], new Remote[0]);
					ReferencePanel.Collapse();
				}
			}
		}

		private static bool IsCommitterSectionVisible(RevisionDetails revisionDetails)
		{
			if (!(revisionDetails.Author.Name != revisionDetails.Committer.Name) && !(revisionDetails.Author.Email != revisionDetails.Committer.Email))
			{
				return revisionDetails.AuthorDate != revisionDetails.CommitterDate;
			}
			return true;
		}

		/// <summary>AI 解释当前 commit：拉取 commit 的 subject/body/diff，打开 AiTextResultWindow 流式展示 AI 解释。</summary>
		private void AiExplainCommitButton_Click(object sender, RoutedEventArgs e)
		{
			if (!OpenAiService.IsAiReviewConfigured())
			{
				MessageBox.Show(
					Preferences.PreferencesLocalization.Translate("AI is not configured. Please configure AI review settings in Preferences first.", ForkPlusSettings.Default.UiLanguage),
					Preferences.PreferencesLocalization.Translate("AI Explain Commit", ForkPlusSettings.Default.UiLanguage),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}
			if (!_sha.HasValue)
			{
				return;
			}
			Sha sha = _sha.GetValueOrDefault();
			GitModule gitModule = RepositoryUserControl?.GitModule;
			if (gitModule == null)
			{
				return;
			}
			FullRevisionDetails fullRevisionDetails = RevisionDetailsUserControl?.FullRevisionDetails;
			if (fullRevisionDetails == null)
			{
				return;
			}
			fullRevisionDetails.RevisionDetails.MessageParts(out var subject, out var body);
			string commitSubject = subject ?? "";
			string commitBody = body ?? "";
			string shaStr = sha.ToString();
			string abbreviatedSha = sha.ToAbbreviatedString();

			AiTextResultWindow window = new AiTextResultWindow();
			window.Owner = Window.GetWindow(this);
			string title = Preferences.PreferencesLocalization.FormatCurrent("AI Explain {0}", abbreviatedSha);
			window.Show();
			window.StartStreaming(title, delegate(AiTextResultWindow w, JobMonitor monitor)
			{
				try
				{
					// 拉取 commit 的 patch（含变更文件和差异内容）
					GitCommand gitCommand = new GitCommand("--no-pager", "show", "--no-color", "--find-renames", "--submodule=short", "--unified=50", "--no-ext-diff", shaStr);
					GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
					string diffSummary = "";
					if (gitRequestResult.ExitCode < 2)
					{
						diffSummary = gitRequestResult.Stdout;
						// 限制 diff 体量，避免 token 爆炸
						const int maxDiffChars = 20000;
						if (diffSummary.Length > maxDiffChars)
						{
							diffSummary = diffSummary.Substring(0, maxDiffChars) + "\n... (diff truncated)\n";
						}
					}
					OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<OpenAiResponse> response = openAiService.ExplainCommit(commitSubject, commitBody, diffSummary, monitor, w.OnChunk);
					if (monitor.IsCanceled)
					{
						return;
					}
					if (!response.Succeeded)
					{
						w.OnError(response.Error.FriendlyMessage);
					}
					else
					{
						w.OnSuccess(response.Result.Message);
					}
				}
				catch (Exception ex)
				{
					w.OnError(ex.Message);
				}
			});
		}

		private void ListBoxItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
		{
			if (sender is ListBoxItem { DataContext: DiffEntry diffEntry } listBoxItem)
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						GitModule gitModule = repositoryUserControl.GitModule;
						if (gitModule != null)
						{
							Sha? sha = _sha;
							if (sha.HasValue)
							{
								Sha valueOrDefault = sha.GetValueOrDefault();
								listBoxItem.ContextMenu.SetItems(CreateFileContextMenuItems(RevisionDetailsUserControl, repositoryUserControl, repositoryData, gitModule, valueOrDefault, diffEntry.ChangedFile));
								return;
							}
						}
					}
				}
				e.Handled = true;
			}
			else
			{
				e.Handled = true;
			}
		}

		private static IEnumerable<Control> CreateFileContextMenuItems(RevisionDetailsUserControl revisionDetailsUserControl, RepositoryUserControl repositoryUserControl, RepositoryData repositoryData, GitModule gitModule, Sha sha, ChangedFile changedFile)
		{
			bool isSubmodule = changedFile is SubmoduleChangedFile;
			SubmoduleChangedFile submoduleChangedFile = changedFile as SubmoduleChangedFile;
			if (submoduleChangedFile != null)
			{
				yield return RepositoryUserControl.Commands.OpenSubmodule.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(repositoryUserControl, gitModule, new Submodule[1] { submoduleChangedFile.Submodule });
				});
				yield return new Separator();
			}
			if (!isSubmodule)
			{
				bool isEnabled = RepositoryUserControl.Commands.OpenFileInDefaultEditor.IsEditorAvailable(gitModule, changedFile.Path);
				yield return RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(gitModule, changedFile.Path);
				}, isEnabled, showShortcut: false);
				List<ExternalTool> diffTools = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
				if (diffTools.Count > 0)
				{
					bool diffIsEnabled = !changedFile.IsDirectory;
					RunExternalDiffToolCommand.DiffTarget.Revision diffTarget = new RunExternalDiffToolCommand.DiffTarget.Revision(sha, null, changedFile);
					if (diffTools.Count == 1)
					{
						ExternalTool diffTool = diffTools[0];
						yield return RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItemFormat("Diff in {0}", new object[1] { diffTool.Name }, delegate
						{
							RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, diffTool);
						}, diffIsEnabled, null, showShortcut: false);
					}
					else if (diffTools.Count > 1)
					{
						MenuItem diffMenuItem = new MenuItem
						{
							Header = Preferences.PreferencesLocalization.MenuHeader("External Diff"),
							IsEnabled = diffIsEnabled
						};
						foreach (ExternalTool diffTool in diffTools)
						{
							ExternalTool capturedDiffTool = diffTool;
							MenuItem newItem = RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItem(capturedDiffTool.Name ?? "", delegate
							{
								RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, capturedDiffTool);
							}, isEnabled: true, null, showShortcut: false);
							diffMenuItem.Items.Add(newItem);
						}
						yield return diffMenuItem;
					}
				}
			}
			yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(gitModule, changedFile.Path);
			});
			yield return new Separator();
			MenuItem resetMenuItem = new MenuItem
			{
				Header = Preferences.PreferencesLocalization.MenuHeader("Reset File To")
			};
			resetMenuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State At Commit...", delegate
			{
				RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, new ChangedFile[1] { changedFile }, sha.ToString());
			}));
			resetMenuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State Before Commit...", delegate
			{
				RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, new ChangedFile[1] { changedFile }, sha.ToString() + "~");
			}));
			yield return resetMenuItem;
			yield return new Separator();
			if (!isSubmodule)
			{
				yield return RepositoryUserControl.Commands.ShowBlameWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowBlameWindow.Execute(repositoryUserControl, changedFile.Path, sha);
				});
			}
			yield return RepositoryUserControl.Commands.ShowFileHistoryWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, changedFile.Mode(), sha);
			});
			yield return RepositoryUserControl.Commands.ShowFileInFileTree.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileTree.Execute(revisionDetailsUserControl, changedFile.Path);
			});
			if (!isSubmodule && repositoryData.GitLfsInitialized && repositoryData.Remotes.HasLfsCompatibleRemotes())
			{
				MenuItem lfsMenuItem = new MenuItem();
				lfsMenuItem.Header = Preferences.PreferencesLocalization.MenuHeader("LFS");
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsLockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsLockCommand.Execute(repositoryUserControl, new string[1] { changedFile.Path });
				}));
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsUnlockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsUnlockCommand.Execute(repositoryUserControl, new string[1] { changedFile.Path });
				}));
				yield return new Separator();
				yield return lfsMenuItem;
			}
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.RepositoryFile);
			CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, changedFile.Path, sha);
			if (changedFile is SubmoduleChangedFile customCommandSubmoduleChangedFile)
			{
				customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.Submodule);
				CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
				{
					new CustomCommandEnvironment.SubmoduleParameter(customCommandSubmoduleChangedFile.Submodule)
				};
				env = new CustomCommandEnvironment(gitModule, parameters);
			}
			if (customCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> customMenuItems = new List<MenuItem>();
				foreach (CustomCommand customCommand in customCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						customCommand.AddCustomCommandItem(repositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, customMenuItems);
					}
				}
				foreach (MenuItem menuItem in customMenuItems)
				{
					yield return menuItem;
				}
			}
			if (!isSubmodule)
			{
				yield return new Separator();
				yield return RepositoryUserControl.Commands.SaveFile.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.SaveFile.Execute(repositoryUserControl, changedFile, sha.ToString());
				});
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyFilePaths.Execute(new string[1] { changedFile.Path });
			}, isEnabled: true, showShortcut: false);
			yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(gitModule, new string[1] { changedFile.Path });
			}, isEnabled: true, showShortcut: false);
		}

	}
}
