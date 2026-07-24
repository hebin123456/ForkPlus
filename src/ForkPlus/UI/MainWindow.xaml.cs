using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.QuickLaunch;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using NLog;
using NLog.Targets;

namespace ForkPlus.UI
{
	[TemplatePart(Name = "PART_MainMenu", Type = typeof(Menu))]
	[TemplatePart(Name = "Part_NotificationManagerToggleButton", Type = typeof(ToggleButton))]
	[TemplatePart(Name = "NotificationManagerUserControl", Type = typeof(NotificationManagerUserControl))]
	public partial class MainWindow : CustomWindow, ILocalizableControl
	{
		private const string PartNameMainMenu = "PART_MainMenu";

		private const string PartNameNotificationManagerToggleButton = "Part_NotificationManagerToggleButton";

		private const string PartNameNotificationManagerUserControl = "NotificationManagerUserControl";

		public static readonly MainWindowCommands Commands = new MainWindowCommands();

		private MainWindowMenuManager _menuManager;

		private Menu _templatePartMainMenu;

		private ToggleButton _templatePartNotificationManagerToggleButton;

		private NotificationManagerUserControl _templatePartNotificationManagerUserControl;

		private readonly AutomaticBackgroundFetchManager _automaticBackgroundFetchManager = new AutomaticBackgroundFetchManager();

		private readonly UpdateCheckManager _updateCheckManager = new UpdateCheckManager();

		private readonly RepositoryStatusManager _repositoryStatusManager = new RepositoryStatusManager();

		// 阶段 3 里程碑 3.15：纯业务状态/逻辑由 VM 承载（零 WPF），本类保留薄转发。
		private readonly MainWindowViewModel _viewModel = new MainWindowViewModel();

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		[Null]
		public static MainWindow Instance => (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

		[Null]
		public static RepositoryUserControl ActiveRepositoryUserControl => Instance?.TabManager.ActiveRepositoryUserControl;

		public TabManager TabManager { get; }

		public JobQueue JobQueue { get; }

		public MainWindow()
		{
			bool flag = global::ForkPlus.DesignTimeHelper.IsInDesignMode();
			if (!flag)
			{
				ServiceLocator.WindowManager.RefreshLayoutScaling();
				StartupTimeReporter.MainWindowCreated();
				foreach (Target configuredNamedTarget in LogManager.Configuration.ConfiguredNamedTargets)
				{
					Log.Info("Log target: " + configuredNamedTarget.Name);
				}
				base.Closed += delegate
				{
					// Avalonia: Shutdown 由 IClassicDesktopStyleApplicationLifetime 提供（替代 WPF Application.Shutdown）
					(Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
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
			// Avalonia: Window 没有 SizeChanged 事件，改用 Resized（尺寸）+ PositionChanged（位置）。
			// 纯状态切换（最大化↔正常）由 OnWindowStateChanged 兜底保存。
			base.Resized += (_, _) => SaveMainWindowLocationState();
			base.PositionChanged += (_, _) => SaveMainWindowLocationState();
			if (Application.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = this;
			}
			TabManager = new TabManager(TabControl);
			// 阶段 4.5：初始化键盘修饰键状态跟踪（替代 WPF Keyboard.IsKeyDown 静态查询）。
			KeyboardHelper.Initialize(this);
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
		// 通知按钮弹出面板的 HeaderLabel 也需要随语言切换刷新；本控件实例在 ControlTemplate
		// 内一次性构造，构造函数里的翻译只生效一次，之前必须重启客户端才更新（Bug v2.1.2）。
		_templatePartNotificationManagerUserControl?.ApplyLocalization();
		RepositoryUserControl activeRepositoryUserControl = ActiveRepositoryUserControl;
		if (activeRepositoryUserControl != null)
		{
			activeRepositoryUserControl.ApplyLocalization();
		}
		TabManager?.ActiveRepositoryManager?.ApplyLocalization();
		TabManager?.ActiveGitMmUserControl?.ApplyLocalization();
		// TODO: Avalonia 迁移 - Application.Current.Windows 改用 desktop lifetime 的 Windows 列表
		var windows = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows;
		if (windows != null)
		{
			foreach (Window window in windows)
			{
				if (window != this && window is ILocalizableControl localizableControl)
				{
					localizableControl.ApplyLocalization();
				}
			}
		}
	}

		public void PreventRefreshAfterChildDialogClose(string reason)
		{
			_viewModel.PreventRefreshAfterChildDialogCloseWithReason(reason);
		}

		public override void OnApplyTemplate(TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (IsDesignMode)
			{
				return;
			}
			// TODO: Avalonia 迁移 - MainWindow.xaml 已移除 ControlTemplate，PART_MainMenu /
			// Part_NotificationManagerToggleButton / NotificationManagerUserControl 不再存在于模板中
			// （标题栏按钮由 CustomWindow 基类处理）。菜单与通知弹出 UI 需重新设计。
			// 下面用 NameScope.Find 安全查找（找不到返回 null），保留原有逻辑结构。
			_templatePartMainMenu = e.NameScope.Find(PartNameMainMenu) as Menu;
			if (_templatePartMainMenu != null)
			{
				// WindowChrome.IsHitTestVisibleInChromeProperty 已移除（Avalonia 由 ExtendClientArea 处理标题栏命中测试）
				_menuManager = new MainWindowMenuManager(_templatePartMainMenu);
			}
			_templatePartNotificationManagerToggleButton = e.NameScope.Find(PartNameNotificationManagerToggleButton) as ToggleButton;
			if (_templatePartNotificationManagerToggleButton != null)
			{
				NotificationManager.Current.IsActiveChanged += delegate
				{
					_templatePartNotificationManagerToggleButton.Hide(!NotificationManager.Current.IsActive);
				};
				_templatePartNotificationManagerToggleButton.Hide(!NotificationManager.Current.IsActive);
				// TODO: NotificationManagerPopup 已移至基类或需重新设计
				_templatePartNotificationManagerToggleButton.ToolTip = PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage);
			}
			// 缓存 ControlTemplate 内的 NotificationManagerUserControl 引用，
			// ApplyLocalization 时调用其 ApplyLocalization() 刷新 HeaderLabel.Text（Bug v2.1.2）。
			_templatePartNotificationManagerUserControl = e.NameScope.Find(PartNameNotificationManagerUserControl) as NotificationManagerUserControl;
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
			// TODO: NotificationManagerPopup 已移至基类或需重新设计
			if (_templatePartNotificationManagerToggleButton != null)
			{
				_templatePartNotificationManagerToggleButton.IsChecked = true;
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
			if (e.Key == Key.O && Keyboard.Modifiers.HasFlag(KeyModifiers.Control) && Keyboard.Modifiers.HasFlag(KeyModifiers.Alt))
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
				// 阶段 3 里程碑 3.15：剪贴板 patch 检测+编码纯逻辑由 VM 承载，View 仅负责 WPF 事件+命令执行。
				byte[] bytes = MainWindowViewModel.TryGetClipboardPatchBytes();
				if (bytes != null)
				{
					e.Handled = true;
					new ShowApplyPatchWindowCommand().Execute(activeRepositoryUserControl2, bytes);
					return;
				}
			}
		}
		base.OnKeyDown(e);
	}

		// Avalonia: 尺寸/位置变化由 Resized/PositionChanged 事件回调本方法保存状态。
		// TODO: Avalonia 迁移 - GetWindowLocationState 依赖 WindowLocationStateExtensions（Win32 WindowInteropHelper），需迁移到 Avalonia 平台句柄。
		private void SaveMainWindowLocationState()
		{
			if (_viewModel.StartUpFinished)
			{
				ForkPlusSettings.Default.MainWindowLocationState = this.GetWindowLocationState();
			}
		}

		protected override void OnWindowStateChanged(EventArgs e)
		{
			base.OnWindowStateChanged(e);
			// 纯状态切换（最大化↔正常）若不伴随尺寸/位置变化，不会触发 Resized/PositionChanged，
			// 此处补充保存，避免状态变更丢失。
			SaveMainWindowLocationState();
		}

		protected override void OnDrop(DragEventArgs e)
		{
			base.OnDrop(e);
			// TODO: Avalonia 迁移 - 文件拖放：e.Data.GetData(DataFormats.FileDrop) → e.Data.GetFiles()
			string[] array = e.Data.GetFiles()?.Select(f => f.Path.LocalPath).ToArray();
			if (array != null && array.Length != 0)
			{
				foreach (string path in array)
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
			// 原 WPF OnSourceInitialized 逻辑：恢复窗口位置/尺寸/状态。
			// Avalonia 无 OnSourceInitialized（Win32 专属），移到 Loaded。
			// TODO: Avalonia 迁移 - Win32 SetWindowPlacement 依赖 WindowLocationStateExtensions 的 WindowInteropHelper.Handle，
			//       需迁移到 Avalonia 平台句柄；Loaded 时机较晚，窗口可能短暂以默认尺寸闪现。
			WindowLocationState windowLocationState = ForkPlusSettings.Default.MainWindowLocationState;
			if (windowLocationState.WindowState == WindowState.Minimized)
			{
				windowLocationState = new WindowLocationState(windowLocationState.Left, windowLocationState.Top, windowLocationState.Width, windowLocationState.Height, WindowState.Normal);
			}
			base.Left = windowLocationState.Left;
			base.Top = windowLocationState.Top;
			base.Width = windowLocationState.Width;
			base.Height = windowLocationState.Height;
			// 再用 Win32 SetWindowPlacement 精确恢复（处理多显示器、DPI、还原矩形）。
			this.SetWindowLocationState(windowLocationState);
			if (windowLocationState.WindowState == WindowState.Maximized)
			{
				base.WindowState = WindowState.Maximized;
			}
			StartupTimeReporter.MainWindowLoaded();
			_menuManager?.Initialize();
			InitializeKeyBindings();
			TabManager.RestoreSession();
			Toolbar.RefreshWorkspacesButton();
			RefreshTitle();
			RefreshRepositoriesStatus();
			_updateCheckManager.Start();
			App.CliArguments.RunCommand();
			// Avalonia: Dispatcher.BeginInvoke → Dispatcher.Post
			base.Dispatcher.Post(StartupTimeReporter.UIReady);
		}

		/// <summary>手动触发更新检测（由帮助菜单"Check for Updates..."调用）。</summary>
		public void CheckForUpdates()
		{
			_updateCheckManager.CheckNow();
		}

		private void InitializeKeyBindings()
		{
			// TODO: Avalonia 迁移 - WPF CommandBindings/RoutedCommand 体系在 Avalonia 中不存在。
			// 需改用 Window.KeyBindings（Avalonia.Input.KeyBinding）+ ICommand 包装，并迁移
			// IUICommandExtension.CreateShortcutCommandBinding（依赖 WPF RoutedCommand/InputGestures）。
			// 以下原始逻辑保留待迁移（暂用 #if false 关闭编译，避免依赖未迁移的 WPF 命令类型）。
#if false
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
			base.CommandBindings.Add(Commands.Undo.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepoForUndo = TabManager.ActiveRepositoryUserControl;
				if (activeRepoForUndo != null)
				{
					Commands.Undo.Execute(activeRepoForUndo);
				}
			}));
			base.CommandBindings.Add(Commands.Redo.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepoForRedo = TabManager.ActiveRepositoryUserControl;
				if (activeRepoForRedo != null)
				{
					Commands.Redo.Execute(activeRepoForRedo);
				}
			}));
#endif
		}

		private void Window_Closing(object sender, WindowClosingEventArgs e)
		{
			ForkPlusSettings.Default.MainWindowLocationState = this.GetWindowLocationStateX();
			TabManager.SaveSession();
			ForkPlusSettings.Default.Save();
		}

		private void Window_Activated(object sender, EventArgs e)
		{
			Log.Info("WindowActivated");
			if (!_viewModel.StartUpFinished)
			{
				_viewModel.StartUpFinished = true;
				return;
			}
			if (_viewModel.PreventRefreshAfterChildDialogClose || ChildDialogsAreNotAlreadyClosed())
			{
				Log.Info("Application Window Activated: skip (" + _viewModel.PreventRefreshAfterChildDialogCloseReason + ")");
				_viewModel.ClearPreventRefreshAfterChildDialogClose();
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
			return _viewModel.ShouldSkipActivationRefresh(repositoryPath);
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
			// TODO: Avalonia 迁移 - Window.OwnedWindows 在 Avalonia 中不存在，
			// 改用 desktop lifetime 的 Windows 列表，按 Owner==this 过滤Owned 窗口。
			var windows = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows;
			if (windows != null)
			{
				foreach (Window ownedWindow in windows)
				{
					if (ownedWindow.Owner == this && ownedWindow is QuickLaunchWindow)
					{
						_viewModel.PreventRefreshAfterChildDialogCloseReason = ownedWindow.GetType().Name;
						return true;
					}
				}
			}
			return false;
		}

	}
}
