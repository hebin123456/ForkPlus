using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI
{
	public class MainWindowMenuManager
	{
		private readonly Menu _mainMenu;

		private readonly MenuItem _fileMenuItem;

		private readonly MenuItem _repositoryMenuItem;

		private readonly MenuItem _viewMenuItem;

		private readonly MenuItem _windowMenuItem;

		private readonly MenuItem _aboutMenuItem;

		public MainWindowMenuManager(Menu mainMenu)
		{
			_mainMenu = mainMenu;
			_fileMenuItem = AddRootMenuItem("_File");
			_viewMenuItem = AddRootMenuItem("_View");
			_repositoryMenuItem = AddRootMenuItem("_Repository");
			_windowMenuItem = AddRootMenuItem("_Window");
			_aboutMenuItem = AddRootMenuItem("_Help");
		}

		public void Initialize()
		{
			WeakEventManager<NotificationCenter, EventArgs<ClosableTabItem>>.AddHandler(NotificationCenter.Current, "ActiveTabChanged", ActiveTabChanged);
			RefreshRepositoryItemState();
		}

		public void ApplyLocalization()
		{
			_fileMenuItem.Header = PreferencesLocalization.Translate("_File", ForkPlusSettings.Default.UiLanguage);
			_viewMenuItem.Header = PreferencesLocalization.Translate("_View", ForkPlusSettings.Default.UiLanguage);
			_repositoryMenuItem.Header = PreferencesLocalization.Translate("_Repository", ForkPlusSettings.Default.UiLanguage);
			_windowMenuItem.Header = PreferencesLocalization.Translate("_Window", ForkPlusSettings.Default.UiLanguage);
			_aboutMenuItem.Header = PreferencesLocalization.Translate("_Help", ForkPlusSettings.Default.UiLanguage);
		}

		private void ActiveTabChanged(object sender, EventArgs<ClosableTabItem> args)
		{
			RefreshRepositoryItemState();
		}

		private void RefreshRepositoryItemState()
		{
			Visibility visibility = Visibility.Collapsed;
			try
			{
				RepositoryUserControl activeRepositoryUserControl = Application.Current.TabManager().ActiveRepositoryUserControl;
				if (activeRepositoryUserControl != null)
				{
					visibility = Visibility.Visible;
				}
			}
			finally
			{
				_viewMenuItem.Visibility = visibility;
				_repositoryMenuItem.Visibility = visibility;
			}
		}

		private MenuItem AddRootMenuItem(string header)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = PreferencesLocalization.Translate(header, ForkPlusSettings.Default.UiLanguage)
			};
			menuItem.Items.Add(new MenuItem());
			menuItem.SubmenuOpened += RootMenuItem_SubmenuOpened;
			_mainMenu.Items.Add(menuItem);
			return menuItem;
		}

		private void RootMenuItem_SubmenuOpened(object sender, RoutedEventArgs e)
		{
			if (!(e.Source is MenuItem menuItem))
			{
				return;
			}
			if (menuItem == _fileMenuItem)
			{
				_fileMenuItem.SetItems(CreateFileMenuItems());
			}
			else if (menuItem == _viewMenuItem)
			{
				RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
				GitModule gitModule = activeRepositoryUserControl?.GitModule;
				if (gitModule != null)
				{
					_viewMenuItem.SetItems(CreateViewMenuItems(activeRepositoryUserControl, gitModule));
				}
			}
			else if (menuItem == _repositoryMenuItem)
			{
				RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
				GitModule gitModule = activeRepositoryUserControl?.GitModule;
				RepositoryData repositoryData = activeRepositoryUserControl?.RepositoryData;
				if (gitModule != null && repositoryData != null)
				{
					_repositoryMenuItem.SetItems(CreateRepositoryMenuItems(activeRepositoryUserControl, gitModule, repositoryData));
				}
			}
			else if (menuItem == _windowMenuItem)
			{
				_windowMenuItem.SetItems(CreateWindowMenuItems());
			}
			else if (menuItem == _aboutMenuItem)
			{
				_aboutMenuItem.SetItems(CreateAboutMenuItems());
			}
		}

		private static IEnumerable<Control> CreateFileMenuItems()
		{
			MainWindowCommands commands = MainWindow.Commands;
			yield return commands.ShowCreateRepositoryWindow.CreateMenuItem(delegate { commands.ShowCreateRepositoryWindow.Execute(); });
			yield return commands.ShowCloneWindow.CreateMenuItem(delegate { commands.ShowCloneWindow.Execute(); });
			yield return commands.ShowInitGitMmRepositoryWindow.CreateMenuItem(delegate { commands.ShowInitGitMmRepositoryWindow.Execute(); });
			yield return new Separator();
			yield return commands.NewTab.CreateMenuItem(delegate { commands.NewTab.Execute(); });
			yield return commands.OpenRepository.CreateMenuItem(delegate { commands.OpenRepository.Execute(); });
			yield return commands.ShowQuickLaunchWindow.CreateMenuItem(delegate { commands.ShowQuickLaunchWindow.Execute(); });
			yield return commands.CloseActiveTab.CreateMenuItem(delegate { commands.CloseActiveTab.Execute(); });
			yield return new Separator();
			yield return commands.ShowConfigureSSHKeysWindow.CreateMenuItem(delegate { commands.ShowConfigureSSHKeysWindow.Execute(); });
			yield return commands.ShowAccountsWindow.CreateMenuItem(delegate { commands.ShowAccountsWindow.Execute(); });
			yield return new Separator();
			yield return commands.ShowPreferencesWindow.CreateMenuItem(delegate { commands.ShowPreferencesWindow.Execute(); });
			yield return new Separator();
			yield return commands.ExitApplication.CreateMenuItem(delegate { commands.ExitApplication.Execute(); });
		}

		private static IEnumerable<Control> CreateViewMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			MainWindowCommands commands = MainWindow.Commands;
			yield return commands.ActivateCommitView.CreateMenuItem(delegate { commands.ActivateCommitView.Execute(); });
			yield return commands.ActivateRevisionList.CreateMenuItem(delegate { commands.ActivateRevisionList.Execute(); });
			yield return new Separator();
			yield return commands.ShowHead.CreateMenuItem(delegate { commands.ShowHead.Execute(); });
			yield return new Separator();

			MenuItem hideTagsMenuItem = commands.ToggleHideTags.CreateMenuItem(delegate { commands.ToggleHideTags.Execute(); });
			hideTagsMenuItem.IsChecked = gitModule.Settings.HideTags;
			yield return hideTagsMenuItem;

			MenuItem hideStashesMenuItem = commands.ToggleHideStashesInRevisionList.CreateMenuItem(delegate { commands.ToggleHideStashesInRevisionList.Execute(); });
			hideStashesMenuItem.IsChecked = gitModule.Settings.HideStashesInRevisionList;
			yield return hideStashesMenuItem;

			bool commitViewMode = repositoryUserControl.ViewMode == RepositoryViewMode.CommitViewMode;
			string reflogHeader = commitViewMode ? "Show Ignored Files" : "Show Lost Commits (Reflog)";
			MenuItem reflogMenuItem = commands.ToggleShowReflogInRevisionList.CreateMenuItem(reflogHeader, delegate { commands.ToggleShowReflogInRevisionList.Execute(); });
			reflogMenuItem.IsChecked = commitViewMode ? repositoryUserControl.Content.CommitUserControl.ShowIgnoredFiles : repositoryUserControl.ShowReflogInRevisionList;
			yield return reflogMenuItem;

			MenuItem collapseMergeMenuItem = commands.ToggleCollapseAllMergeRevisions.CreateMenuItem(delegate { commands.ToggleCollapseAllMergeRevisions.Execute(); });
			collapseMergeMenuItem.IsChecked = gitModule.Settings.CollapseAllMergeRevisions;
			yield return collapseMergeMenuItem;

			string filterHeader = gitModule.Settings.FilterReferences.Length != 0 ? "Clear Branch Filter" : "Filter by Active Branch";
			yield return commands.ToggleReferenceFilter.CreateMenuItem(filterHeader, delegate { commands.ToggleReferenceFilter.Execute(); });
		}

		private static IEnumerable<Control> CreateRepositoryMenuItems(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData)
		{
			MainWindowCommands commands = MainWindow.Commands;
			repositoryUserControl.SubmodulesToUpdate();

			yield return commands.RefreshRepositoryData.CreateMenuItem(delegate { commands.RefreshRepositoryData.Execute(); });

			if (!File.Exists(gitModule.MakePath(".gitignore")))
			{
				yield return new Separator();
				yield return RepositoryUserControl.Commands.ShowAddGitignoreTemplateWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowAddGitignoreTemplateWindow.Execute(repositoryUserControl);
				});
			}

			yield return new Separator();
			yield return commands.ShowFetchWindow.CreateMenuItem(delegate { commands.ShowFetchWindow.Execute(repositoryUserControl, gitModule); });
			yield return commands.ShowPullWindow.CreateMenuItem(delegate { commands.ShowPullWindow.Execute(repositoryUserControl); });
			yield return commands.ShowPushWindow.CreateMenuItem(delegate { commands.ShowPushWindow.Execute(repositoryUserControl); });
			yield return new Separator();
			yield return commands.ShowSaveStashWindow.CreateMenuItem(delegate { commands.ShowSaveStashWindow.Execute(repositoryUserControl, gitModule); });
			yield return new Separator();
			yield return commands.ShowCreateBranchWindow.CreateMenuItem(delegate { commands.ShowCreateBranchWindow.Execute(repositoryUserControl, null); });
			yield return commands.ShowCreateTagWindow.CreateMenuItem(delegate { commands.ShowCreateTagWindow.Execute(repositoryUserControl, null); });
			yield return commands.ShowCreateWorktreeWindow.CreateMenuItem(delegate { commands.ShowCreateWorktreeWindow.Execute(repositoryUserControl); });
			yield return new Separator();
			yield return CreateGitFlowMenuItem(repositoryUserControl, gitModule, repositoryData);
			yield return CreateGitLfsMenuItem(repositoryUserControl, gitModule, repositoryData.GitLfsInitialized);
			yield return new Separator();
			yield return RepositoryUserControl.Commands.ApplyPatch.CreateMenuItem(delegate { RepositoryUserControl.Commands.ApplyPatch.Execute(repositoryUserControl); });
			yield return new Separator();
			yield return RepositoryUserControl.Commands.Bisect.CreateMenuItem(delegate { RepositoryUserControl.Commands.Bisect.Execute(repositoryUserControl, BisectGitCommand.BisectCommand.Start); });
			yield return new Separator();
			yield return commands.OpenRepositoryInFileExplorer.CreateMenuItem(delegate { commands.OpenRepositoryInFileExplorer.Execute(gitModule); });
			yield return commands.OpenRepositoryInShellTool.CreateMenuItem(delegate { commands.OpenRepositoryInShellTool.Execute(gitModule); });
			yield return new Separator();
			yield return RepositoryUserControl.Commands.ShowRepositoryStatisticsWindow.CreateMenuItem(delegate { RepositoryUserControl.Commands.ShowRepositoryStatisticsWindow.Execute(gitModule); });
			yield return RepositoryUserControl.Commands.ShowRepositoryOverviewWindow.CreateMenuItem(delegate { RepositoryUserControl.Commands.ShowRepositoryOverviewWindow.Execute(repositoryUserControl, gitModule); });
			yield return commands.ShowBenchmarkWindow.CreateMenuItem(delegate { commands.ShowBenchmarkWindow.Execute(repositoryUserControl); });
			yield return new Separator();
			yield return RepositoryUserControl.Commands.ShowRepositorySettingsWindow.CreateMenuItem(delegate { RepositoryUserControl.Commands.ShowRepositorySettingsWindow.Execute(gitModule, repositoryData); });

			CustomCommand[] repositoryCustomCommands = CustomCommandManager.Current.GetCustomCommands(repositoryData, CustomCommandTarget.Repository);
			if (repositoryCustomCommands.Length == 0)
			{
				yield break;
			}

			yield return new Separator();
			List<MenuItem> customCommandMenuItems = new List<MenuItem>();
			foreach (CustomCommand customCommand in repositoryCustomCommands)
			{
				if (customCommand.OS.IsSupported())
				{
					CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule);
					customCommand.AddCustomCommandItem(repositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, customCommandMenuItems);
				}
			}
			foreach (MenuItem menuItem in customCommandMenuItems)
			{
				yield return menuItem;
			}
		}

		private static IEnumerable<Control> CreateWindowMenuItems()
		{
			MainWindowCommands commands = MainWindow.Commands;
			yield return commands.SelectPreviousTab.CreateMenuItem(delegate { commands.SelectPreviousTab.Execute(); });
			yield return commands.SelectNextTab.CreateMenuItem(delegate { commands.SelectNextTab.Execute(); });
			yield return new Separator();
			yield return commands.SwitchApplicationTheme.CreateMenuItem(delegate { commands.SwitchApplicationTheme.Execute(); });
			yield return new Separator();
			yield return commands.IncreaseLayoutScale.CreateMenuItem(delegate { commands.IncreaseLayoutScale.Execute(); });
			yield return commands.DecreaseLayoutScale.CreateMenuItem(delegate { commands.DecreaseLayoutScale.Execute(); });
		}

		private static IEnumerable<Control> CreateDevelopMenuItems()
		{
			MainWindowCommands commands = MainWindow.Commands;
			MenuItem refreshMenuItem = commands.ToggleRefreshOnActivate.CreateMenuItem(delegate { commands.ToggleRefreshOnActivate.Execute(); });
			refreshMenuItem.IsChecked = ForkPlusSettings.Default.DisableRefreshOnAppActivation;
			yield return refreshMenuItem;

			MenuItem traceMenuItem = commands.ToggleTraceElapsedTime.CreateMenuItem(delegate { commands.ToggleTraceElapsedTime.Execute(); });
			traceMenuItem.IsChecked = ForkPlusSettings.Default.LogElapsedTime;
			yield return traceMenuItem;

			yield return new Separator();
			yield return commands.OpenApplicationDataDirectory.CreateMenuItem(delegate { commands.OpenApplicationDataDirectory.Execute(); });
		}

		private static IEnumerable<Control> CreateAboutMenuItems()
		{
			MainWindowCommands commands = MainWindow.Commands;
			yield return commands.UpdateApplication.CreateMenuItem(delegate { commands.UpdateApplication.Execute(); });
			yield return new Separator();
			yield return commands.OpenKeyboardShortcuts.CreateMenuItem(delegate { commands.OpenKeyboardShortcuts.Execute(); });
			yield return commands.ShowPerformanceDiagnosticsWindow.CreateMenuItem(delegate { commands.ShowPerformanceDiagnosticsWindow.Execute(); });
			yield return new Separator();
			yield return commands.ShowAboutWindow.CreateMenuItem(delegate { commands.ShowAboutWindow.Execute(); });
		}

		private static Control CreateGitLfsMenuItem(RepositoryUserControl repositoryUserControl, GitModule gitModule, bool gitLfsInitialized)
		{
			MenuItem menuItem = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Git LFS")
			};
			if (!gitLfsInitialized)
			{
				menuItem.Items.Add(RepositoryUserControl.Commands.InitGitLfs.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.InitGitLfs.Execute(repositoryUserControl, gitModule);
				}));
				return menuItem;
			}

			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsTrackWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitLfsTrackWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(new Separator());
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsStatusWindow.CreateMenuItem("Status (Locks)...", delegate
			{
				RepositoryUserControl.Commands.ShowGitLfsStatusWindow.Execute(repositoryUserControl);
			}));
			menuItem.Items.Add(new Separator());
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsFetchWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitLfsFetchWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitLfsPullWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitLfsPullWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(new Separator());
			menuItem.Items.Add(RepositoryUserControl.Commands.GitLfsPrune.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.GitLfsPrune.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(new Separator());
			menuItem.Items.Add(RepositoryUserControl.Commands.DeinitializeGitLfs.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.DeinitializeGitLfs.Execute(repositoryUserControl, gitModule);
			}));
			return menuItem;
		}

		private static Control CreateGitFlowMenuItem(RepositoryUserControl repositoryUserControl, GitModule gitModule, RepositoryData repositoryData)
		{
			LocalBranch activeLocalBranch = repositoryData.References.ActiveBranch;
			MenuItem menuItem = new MenuItem
			{
				Header = PreferencesLocalization.MenuHeader("Git Flow")
			};
			if (repositoryData.GitFlowSettings == null)
			{
				menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowInitWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowInitWindow.Execute(repositoryUserControl, gitModule);
				}));
				return menuItem;
			}

			if (activeLocalBranch != null && activeLocalBranch.IsFeatureBranch(repositoryData.GitFlowSettings))
			{
				menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.CreateMenuItem("Finish '" + activeLocalBranch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowFinishFeatureWindow.Execute(repositoryUserControl, gitModule, repositoryData, activeLocalBranch);
				}));
			}
			else if (activeLocalBranch != null && activeLocalBranch.IsReleaseBranch(repositoryData.GitFlowSettings))
			{
				menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.CreateMenuItem("Finish '" + activeLocalBranch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowFinishReleaseWindow.Execute(repositoryUserControl, gitModule, repositoryData, activeLocalBranch);
				}));
			}
			else if (activeLocalBranch != null && activeLocalBranch.IsHotfixBranch(repositoryData.GitFlowSettings))
			{
				menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.CreateMenuItem("Finish '" + activeLocalBranch.Name + "'...", delegate
				{
					RepositoryUserControl.Commands.ShowGitFlowFinishHotfixWindow.Execute(repositoryUserControl, gitModule, repositoryData, activeLocalBranch);
				}));
			}
			if (menuItem.Items.Count > 0)
			{
				menuItem.Items.Add(new Separator());
			}
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartFeatureWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartReleaseWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowGitFlowStartHotfixWindow.Execute(repositoryUserControl, gitModule);
			}));
			menuItem.Items.Add(new Separator());
			menuItem.Items.Add(RepositoryUserControl.Commands.DeinitializeGitFlow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.DeinitializeGitFlow.Execute(repositoryUserControl, gitModule);
			}));
			return menuItem;
		}
	}
}
