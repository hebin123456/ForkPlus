using System;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Markup;
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
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI
{
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_MainMenu", Type = typeof(Menu))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "Part_NotificationManagerToggleButton", Type = typeof(ToggleButton))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "NotificationManagerUserControl", Type = typeof(NotificationManagerUserControl))]
	public partial class MainWindow : CustomWindow, ILocalizableControl
	{
		private const string PartNameMainMenu = "PART_MainMenu";

		private const string PartNameNotificationManagerToggleButton = "Part_NotificationManagerToggleButton";

		private const string PartNameNotificationManagerUserControl = "NotificationManagerUserControl";

		public static readonly MainWindowCommands Commands = new MainWindowCommands();

		private MainWindowMenuManager _menuManager;

		private bool _startUpFinished;

		private Menu _templatePartMainMenu;

		private ToggleButton _templatePartNotificationManagerToggleButton;

		private NotificationManagerUserControl _templatePartNotificationManagerUserControl;

		private readonly AutomaticBackgroundFetchManager _automaticBackgroundFetchManager = new AutomaticBackgroundFetchManager();

		private readonly UpdateCheckManager _updateCheckManager = new UpdateCheckManager();

		private readonly RepositoryStatusManager _repositoryStatusManager = new RepositoryStatusManager();

		private bool _preventRefreshAfterChildDialogClose;

		private string _preventRefreshAfterChildDialogCloseReason;

		private DateTime _lastActivationStatusRefreshTime = DateTime.MinValue;

		private string _lastActivationStatusRefreshRepositoryPath;

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		[Null]
		public static MainWindow Instance => (global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow as MainWindow;

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
					(global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
				};
			}
			InitializeComponent();
			// Migration note：WPF Window.OnDrop override → Avalonia DragDrop.DropEvent 订阅转发。
			global::Avalonia.Input.DragDrop.SetAllowDrop(this, true);
			AddHandler(global::Avalonia.Input.DragDrop.DropEvent, (s, e) => OnDrop(e));
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
			// Migration note（根因）：WPF 里 Closing/Activated 在 XAML 或基类接线，迁移后丢失——
			// Window_Closing（保存窗口位置+会话）与 Window_Activated（启动完成标记+激活刷新）
			// 从未执行：窗口位置永不保存、_startUpFinished 恒 false、激活时仓库不刷新。
			base.Closing += Window_Closing;
			base.Activated += Window_Activated;
			// 预同步几何（Width/Height 是 DIP 属性，Show 前设置可避免 1000x600 → 恢复尺寸的闪跳；
			// Position/最大化在 OnOpened 里恢复，避免未显示窗口操作 PlatformImpl 的时序问题）。
			WindowLocationState windowLocationState = ForkPlusSettings.Default.MainWindowLocationState;
			if (windowLocationState.WindowState != global::Avalonia.Controls.WindowState.Minimized)
			{
				base.Width = windowLocationState.Width;
				base.Height = windowLocationState.Height;
			}
			(global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow = this;
			TabManager = new TabManager(TabControl);
		}

		// 修复（2026-09-05，测试宿主 Stack overflow）：ApplyLocalization 末尾把本轮刷新扇出给
		// lifetime.Windows 里其他 ILocalizableControl 窗口；本类自身就实现该接口，当窗口集合
		// 里有 ≥2 个 MainWindow（测试环境多窗口并存；真实用户场景主窗+偏好窗同理可构造出
		// 跨窗互调链）时，A→B→A 无守卫互调无限递归爆栈（实证：全量测试尾部 test host 崩溃，
		// 栈帧 ApplyLocalization 连续出现）。本地化刷新幂等：一轮分发进行中，后续扇出请求
		// 直接跳过即可（同一轮里文案已刷新过）。
		private static bool _localizationDispatchInProgress;

		public void ApplyLocalization()
	{
		if (_localizationDispatchInProgress)
		{
			return;
		}
		_localizationDispatchInProgress = true;
		try
		{
			ApplyLocalizationCore();
		}
		finally
		{
			_localizationDispatchInProgress = false;
		}
	}

		private void ApplyLocalizationCore()
	{
		_menuManager?.ApplyLocalization();
		Toolbar.ApplyLocalization();
		TabManager?.RefreshTabTitles();
		if (_templatePartNotificationManagerToggleButton != null)
		{
			global::Avalonia.Controls.ToolTip.SetTip(_templatePartNotificationManagerToggleButton,PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage));
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
		foreach (Window window in (global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows)
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

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
	{
		base.OnApplyTemplate(e);
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
			global::Avalonia.Controls.ToolTip.SetTip(_templatePartNotificationManagerToggleButton,PreferencesLocalization.Translate("Notifications", ForkPlusSettings.Default.UiLanguage));
		}
		// 缓存 ControlTemplate 内的 NotificationManagerUserControl 引用，
		// ApplyLocalization 时调用其 ApplyLocalization() 刷新 HeaderLabel.Text（Bug v2.1.2）。
		base.Template.TryFindName<NotificationManagerUserControl>(PartNameNotificationManagerUserControl, this, out _templatePartNotificationManagerUserControl);
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

		// Migration note（根因）：WPF OnSourceInitialized 是窗口句柄创建后的虚方法回调，Avalonia 无此
		// 生命周期；原 protected void OnSourceInitialized 从未被调用（死代码），导致窗口位置/大小
		// 永不恢复（一直停在 XAML 默认 1000x600）。改在 OnOpened（Avalonia 等价时机）恢复。
		protected override void OnOpened(EventArgs e)
		{
			base.OnOpened(e);
			WindowLocationState windowLocationState = ForkPlusSettings.Default.MainWindowLocationState;
			if (windowLocationState.WindowState == global::Avalonia.Controls.WindowState.Minimized)
			{
				windowLocationState = new WindowLocationState(windowLocationState.Left, windowLocationState.Top, windowLocationState.Width, windowLocationState.Height, global::Avalonia.Controls.WindowState.Normal);
			}
			// 先同步几何属性到目标值（DIP），SetWindowLocationState 内部按平台走 Win32/Avalonia 路径
			// （Unix 路径负责 DIP→物理像素换算并处理最大化）。
			this.SetWindowLocationState(windowLocationState);
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

		// Migration note（根因）：原来是非 override 的 protected void（隐藏 CustomWindow 虚方法），
		// CustomWindow.PositionChanged → 虚方法 OnLocationChanged 永远分派到基类实现，
		// 本方法体（移动窗口时保存位置）从未执行。改为 override 修复分发链。
		protected override void OnLocationChanged(EventArgs e)
		{
			base.OnLocationChanged(e);
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.MainWindowLocationState = this.GetWindowLocationState();
			}
		}

		// Migration note（根因）：同 OnLocationChanged——原为隐藏基类的死代码，改 override 接入
		// CustomWindow.OnPropertyChanged → OnStateChanged 分发链（状态切换时保存位置）。
		protected override void OnStateChanged(EventArgs e)
		{
			base.OnStateChanged(e);
			// 纯状态切换（最大化↔正常）若不伴随尺寸/位置变化，不会触发 SizeChanged/LocationChanged，
			// 此处补充保存，避免状态变更丢失。
			if (_startUpFinished)
			{
				ForkPlusSettings.Default.MainWindowLocationState = this.GetWindowLocationState();
			}
		}

		protected void OnDrop(DragEventArgs e)
		{
			if (e.WpfData().GetData(DataFormats.FileDrop) is string[] array && array.Length != 0)
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
			_updateCheckManager.Start();
			App.CliArguments.RunCommand();
			base.Dispatcher.Post(StartupTimeReporter.UIReady);
		}

		/// <summary>手动触发更新检测（由帮助菜单"Check for Updates..."调用）。</summary>
		public void CheckForUpdates()
		{
			_updateCheckManager.CheckNow();
		}

		private void InitializeKeyBindings()
		{
			this.AddCommandBinding(Commands.ActivateCommitView.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateCommitView.Execute();
			}));
			this.AddCommandBinding(Commands.ActivateRevisionList.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateRevisionList.Execute();
			}));
			this.AddCommandBinding(Commands.ActivateRepositoryTab.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateRepositoryTab.Execute();
			}));
			this.AddCommandBinding(Commands.ActivateSearchTab.CreateShortcutCommandBinding(delegate
			{
				Commands.ActivateSearchTab.Execute();
			}));
			this.AddCommandBinding(Commands.ShowHead.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowHead.Execute();
			}));
			this.AddCommandBinding(Commands.ToggleShowReflogInRevisionList.CreateShortcutCommandBinding(delegate
			{
				Commands.ToggleShowReflogInRevisionList.Execute();
			}));
			this.AddCommandBinding(Commands.CloseActiveTab.CreateShortcutCommandBinding(delegate
			{
				Commands.CloseActiveTab.Execute();
			}));
			this.AddCommandBinding(Commands.NewTab.CreateShortcutCommandBinding(delegate
			{
				Commands.NewTab.Execute();
			}));
			this.AddCommandBinding(Commands.OpenRepository.CreateShortcutCommandBinding(delegate
			{
				Commands.OpenRepository.Execute();
			}));
			this.AddCommandBinding(Commands.RefreshRepositoryData.CreateShortcutCommandBinding(delegate
			{
				Commands.RefreshRepositoryData.Execute();
			}));
			this.AddCommandBinding(Commands.ShowCloneWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowCloneWindow.Execute();
			}));
			this.AddCommandBinding(Commands.ShowInitGitMmRepositoryWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowInitGitMmRepositoryWindow.Execute();
			}));
			this.AddCommandBinding(Commands.ShowCreateBranchWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl8 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl8 != null)
				{
					Commands.ShowCreateBranchWindow.Execute(activeRepositoryUserControl8, null);
				}
			}));
			this.AddCommandBinding(Commands.ShowCreateRepositoryWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowCreateRepositoryWindow.Execute();
			}));
			this.AddCommandBinding(Commands.ShowCreateTagWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl7 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl7 != null)
				{
					Commands.ShowCreateTagWindow.Execute(activeRepositoryUserControl7, null);
				}
			}));
			this.AddCommandBinding(Commands.ShowFetchWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl6 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl6 != null)
				{
					Commands.ShowFetchWindow.Execute(activeRepositoryUserControl6, activeRepositoryUserControl6.GitModule);
				}
			}));
			this.AddCommandBinding(Commands.ShowQuickLaunchWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowQuickLaunchWindow.Execute();
			}));
			this.AddCommandBinding(Commands.ShowQuickLaunchCheckoutWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowQuickLaunchCheckoutWindow.Execute();
			}));
			this.AddCommandBinding(Commands.ShowPullWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowPullWindow.Execute(TabManager.ActiveRepositoryUserControl);
			}));
			this.AddCommandBinding(Commands.QuickPull.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl5 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl5 != null)
				{
					Commands.QuickPull.Execute(activeRepositoryUserControl5);
				}
			}));
			this.AddCommandBinding(Commands.ShowPushWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl4 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl4 != null)
				{
					Commands.ShowPushWindow.Execute(activeRepositoryUserControl4);
				}
			}));
			this.AddCommandBinding(Commands.QuickPush.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl3 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl3 != null)
				{
					Commands.QuickPush.Execute(activeRepositoryUserControl3);
				}
			}));
			this.AddCommandBinding(Commands.SelectNextTab.CreateShortcutCommandBinding(delegate
			{
				Commands.SelectNextTab.Execute();
			}));
			this.AddCommandBinding(Commands.SelectPreviousTab.CreateShortcutCommandBinding(delegate
			{
				Commands.SelectPreviousTab.Execute();
			}));
			this.AddCommandBinding(Commands.ShowSaveStashWindow.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl2 = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl2 != null)
				{
					Commands.ShowSaveStashWindow.Execute(activeRepositoryUserControl2, activeRepositoryUserControl2.GitModule);
				}
			}));
			this.AddCommandBinding(Commands.OpenRepositoryInShellTool.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepositoryUserControl = TabManager.ActiveRepositoryUserControl;
				if (activeRepositoryUserControl != null)
				{
					Commands.OpenRepositoryInShellTool.Execute(activeRepositoryUserControl.GitModule);
				}
			}));
			this.AddCommandBinding(Commands.ToggleReferenceFilter.CreateShortcutCommandBinding(delegate
			{
				Commands.ToggleReferenceFilter.Execute();
			}));
			this.AddCommandBinding(Commands.IncreaseLayoutScale.CreateShortcutCommandBinding(delegate
			{
				Commands.IncreaseLayoutScale.Execute();
			}));
			this.AddCommandBinding(Commands.DecreaseLayoutScale.CreateShortcutCommandBinding(delegate
			{
				Commands.DecreaseLayoutScale.Execute();
			}));
			this.AddCommandBinding(Commands.ShowPreferencesWindow.CreateShortcutCommandBinding(delegate
			{
				Commands.ShowPreferencesWindow.Execute();
			}));
			this.AddCommandBinding(Commands.Undo.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepoForUndo = TabManager.ActiveRepositoryUserControl;
				if (activeRepoForUndo != null)
				{
					Commands.Undo.Execute(activeRepoForUndo);
				}
			}));
			this.AddCommandBinding(Commands.Redo.CreateShortcutCommandBinding(delegate
			{
				RepositoryUserControl activeRepoForRedo = TabManager.ActiveRepositoryUserControl;
				if (activeRepoForRedo != null)
				{
					Commands.Redo.Execute(activeRepoForRedo);
				}
			}));
		}

		// Migration note：WPF Closing 事件是 CancelEventHandler(CancelEventArgs)，
		// Avalonia Window.Closing 是 EventHandler<WindowClosingEventArgs>。
		private void Window_Closing(object sender, global::Avalonia.Controls.WindowClosingEventArgs e)
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

		// v3.10.2 修复：同仓库激活刷新节流。
		// 此前固定 10 秒：编辑器 ↔ ForkPlus 快速切换（<10s）时激活刷新被跳过，而 ForkPlus 没有
		// 文件监听/定时器等其他主动刷新手段 → 变更视图停留在旧快照上：
		// 文件明明改了却显示为"未变更"（点击无 diff、双击被过滤无法暂存），直到某次间隔 >10s 的
		// 激活才恢复——这也是"开一圈小乌龟回来就好了"的真正原因（往返耗时超过了节流窗口）。
		// 提交视图只刷 SubDomain.Status（一条 git status），代价小，节流降到 3 秒；
		// 完整仓库刷新（revision 视图）保持 10 秒防抖。
		private bool ShouldSkipActivationRefresh(string repositoryPath, double throttleSeconds = 10.0)
		{
			DateTime now = DateTime.UtcNow;
			if (string.Equals(repositoryPath, _lastActivationStatusRefreshRepositoryPath, StringComparison.OrdinalIgnoreCase) && now - _lastActivationStatusRefreshTime < TimeSpan.FromSeconds(throttleSeconds))
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
			if (ShouldSkipActivationRefresh(repositoryPath, 3.0))
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
