using System;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands.LeanBranching;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class ToolbarUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private MainWindow _mainWindow;

		public ToolbarUserControl()
		{
			InitializeComponent();
			FetchToolbarButton.ToolTip = Preferences.PreferencesLocalization.Current("Fetch") + Environment.NewLine + Preferences.PreferencesLocalization.Current("Hold Ctrl for Quick Fetch");
			PullToolbarButton.ToolTip = Preferences.PreferencesLocalization.Current("Pull") + Environment.NewLine + Preferences.PreferencesLocalization.Current("Hold Ctrl for Quick Pull");
			PushToolbarButton.ToolTip = Preferences.PreferencesLocalization.Current("Push") + Environment.NewLine + Preferences.PreferencesLocalization.Current("Hold Ctrl for Quick Push");
			WeakEventManager<NotificationCenter, EventArgs<ClosableTabItem>>.AddHandler(NotificationCenter.Current, "ActiveTabChanged", ActiveTabChanged);
			WeakEventManager<NotificationCenter, EventArgs>.AddHandler(NotificationCenter.Current, "ShellChanged", ShellChanged);
		}

		public void Initialize(MainWindow mainWindow)
		{
			_mainWindow = mainWindow;
			ApplyLocalization();
			OpenQuicklyToolbarButton.Click += delegate
			{
				MainWindow.Commands.ShowQuickLaunchWindow.Execute();
			};
			FetchToolbarButton.Click += delegate
			{
				RepositoryUserControl repositoryUserControl5 = _mainWindow?.TabManager.ActiveRepositoryUserControl;
				if (repositoryUserControl5 != null)
				{
					if (KeyboardHelper.IsCtrlDown)
					{
						MainWindow.Commands.QuickFetch.Execute(repositoryUserControl5, repositoryUserControl5.GitModule);
					}
					else
					{
						MainWindow.Commands.ShowFetchWindow.Execute(repositoryUserControl5, repositoryUserControl5.GitModule);
					}
				}
			};
			PullToolbarButton.Click += delegate
			{
				RepositoryUserControl repositoryUserControl4 = _mainWindow?.TabManager.ActiveRepositoryUserControl;
				if (repositoryUserControl4 != null)
				{
					if (KeyboardHelper.IsCtrlDown)
					{
						MainWindow.Commands.QuickPull.Execute(repositoryUserControl4);
					}
					else
					{
						MainWindow.Commands.ShowPullWindow.Execute(repositoryUserControl4);
					}
				}
			};
			PushToolbarButton.Click += delegate
			{
				RepositoryUserControl repositoryUserControl3 = _mainWindow?.TabManager.ActiveRepositoryUserControl;
				if (repositoryUserControl3 != null)
				{
					if (KeyboardHelper.IsCtrlDown)
					{
						MainWindow.Commands.QuickPush.Execute(repositoryUserControl3);
					}
					else
					{
						MainWindow.Commands.ShowPushWindow.Execute(repositoryUserControl3);
					}
				}
			};
			StashToolbarButton.Click += delegate
			{
				RepositoryUserControl repositoryUserControl2 = _mainWindow?.TabManager.ActiveRepositoryUserControl;
				if (repositoryUserControl2 != null)
				{
					MainWindow.Commands.ShowSaveStashWindow.Execute(repositoryUserControl2, repositoryUserControl2.GitModule);
				}
			};
			OpenInConsoleToolbarButton.Click += delegate
			{
				RepositoryUserControl repositoryUserControl = _mainWindow?.TabManager.ActiveRepositoryUserControl;
				if (repositoryUserControl != null)
				{
					MainWindow.Commands.OpenRepositoryInShellTool.Execute(repositoryUserControl.GitModule);
				}
			};
			AiDevelopmentToolbarButton.Click += delegate
			{
				RepositoryUserControl repositoryUserControl = _mainWindow?.TabManager.ActiveRepositoryUserControl;
				if (repositoryUserControl == null)
				{
					return;
				}
				ForkPlus.Git.GitModule gitModule = repositoryUserControl.GitModule;
				if (gitModule == null)
				{
					return;
				}
				if (!ForkPlus.Accounts.AiServices.OpenAiService.IsAiReviewConfigured())
				{
					System.Windows.MessageBox.Show(
						Preferences.PreferencesLocalization.Current("AI development requires API configuration. Please configure service URL and API Key in Settings → AI Review."),
						Preferences.PreferencesLocalization.Current("Configuration Reminder"),
						System.Windows.MessageBoxButton.OK,
						System.Windows.MessageBoxImage.Information);
					return;
				}
				ForkPlus.UI.Dialogs.AiDevelopmentWindow window = new ForkPlus.UI.Dialogs.AiDevelopmentWindow(repositoryUserControl, gitModule);
				window.Show();
			};
			BranchToolbarButton.Click += delegate
			{
				RepositoryUserControl activeRepositoryUserControl = _mainWindow.TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl != null)
				{
					MainWindow.Commands.ShowCreateBranchWindow.Execute(activeRepositoryUserControl, null);
				}
			};
			AppearanceToolbarDropdownButton.Click += delegate
			{
				if (KeyboardHelper.IsCtrlDown)
				{
					MainWindow.Commands.SwitchApplicationTheme.Execute();
					AppearanceToolbarDropdownButton.ContextMenu.IsOpen = false;
				}
				else
				{
					InitializeAppearanceToolBarButtonContextMenu();
				}
			};
			WorkspacesToolbarDropdownButton.Click += delegate
			{
				if (KeyboardHelper.IsCtrlDown)
				{
					MainWindow.Commands.SwitchWorkspace.Execute();
					WorkspacesToolbarDropdownButton.ContextMenu.IsOpen = false;
				}
				else
				{
					InitializeWorkspacesToolbarDropdownButtonContextMenu();
				}
			};
		}

		public void RefreshWorkspacesButton()
		{
			WorkspacesToolbarDropdownButton.Title = Preferences.PreferencesLocalization.Translate(ForkPlusSettings.Default.Workspaces.ActiveWorkspace.Name.Split(Consts.Chars.Slash).LastItem(), ForkPlusSettings.Default.UiLanguage);
		}

		public void ApplyLocalization()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			OpenQuicklyToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Quick Launch", language);
			FetchToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Fetch", language);
			PullToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Pull", language);
			PushToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Push", language);
			StashToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Stash", language);
			BranchToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Branch", language);
			AppearanceToolbarDropdownButton.Title = Preferences.PreferencesLocalization.Translate("Appearance", language);
			OpenInDropDownButton.Title = Preferences.PreferencesLocalization.Translate("Open in", language);
			OpenInConsoleToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Console", language);
			AiDevelopmentToolbarButton.Title = Preferences.PreferencesLocalization.Translate("AI-Assisted Development", language);
			FetchToolbarButton.ToolTip = Preferences.PreferencesLocalization.Translate("Fetch", language) + Environment.NewLine + Preferences.PreferencesLocalization.Translate("Hold Ctrl for Quick Fetch", language);
			PullToolbarButton.ToolTip = Preferences.PreferencesLocalization.Translate("Pull", language) + Environment.NewLine + Preferences.PreferencesLocalization.Translate("Hold Ctrl for Quick Pull", language);
			PushToolbarButton.ToolTip = Preferences.PreferencesLocalization.Translate("Push", language) + Environment.NewLine + Preferences.PreferencesLocalization.Translate("Hold Ctrl for Quick Push", language);
			RefreshWorkspacesButton();
			StatusUserControl.ApplyLocalization();
		}

		public void RefreshPullPushBadges(UpstreamStatus? upstreamStatus)
		{
			if (upstreamStatus.HasValue)
			{
				UpstreamStatus valueOrDefault = upstreamStatus.GetValueOrDefault();
				if (valueOrDefault.IsValid)
				{
					RefreshBadge(PullBadge, PullBadgeText, PullToolbarButton, valueOrDefault.Behind);
					RefreshBadge(PushBadge, PushBadgeText, PushToolbarButton, valueOrDefault.Ahead);
					return;
				}
			}
			PullBadge.Collapse();
			PushBadge.Collapse();
		}

		private void RefreshBadge(Border badge, TextBlock badgeText, FrameworkElement button, int count)
		{
			if (count > 0)
			{
				badgeText.Text = count.ToString();
				badge.Show();
				RefreshBadgePosition(badge, button);
			}
			else
			{
				badge.Collapse();
			}
		}

		private void RefreshBadgePosition(FrameworkElement badge, FrameworkElement button)
		{
			Point point = button.TranslatePoint(new Point(0.0, 0.0), BadgesCanvas);
			Canvas.SetLeft(badge, point.X + button.ActualWidth - 10.0);
			Canvas.SetTop(badge, point.Y - 2.0);
		}

		private void ActiveTabChanged(object sender, EventArgs<ClosableTabItem> args)
		{
			RefreshToolbar();
		}

		private void ShellChanged(object sender, EventArgs args)
		{
			OpenInConsoleToolbarButton.Title = Preferences.PreferencesLocalization.Translate("Console", ForkPlusSettings.Default.UiLanguage);
		}

		private void RefreshToolbar()
		{
			ClosableTabItem activeTab = _mainWindow.TabManager.ActiveTab;
			RepositoryUserControl repositoryUserControl = _mainWindow?.TabManager.ActiveRepositoryUserControl;
			bool isEnabled = repositoryUserControl != null;
			FetchToolbarButton.IsEnabled = isEnabled;
			PullToolbarButton.IsEnabled = isEnabled;
			PushToolbarButton.IsEnabled = isEnabled;
			StashToolbarButton.IsEnabled = isEnabled;
			StashToolbarDropdownButton.IsEnabled = isEnabled;
			BranchToolbarButton.IsEnabled = isEnabled;
			BranchToolbarDropdownButton.IsEnabled = isEnabled;
			OpenInDropDownButton.IsEnabled = isEnabled;
			OpenInConsoleToolbarButton.IsEnabled = isEnabled;
			AiDevelopmentToolbarButton.IsEnabled = isEnabled;
			if (repositoryUserControl != null)
			{
				RepositoryData repositoryData = repositoryUserControl.RepositoryData;
				if (repositoryData != null)
				{
					LocalBranch activeBranch = repositoryData.References.ActiveBranch;
					if (activeBranch != null)
					{
						UpstreamStatus? upstreamStatus = repositoryData.UpstreamStatus.GetUpstreamStatus(activeBranch);
						RefreshPullPushBadges(upstreamStatus);
						return;
					}
				}
			}
			RefreshPullPushBadges(null);
		}

		private void InitializeAppearanceToolBarButtonContextMenu()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			ContextMenu contextMenu = AppearanceToolbarDropdownButton.ContextMenu;
			contextMenu.Items.Clear();
			contextMenu.Items.Add(new HeaderMenuItem(Preferences.PreferencesLocalization.Translate("Theme", language)));
			MenuItem menuItem = MainWindow.Commands.SwitchApplicationTheme.CreateMenuItem(Preferences.PreferencesLocalization.Translate("Light", language), delegate
			{
				MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Light);
			});
			menuItem.IsChecked = ForkPlusSettings.Default.Theme == ThemeType.Light;
			contextMenu.Items.Add(menuItem);
			MenuItem menuItem2 = MainWindow.Commands.SwitchApplicationTheme.CreateMenuItem(Preferences.PreferencesLocalization.Translate("Dark", language), delegate
			{
				MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Dark);
			});
			menuItem2.IsChecked = ForkPlusSettings.Default.Theme == ThemeType.Dark;
			contextMenu.Items.Add(menuItem2);
			contextMenu.Items.Add(new Separator
			{

				Margin = new Thickness(-30.0, 0.0, 0.0, 0.0)
			});
			contextMenu.Items.Add(new HeaderMenuItem(Preferences.PreferencesLocalization.Translate("Language", language)));
			foreach (Preferences.PreferencesLocalization.LanguageOption languageOption in Preferences.PreferencesLocalization.GetLanguages())
			{
				AddLanguageMenuItem(contextMenu.Items, languageOption.Code, languageOption.DisplayName);
			}
			contextMenu.Items.Add(new Separator
			{
				Margin = new Thickness(-30.0, 0.0, 0.0, 0.0)
			});
			contextMenu.Items.Add(new HeaderMenuItem(Preferences.PreferencesLocalization.Translate("Commit List Layout", language)));
			ClosableTabItem activeTab = _mainWindow.TabManager.ActiveTab;
			bool isEnabled = _mainWindow?.TabManager.ActiveRepositoryUserControl != null;
			MenuItem menuItem3 = MainWindow.Commands.SwitchApplicationTheme.CreateMenuItem(Preferences.PreferencesLocalization.Translate("Horizontal", language), delegate
			{
				MainWindow.Commands.SwitchRevisionListOrientation.Execute(RevisionListOrientation.Horizontal);
			});
			menuItem3.IsChecked = ForkPlusSettings.Default.RevisionListOrientation == RevisionListOrientation.Horizontal;
			menuItem3.IsEnabled = isEnabled;
			contextMenu.Items.Add(menuItem3);
			MenuItem menuItem4 = MainWindow.Commands.SwitchApplicationTheme.CreateMenuItem(Preferences.PreferencesLocalization.Translate("Vertical", language), delegate
			{
				MainWindow.Commands.SwitchRevisionListOrientation.Execute(RevisionListOrientation.Vertical);
			});
			menuItem4.IsChecked = ForkPlusSettings.Default.RevisionListOrientation == RevisionListOrientation.Vertical;
			menuItem4.IsEnabled = isEnabled;
			contextMenu.Items.Add(menuItem4);
		}

		private static void AddLanguageMenuItem(ItemCollection items, string language, string title)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = title,
				IsChecked = ForkPlusSettings.Default.UiLanguage == language
			};
			menuItem.Click += delegate
			{
				ForkPlusSettings.Default.UiLanguage = language;
				MainWindow.Instance?.ApplyLocalization();
			};
			items.Add(menuItem);
		}

		private void StashToolbarDropdownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
			contextMenu.Items.Add(new HeaderMenuItem(Preferences.PreferencesLocalization.Translate("Recent Stashes", language)));
			RepositoryUserControl repositoryUserControl = _mainWindow?.TabManager.ActiveRepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			StashRevision[] array = repositoryUserControl.RepositoryData?.Stashes.Items;
			if (array == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			contextMenu.Items.Add(new Separator());
			for (int i = 0; i < array.Length && i < 15; i++)
			{
				StashRevision stashRevision = array[i];
				MenuItem newItem = RepositoryUserControl.Commands.ShowApplyStashWindow.CreateMenuItem(stashRevision.Message, delegate
				{
					RepositoryUserControl.Commands.ShowApplyStashWindow.Execute(repositoryUserControl, stashRevision);
				});
				contextMenu.Items.Add(newItem);
			}
			contextMenu.Items.Add(new Separator());
			MenuItem newItem2 = RepositoryUserControl.Commands.ShowSaveSnapshotWindow.CreateMenuItem(Preferences.PreferencesLocalization.Translate("Save Snapshot...", language), delegate
			{
				RepositoryUserControl.Commands.ShowSaveSnapshotWindow.Execute(repositoryUserControl, gitModule);
			});
			contextMenu.Items.Add(newItem2);
		}

		private void BranchToolbarDropdownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
			RepositoryUserControl repositoryUserControl = _mainWindow?.TabManager.ActiveRepositoryUserControl;
			if (repositoryUserControl == null)
			{
				return;
			}
			RepositoryData repositoryData = repositoryUserControl.RepositoryData;
			if (repositoryData == null)
			{
				return;
			}
			GitModule gitModule = repositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			RepositoryReferences references = repositoryData.References;
			LocalBranch activeBranch = references.ActiveBranch;
			string language = ForkPlusSettings.Default.UiLanguage;
			contextMenu.Items.Add(MainWindow.Commands.ShowCreateBranchWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowCreateBranchWindow.Execute(repositoryUserControl, null);
			}));
			contextMenu.Items.Add(MainWindow.Commands.ShowCreateWorktreeWindow.CreateMenuItem(delegate
			{
				MainWindow.Commands.ShowCreateWorktreeWindow.Execute(repositoryUserControl);
			}));
			if (repositoryData.GitFlowSettings != null)
			{
				contextMenu.Items.Add(new Separator());
				contextMenu.Items.Add(new HeaderMenuItem(Preferences.PreferencesLocalization.Translate("Git Flow", language)));
				contextMenu.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.Execute(repositoryUserControl, gitModule);
				}));
				contextMenu.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.Execute(repositoryUserControl, gitModule);
				}));
				contextMenu.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.Execute(repositoryUserControl, gitModule);
				}));
				LocalBranch localBranch = activeBranch;
				if (localBranch != null && localBranch.IsFeatureBranch(repositoryData.GitFlowSettings))
				{
					contextMenu.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.CreateMenuItem(string.Format(Preferences.PreferencesLocalization.Translate("Finish '{0}'...", language), activeBranch.Name), delegate
					{
						RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.Execute(repositoryUserControl, gitModule, repositoryData, activeBranch);
					}));
				}
				else
				{
					LocalBranch localBranch2 = activeBranch;
					if (localBranch2 != null && localBranch2.IsReleaseBranch(repositoryData.GitFlowSettings))
					{
						contextMenu.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.CreateMenuItem(string.Format(Preferences.PreferencesLocalization.Translate("Finish '{0}'...", language), activeBranch.Name), delegate
						{
							RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.Execute(repositoryUserControl, gitModule, repositoryData, activeBranch);
						}));
					}
					else
					{
						LocalBranch localBranch3 = activeBranch;
						if (localBranch3 != null && localBranch3.IsHotfixBranch(repositoryData.GitFlowSettings))
						{
							contextMenu.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.CreateMenuItem(string.Format(Preferences.PreferencesLocalization.Translate("Finish '{0}'...", language), activeBranch.Name), delegate
							{
								RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.Execute(repositoryUserControl, gitModule, repositoryData, activeBranch);
							}));
						}
					}
				}
			}
			CommitGraphCache commitGraphCache = repositoryUserControl.CommitGraphCache;
			if (commitGraphCache == null)
			{
				return;
			}
			LocalBranch localBranch4 = references.LocalMain(gitModule);
			if (localBranch4 == null)
			{
				return;
			}
			RemoteBranch remoteBranch = references.Upstream(localBranch4);
			if (remoteBranch == null)
			{
				return;
			}
			Branch mainBranch = references.MainBranch(gitModule, commitGraphCache);
			if (mainBranch != null)
			{
				contextMenu.Items.Add(new Separator());
				contextMenu.Items.Add(new HeaderMenuItem(Preferences.PreferencesLocalization.Translate("Lean Branching", language)));
				contextMenu.Items.Add(RepositoryUserControl.Commands.ShowLeanBranchingStartWindow.CreateMenuItem(string.Format(Preferences.PreferencesLocalization.Translate("Start Branch on '{0}'...", language), mainBranch.Name), delegate
				{
					RepositoryUserControl.Commands.ShowLeanBranchingStartWindow.Execute(repositoryUserControl, mainBranch);
				}));
				string header = ((activeBranch == null) ? string.Format(Preferences.PreferencesLocalization.Translate("Sync (Rebase on '{0}')", language), localBranch4.Name) : ((activeBranch != localBranch4) ? string.Format(Preferences.PreferencesLocalization.Translate("Sync '{0}' (Rebase on '{1}')", language), activeBranch.Name, mainBranch.Name) : string.Format(Preferences.PreferencesLocalization.Translate("Sync '{0}' (Rebase on '{1}')", language), activeBranch.Name, remoteBranch.Name)));
				contextMenu.Items.Add(RepositoryUserControl.Commands.LeanBranchingSync.CreateMenuItem(header, delegate
				{
					RepositoryUserControl.Commands.LeanBranchingSync.Execute(repositoryUserControl);
				}, activeBranch != null));
				string header2 = ((activeBranch == null || activeBranch == mainBranch) ? string.Format(Preferences.PreferencesLocalization.Translate("Finish (Merge into '{0}')...", language), localBranch4.Name) : string.Format(Preferences.PreferencesLocalization.Translate("Finish '{0}' (Merge into '{1}')...", language), activeBranch.Name, localBranch4.Name));
				contextMenu.Items.Add(RepositoryUserControl.Commands.ShowLeanBranchingFinishWindow.CreateMenuItem(header2, delegate
				{
					RepositoryUserControl.Commands.ShowLeanBranchingFinishWindow.Execute(repositoryUserControl);
				}, activeBranch != null && activeBranch != localBranch4));
			}
		}

		private void OpenInDropDownButtonContextMenu_Opened(object sender, RoutedEventArgs e)
		{
			ContextMenu contextMenu = sender as ContextMenu;
			contextMenu.Items.Clear();
			RepositoryUserControl activeRepositoryUserControl = _mainWindow.TabManager.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				return;
			}
			GitModule gitModule = activeRepositoryUserControl.GitModule;
			if (gitModule == null)
			{
				return;
			}
			ImageSource consoleIcon = Theme.ConsoleIcon;
			if (!(ForkPlusSettings.Default.ShellTool is ShellTool.Default))
			{
				contextMenu.Items.Add(MainWindow.Commands.OpenRepositoryInDefaultShellTool.CreateMenuItem(new Image
				{
					Source = consoleIcon
				}, delegate
				{
					MainWindow.Commands.OpenRepositoryInDefaultShellTool.Execute(gitModule);
				}));
			}
			contextMenu.Items.Add(MainWindow.Commands.OpenRepositoryInShellTool.CreateMenuItem(new Image
			{
				Source = consoleIcon
			}, delegate
			{
				MainWindow.Commands.OpenRepositoryInShellTool.Execute(gitModule);
			}));
			Image icon = new Image
			{
				Source = Theme.OpenInIcon
			};
			contextMenu.Items.Add(MainWindow.Commands.OpenRepositoryInFileExplorer.CreateMenuItem(icon, delegate
			{
				MainWindow.Commands.OpenRepositoryInFileExplorer.Execute(gitModule);
			}));
			ExternalProjectEditor[] availableEditors = ExternalProjectEditor.GetAvailableEditors();
			if (availableEditors.Length != 0)
			{
				contextMenu.Items.Add(new Separator());
				ExternalProjectEditor[] array = availableEditors;
				foreach (ExternalProjectEditor editor2 in array)
				{
					string[] projectFilePaths = editor2.GetProjectFilePaths(gitModule.Path);
					foreach (string absoluteProjectFilePath in projectFilePaths)
					{
						string text = PathHelper.RelativePathOrFileName(gitModule.Path, absoluteProjectFilePath);
						Image icon2 = new Image
						{
							Source = editor2.Icon
						};
						contextMenu.Items.Add(MainWindow.Commands.OpenRepositoryInExternalEditor.CreateMenuItem("Open '" + text + "' in " + editor2.Name, delegate
						{
							editor2.OpenProject(absoluteProjectFilePath);
						}, isEnabled: true, icon2));
					}
				}
			}
			ExternalRepositoryEditor[] availableEditors2 = ExternalRepositoryEditor.GetAvailableEditors();
			if (availableEditors2.Length != 0)
			{
				ExternalRepositoryEditor[] array2 = availableEditors2;
				foreach (ExternalRepositoryEditor editor in array2)
				{
					Image icon3 = new Image
					{
						Source = editor.Icon
					};
					contextMenu.Items.Add(MainWindow.Commands.OpenRepositoryInExternalEditor.CreateMenuItem("Open in " + editor.Name, delegate
					{
						MainWindow.Commands.OpenRepositoryInExternalEditor.Execute(gitModule, editor);
					}, isEnabled: true, icon3));
				}
			}
			Remote[] array3 = activeRepositoryUserControl.RepositoryData?.Remotes.Items.ToSortedArray(Remote.ComparerIgnoreCaseNumeric);
			if (array3 != null && array3.Length != 0)
			{
				contextMenu.Items.Add(new Separator());
				foreach (Remote remote in array3)
				{
					string text2 = remote.RemoteType.FriendlyName();
					if (text2 == null)
					{
						continue;
					}
					string repositoryWebpageUrl = new RepositoryUrlBuilder(remote).RepositoryWebpageUrl;
					if (repositoryWebpageUrl != null)
					{
						string header = ((array3.Length > 1) ? ("View " + remote.Name + " on " + text2) : ("View on " + text2));
						contextMenu.Items.Add(MainWindow.Commands.OpenUrl.CreateMenuItem(header, delegate
						{
							MainWindow.Commands.OpenUrl.Execute(repositoryWebpageUrl);
						}, isEnabled: true, new Image
						{
							Source = remote.Icon
						}));
					}
				}
			}
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(activeRepositoryUserControl.RepositoryData, CustomCommandTarget.Repository);
			if (customCommands.Length == 0)
			{
				return;
			}
			contextMenu.Items.Add(new Separator());
			int count = contextMenu.Items.Count;
			CustomCommand[] array4 = customCommands;
			foreach (CustomCommand customCommand in array4)
			{
				if (customCommand.OS.IsSupported())
				{
					CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule);
					customCommand.AddCustomCommandItem(activeRepositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, contextMenu.Items, count);
				}
			}
		}

		private void InitializeWorkspacesToolbarDropdownButtonContextMenu()
		{
			string language = ForkPlusSettings.Default.UiLanguage;
			ContextMenu contextMenu = WorkspacesToolbarDropdownButton.ContextMenu;
			contextMenu.Items.Clear();
			contextMenu.Items.Add(new HeaderMenuItem(Preferences.PreferencesLocalization.Translate("Workspaces", language)));
			ForkPlusSettings.WorkspacesSettings workspaces = ForkPlusSettings.Default.Workspaces;
			Workspace activeWorkspace = workspaces.ActiveWorkspace;
			Workspace[] all = workspaces.All;
			foreach (Workspace workspace in all)
			{
				bool isActive = workspace == activeWorkspace;
				AddWorkspaceItem(contextMenu.Items, workspace.Name.Split(Consts.Chars.Slash), 0, workspace, isActive);
			}
			contextMenu.Items.Add(new Separator());
			MenuItem newItem = MainWindow.Commands.ShowConfigureWorkspacesWindow.CreateMenuItem(delegate
			{
				MainWindow.Commands.ShowConfigureWorkspacesWindow.Execute();
			});
			contextMenu.Items.Add(newItem);
		}

		private static void AddWorkspaceItem(ItemCollection menuItems, string[] path, int pathIndex, Workspace workspace, bool isActive)
		{
			string text = path[pathIndex];
			if (pathIndex < path.Length - 1)
			{
				AddWorkspaceItem(FindOrCreateFolderItem(menuItems, text).Items, path, pathIndex + 1, workspace, isActive);
				return;
			}
			MenuItem menuItem = new MenuItem
			{
				Header = text
			};
			menuItem.Click += delegate
			{
				MainWindow.Commands.SwitchWorkspace.Execute(workspace);
			};
			menuItem.IsChecked = isActive;
			menuItems.Add(menuItem);
		}

		private static MenuItem FindOrCreateFolderItem(ItemCollection menuItems, string name)
		{
			foreach (MenuItem item in (IEnumerable)menuItems)
			{
				if (item.Header.ToString().Equals(name, StringComparison.OrdinalIgnoreCase) && item.Items.Count > 0)
				{
					return item;
				}
			}
			MenuItem menuItem2 = new MenuItem
			{
				Header = name
			};
			menuItems.Add(menuItem2);
			return menuItem2;
		}

	}
}
