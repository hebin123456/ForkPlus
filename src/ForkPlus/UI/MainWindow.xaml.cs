using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shell;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.QuickLaunch;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using NLog;
using NLog.Targets;
using ForkPlus.UI.Helpers;
using ForkPlus.Services;

namespace ForkPlus.UI
{
	[TemplatePart(Name = "PART_MainMenu", Type = typeof(Menu))]
	[TemplatePart(Name = "Part_NotificationManagerToggleButton", Type = typeof(ToggleButton))]
	public partial class MainWindow : CustomWindow, ILocalizableControl
	{
		private const string PartNameMainMenu = "PART_MainMenu";

		private const string PartNameNotificationManagerToggleButton = "Part_NotificationManagerToggleButton";

		public static readonly MainWindowCommands Commands = new MainWindowCommands();

		private MainWindowMenuManager _menuManager;

		private bool _startUpFinished;

		private Menu _templatePartMainMenu;

		private ToggleButton _templatePartNotificationManagerToggleButton;

		private readonly AutomaticBackgroundFetchManager _automaticBackgroundFetchManager = new AutomaticBackgroundFetchManager();

		private readonly RepositoryStatusManager _repositoryStatusManager = new RepositoryStatusManager();

		private bool _preventRefreshAfterChildDialogClose;

		private string _preventRefreshAfterChildDialogCloseReason;

		private DateTime _lastActivationStatusRefreshTime = DateTime.MinValue;

		private string _lastActivationStatusRefreshRepositoryPath;

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		[Null]
		public static MainWindow Instance => Application.Current.MainWindow as MainWindow;

		[Null]
		public static RepositoryUserControl ActiveRepositoryUserControl => Instance?.TabManager.ActiveRepositoryUserControl;

		public TabManager TabManager { get; }

		public JobQueue JobQueue { get; }

		public MainWindow()
		{
			bool flag = global::ForkPlus.DesignTimeHelper.IsInDesignMode();
			if (!flag)
			{
				Application.Current?.RefreshLayoutScaling();
				StartupTimeReporter.MainWindowCreated();
				foreach (Target configuredNamedTarget in LogManager.Configuration.ConfiguredNamedTargets)
				{
					Log.Info("Log target: " + configuredNamedTarget.Name);
				}
				base.Closed += delegate
				{
					Application.Current.Shutdown();
				};
			}
			InitializeComponent();
			base.IsTitleVisible = true;
			if (flag)
			{
				base.Title = App.AppName ?? "Fork";
				return;
			}
			JobQueue = new JobQueue();
			Toolbar.Initialize(this);
			RefreshTitle();
			base.SizeChanged += MainWindow_SizeChanged;
			Application.Current.MainWindow = this;
			TabManager = new TabManager(TabControl);
		}

		public void ApplyLocalization()
		{
			_menuManager?.ApplyLocalization();
			Toolbar.ApplyLocalization();
			TabManager?.RefreshTabTitles();
			if (_templatePartNotificationManagerToggleButton != null)
			{
				_templatePartNotificationManagerToggleButton.ToolTip = PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage);
			}
			RepositoryUserControl activeRepositoryUserControl = ActiveRepositoryUserControl;
			if (activeRepositoryUserControl != null)
			{
				activeRepositoryUserControl.ApplyLocalization();
			}
			TabManager?.ActiveRepositoryManager?.ApplyLocalization();
			TabManager?.ActiveGitMmUserControl?.ApplyLocalization();
			foreach (Window window in Application.Current.Windows)
			{
				if (window != this && window is ILocalizableControl localizableControl)
				{
					localizableControl.ApplyLocalization();
				}
			}
		}

		public void PreventRefreshAfterChildDialogClose(string reason)
		{
			_preventRefreshAfterChildDialogCloseReason = reason;
			_preventRefreshAfterChildDialogClose = true;
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			if (IsDesignMode)
			{
				return;
			}
			if (base.Template.TryFindName<Menu>("PART_MainMenu", this, out _templatePartMainMenu))
			{
				_templatePartMainMenu.SetValue(WindowChrome.IsHitTestVisibleInChromeProperty, true);
				_menuManager = new MainWindowMenuManager(_templatePartMainMenu);
			}
			if (base.Template.TryFindName<ToggleButton>("Part_NotificationManagerToggleButton", this, out _templatePartNotificationManagerToggleButton))
			{
				NotificationManager.Current.IsActiveChanged += delegate
				{
					_templatePartNotificationManagerToggleButton.Hide(!NotificationManager.Current.IsActive);
				};
				_templatePartNotificationManagerToggleButton.Hide(!NotificationManager.Current.IsActive);
				_templatePartNotificationManagerToggleButton.ToolTip = PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage);
			}
		}

		public void RefreshTitle()
		{
			if (IsDesignMode)
			{
				base.Title = App.AppName ?? "Fork";
				return;
			}
			string text = (App.IsDebug ? (App.AppName + " [DEBUG]") : App.AppName);
			base.Title = (ForkPlusSettings.Default.Workspaces.ShowInTitle ? (text + " - " + ForkPlusSettings.Default.Workspaces.ActiveWorkspace.Name) : text);
		}

		public void RefreshRepositoriesStatus()
		{
			_repositoryStatusManager.Refresh();
		}

		public void ShowNotificationManager()
		{
			_templatePartNotificationManagerToggleButton.IsChecked = true;
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			WindowLocationState windowLocationState = ForkPlusSettings.Default.MainWindowLocationState;
			if (windowLocationState.WindowState == WindowState.Minimized)
			{
				windowLocationState = new WindowLocationState(windowLocationState.Left, windowLocationState.Top, windowLocationState.Width, windowLocationState.Height, WindowState.Normal);
			}
			this.SetWindowLocationState(windowLocationState);
			if (windowLocationState.WindowState == WindowState.Maximized)
			{
				base.WindowState = WindowState.Maximized;
			}
		}

		protected override void OnKeyUp(KeyEventArgs e)
		{
			if (e.Key == Key.F && KeyboardHelper.IsCtrlDown && KeyboardHelper.IsAltDown && KeyboardHelper.IsShiftDown)
			{
				e.Handled = true;
				RepositoryUserControl activeRepositoryUserControl = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl != null)
				{
					Commands.QuickFetch.Execute(activeRepositoryUserControl, activeRepositoryUserControl.GitModule);
				}
			}
			else
			{
				base.OnKeyUp(e);
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.O && Keyboard.IsKeyDown(Key.LeftCtrl) && Keyboard.IsKeyDown(Key.LeftAlt))
			{
				e.Handled = true;
				RepositoryUserControl activeRepositoryUserControl = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl != null)
				{
					Commands.OpenRepositoryInFileExplorer.Execute(activeRepositoryUserControl.GitModule);
				}
				return;
			}
			if (e.Key == Key.V && KeyboardHelper.IsCtrlDown)
			{
				RepositoryUserControl activeRepositoryUserControl2 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl2 != null)
				{
					string text = ServiceLocator.Clipboard.GetText();
					if (text != null && (text.StartsWith("diff ") || text.StartsWith("From ")))
					{
						e.Handled = true;
						byte[] bytes = Encoding.UTF8.GetBytes(text);
						new ShowApplyPatchWindowCommand().Execute(activeRepositoryUserControl2, bytes);
						return;
					}
				}
			}
			base.OnKeyDown(e);
		}

		private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.MainWindowLocationState = this.GetWindowLocationState();
			}
		}

		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.MainWindowLocationState = this.GetWindowLocationState();
			}
		}

		protected override void OnDrop(DragEventArgs e)
		{
			base.OnDrop(e);
			if (e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)
			{
				string[] array2 = array;
				foreach (string path in array2)
				{
					TabManager.OpenRepository(path);
				}
				e.Handled = true;
			}
		}

		private void ForkWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			StartupTimeReporter.MainWindowLoaded();
			_menuManager.Initialize();
			InitializeKeyBindings();
			TabManager.RestoreSession();
			Toolbar.RefreshWorkspacesButton();
			RefreshTitle();
			RefreshRepositoriesStatus();
			App.CliArguments.RunCommand();
			base.Dispatcher.Async(StartupTimeReporter.UIReady);
		}

		private void InitializeKeyBindings()
		{
			base.CommandBindings.Add(Commands.ActivateCommitView.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateCommitView.Execute();
			}));
			base.CommandBindings.Add(Commands.ActivateRevisionList.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateRevisionList.Execute();
			}));
			base.CommandBindings.Add(Commands.ActivateRepositoryTab.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateRepositoryTab.Execute();
			}));
			base.CommandBindings.Add(Commands.ActivateSearchTab.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateSearchTab.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowHead.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowHead.Execute();
			}));
			base.CommandBindings.Add(Commands.ToggleShowReflogInRevisionList.CreateShortcutCommandBinding(delegate
			{
				Commands.ToggleShowReflogInRevisionList.Execute();
			}));
			base.CommandBindings.Add(Commands.CloseActiveTab.CreateShortcutCommandBinding(delegate
			{
				Commands.CloseActiveTab.Execute();
			}));
			base.CommandBindings.Add(Commands.NewTab.CreateShortcutCommandBinding(delegate
			{
				Commands.NewTab.Execute();
			}));
			base.CommandBindings.Add(Commands.OpenRepository.CreateShortcutCommandBinding(delegate
			{
				Commands.OpenRepository.Execute();
			}));
			base.CommandBindings.Add(Commands.RefreshRepositoryData.CreateShortcutCommandBinding(delegate
			{
				Commands.RefreshRepositoryData.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowCloneWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowCloneWindow.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowInitGitMmRepositoryWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowInitGitMmRepositoryWindow.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowCreateBranchWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl8 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl8 != null)
				{
					Commands.ShowCreateBranchWindow.Execute(activeRepositoryUserControl8, null);
				}
			}));
			base.CommandBindings.Add(Commands.ShowCreateRepositoryWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowCreateRepositoryWindow.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowCreateTagWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl7 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl7 != null)
				{
					Commands.ShowCreateTagWindow.Execute(activeRepositoryUserControl7, null);
				}
			}));
			base.CommandBindings.Add(Commands.ShowFetchWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl6 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl6 != null)
				{
					Commands.ShowFetchWindow.Execute(activeRepositoryUserControl6, activeRepositoryUserControl6.GitModule);
				}
			}));
			base.CommandBindings.Add(Commands.ShowQuickLaunchWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowQuickLaunchWindow.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowQuickLaunchCheckoutWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowQuickLaunchCheckoutWindow.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowPullWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowPullWindow.Execute(TabManager.ActiveRepositoryUserControl);
			}));
			base.CommandBindings.Add(Commands.QuickPull.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl5 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl5 != null)
				{
					Commands.QuickPull.Execute(activeRepositoryUserControl5);
				}
			}));
			base.CommandBindings.Add(Commands.ShowPushWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl4 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl4 != null)
				{
					Commands.ShowPushWindow.Execute(activeRepositoryUserControl4);
				}
			}));
			base.CommandBindings.Add(Commands.QuickPush.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl3 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl3 != null)
				{
					Commands.QuickPush.Execute(activeRepositoryUserControl3);
				}
			}));
			base.CommandBindings.Add(Commands.SelectNextTab.CreateShortcutCommandBinding(delegate
			{
				Commands.SelectNextTab.Execute();
			}));
			base.CommandBindings.Add(Commands.SelectPreviousTab.CreateShortcutCommandBinding(delegate
			{
				Commands.SelectPreviousTab.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowSaveStashWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl2 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl2 != null)
				{
					Commands.ShowSaveStashWindow.Execute(activeRepositoryUserControl2, activeRepositoryUserControl2.GitModule);
				}
			}));
			base.CommandBindings.Add(Commands.OpenRepositoryInShellTool.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl != null)
				{
					Commands.OpenRepositoryInShellTool.Execute(activeRepositoryUserControl.GitModule);
				}
			}));
			base.CommandBindings.Add(Commands.ToggleReferenceFilter.CreateShortcutCommandBinding(delegate
			{
				Commands.ToggleReferenceFilter.Execute();
			}));
			base.CommandBindings.Add(Commands.IncreaseLayoutScale.CreateShortcutCommandBinding(delegate
			{
				Commands.IncreaseLayoutScale.Execute();
			}));
			base.CommandBindings.Add(Commands.DecreaseLayoutScale.CreateShortcutCommandBinding(delegate
			{
				Commands.DecreaseLayoutScale.Execute();
			}));
			base.CommandBindings.Add(Commands.ShowPreferencesWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowPreferencesWindow.Execute();
			}));
		}

		private void Window_Closing(object sender, CancelEventArgs e)
		{
			ForkPlusSettings.Default.MainWindowLocationState = this.GetWindowLocationStateX();
			TabManager.SaveSession();
			ForkPlusSettings.Default.Save();
		}

		private void Window_Activated(object sender, EventArgs e)
		{
			Log.Info("WindowActivated");
			if (!_startUpFinished)
			{
				_startUpFinished = true;
				return;
			}
			if (_preventRefreshAfterChildDialogClose || ChildDialogsAreNotAlreadyClosed())
			{
				Log.Info("Application Window Activated: skip (" + _preventRefreshAfterChildDialogCloseReason + ")");
				_preventRefreshAfterChildDialogCloseReason = null;
				_preventRefreshAfterChildDialogClose = false;
				return;
			}
			if (!ForkPlusSettings.Default.DisableRefreshOnAppActivation)
			{
				Log.Info("Application Window Activated");
				if (!RefreshActiveCommitViewStatus())
				{
					RepositoryUserControl activeRepositoryUserControl = TabManager.ActiveRepositoryUserControl;
					string repositoryPath = activeRepositoryUserControl?.GitModule?.Path ?? "";
					if (!ShouldSkipActivationRefresh(repositoryPath))
					{
						TabControl.SelectedTab?.Refresh();
						RefreshRepositoriesStatus();
					}
				}
			}
			if (ShowNewYearNotification.NotificationRequired)
			{
				new ShowNewYearNotification().Execute();
			}
		}

		private bool ShouldSkipActivationRefresh(string repositoryPath)
		{
			DateTime now = DateTime.UtcNow;
			if (string.Equals(repositoryPath, _lastActivationStatusRefreshRepositoryPath, StringComparison.OrdinalIgnoreCase) && now - _lastActivationStatusRefreshTime < TimeSpan.FromSeconds(10.0))
			{
				return true;
			}
			_lastActivationStatusRefreshRepositoryPath = repositoryPath;
			_lastActivationStatusRefreshTime = now;
			return false;
		}

		private bool RefreshActiveCommitViewStatus()
		{
			RepositoryUserControl activeRepositoryUserControl = TabManager.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl?.ViewMode != RepositoryViewMode.CommitViewMode)
			{
				return false;
			}
			string repositoryPath = activeRepositoryUserControl.GitModule?.Path ?? "";
			if (ShouldSkipActivationRefresh(repositoryPath))
			{
				return true;
			}
			activeRepositoryUserControl.InvalidateAndRefresh(SubDomain.Status, null, RepositoryViewMode.CommitViewMode);
			return true;
		}

		private bool ChildDialogsAreNotAlreadyClosed()
		{
			foreach (object ownedWindow in base.OwnedWindows)
			{
				if (ownedWindow is QuickLaunchWindow)
				{
					_preventRefreshAfterChildDialogCloseReason = ownedWindow.GetType().Name;
					return true;
				}
			}
			return false;
		}

	}
}
