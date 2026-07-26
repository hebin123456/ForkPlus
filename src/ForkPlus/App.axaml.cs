using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.IO.Ipc;
using ForkPlus.Services;
using ForkPlus.Services.Avalonia;
using ForkPlus.Services.Wpf;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using Microsoft.Win32;
using NLog;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Controls.Documents;

namespace ForkPlus
{
	public partial class App : Avalonia.Application
	{
		/// <summary>
		/// Avalonia XAML 加载入口（替代 WPF 自动生成的 InitializeComponent）。
		/// </summary>
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		/// <summary>
		/// Avalonia 框架初始化完成回调（替代 WPF 的 OnStartup）。
		/// 原 WPF OnStartup 的业务逻辑迁移到 OnStartupLegacy，由此处调用。
		/// ShutdownMode="OnExplicitShutdown" 在 Avalonia 中通过 desktop.ShutdownMode 设置。
		/// </summary>
		public override void OnFrameworkInitializationCompleted()
		{
			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
				OnStartupLegacy();
			}
			base.OnFrameworkInitializationCompleted();
		}

		private class NativeMethods
		{
			[DllImport("shell32.dll", SetLastError = true)]
			[SupportedOSPlatform("windows")]
			private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

			public static void SetAppUserModelID(string appUserModelID)
			{
				// 阶段 5：AppUserModelID 仅 Windows 任务栏分组需要，非 Windows 平台无意义，直接跳过。
				if (!OperatingSystem.IsWindows())
				{
					return;
				}
				try
				{
					SetCurrentProcessExplicitAppUserModelID(appUserModelID);
				}
				catch
				{
				}
			}
		}

		private enum SystemTheme
		{
			Light,
			Dark
		}

		public static readonly string ForkDirectoryPath;

		public static readonly string ForkDataDirectoryPath;

		private static readonly string LegacyForkDirectoryPath;

		private static readonly string LegacyForkDataDirectoryPath;

		public static readonly string RepositoriesFilePath;

		public static readonly string InstanceDirectory;

		public static readonly string ForkCredentialHelperPath;

		private static readonly string[] _defaultCredentialHelper;

		private static readonly string[] _overrideCredentialHelper;

		private static readonly string[] _overrideCredentialHelperBt;

		public static readonly string EnvironmentGitInstancePath;

		public static readonly string ForkGitInstancePath;

		public static readonly string AppName;

		public static readonly Version OSVersion;

		public static readonly CliArguments CliArguments;

		private static readonly string AppUserModelID;

		private static readonly string DefaultIpcPipe_StringSeparator;

		private static readonly string DefaultIpcPipe_CliRequest;

		private static readonly string DefaultIpcPipe_Handled;

		private static readonly SolidColorBrush _defaultWindowBorderLightBrush;

		private static readonly SolidColorBrush _defaultWindowBorderDarkBrush;

		private static IBrush _windowBorderBrush;

		private static SystemTheme _systemTheme;

		private readonly IpcServer _askPassIpcServer;

		private readonly IpcServer _defaultIpcServer;

		private bool _loggedVisualParentingFirstChanceException;

		public static string[] OverrideCredentialHelperBt
		{
			get
			{
				if (AccountManager.Current.Accounts.Length == 0)
				{
					return _defaultCredentialHelper;
				}
				return _overrideCredentialHelperBt;
			}
		}

		public static string[] OverrideCredentialHelper
		{
			get
			{
				if (AccountManager.Current.Accounts.Length == 0)
				{
					return _defaultCredentialHelper;
				}
				return _overrideCredentialHelper;
			}
		}

		public static string GitPath => EnvironmentGitInstancePath ?? ForkPlusSettings.Default.GitInstancePath ?? ForkGitInstancePath;

		public static string ShellPath => Path.Combine(Path.GetDirectoryName(GitPath), "sh.exe");

		public static string BashPath => Path.Combine(Path.GetDirectoryName(GitPath), "bash.exe");

		/// <summary>
		/// PATH 查找 git-mm.exe 的缓存。PATH 在运行时通常不变，缓存避免每次访问 GitMmPath 都遍历 PATH。
		/// </summary>
		private static string _cachedGitMmFromPath;
		private static bool _gitMmFromPathResolved;

		/// <summary>
		/// git-mm 可执行文件路径。优先使用用户在偏好设置中指定的路径；
		/// 否则在 PATH 环境变量中查找 <c>git-mm.exe</c>；
		/// 再否则在 git.exe 同目录查找。三者都找不到返回 null。
		/// </summary>
		public static string GitMmPath => ResolveGitMmPath();

	/// <summary>
	/// 仅从 PATH 查找的 git-mm.exe 路径（带缓存）。供偏好设置 UI 列出候选时使用，
	/// 避免直接调用 FindExecutableInPath 绕过缓存导致每次刷新都遍历 PATH。
	/// </summary>
	public static string GitMmPathFromPath
	{
		get
		{
			if (!_gitMmFromPathResolved)
			{
				_cachedGitMmFromPath = FindExecutableInPath("git-mm.exe");
				_gitMmFromPathResolved = true;
			}
			return _cachedGitMmFromPath;
		}
	}

	private static string ResolveGitMmPath()
	{
		string saved = ForkPlusSettings.Default.GitMmInstancePath;
		if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved))
		{
			return saved;
		}
		string fromPath = GitMmPathFromPath;
		if (fromPath != null)
		{
			return fromPath;
		}
		try
		{
			string gitDir = Path.GetDirectoryName(GitPath);
			if (gitDir != null)
			{
				string sibling = Path.Combine(gitDir, "git-mm.exe");
				if (File.Exists(sibling))
				{
					return sibling;
				}
			}
		}
			catch (Exception ex)
			{
				Log.Error("Failed to resolve git-mm path from git directory", ex);
			}
			return null;
		}

		/// <summary>
		/// 在 PATH 环境变量中查找指定可执行文件，返回第一个匹配的完整路径；未找到返回 null。
		/// </summary>
		public static string FindExecutableInPath(string fileName)
		{
			try
			{
				string pathEnv = Environment.GetEnvironmentVariable("PATH");
				if (string.IsNullOrEmpty(pathEnv))
				{
					return null;
				}
				string[] segments = pathEnv.Split(Path.PathSeparator);
				foreach (string raw in segments)
				{
					if (string.IsNullOrWhiteSpace(raw))
					{
						continue;
					}
					string dir = raw.Trim();
					try
					{
						string candidate = Path.Combine(dir, fileName);
						if (File.Exists(candidate))
						{
							return Path.GetFullPath(candidate);
						}
					}
					catch (Exception ex)
					{
						Log.Error("Failed to check '" + dir + "' in PATH for '" + fileName + "'", ex);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to search PATH for '" + fileName + "'", ex);
			}
			return null;
		}

		public static int ProcessId { get; }

		public static string ProcessIdString { get; }

		public static string Version
		{
			get
			{
				AssemblyInformationalVersionAttribute informationalVersion = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>();
				if (informationalVersion != null && !string.IsNullOrEmpty(informationalVersion.InformationalVersion))
				{
					return informationalVersion.InformationalVersion;
				}
				Version version = Assembly.GetExecutingAssembly().GetName().Version;
				if (version != null)
				{
					return version.ToString();
				}
				return "0.0.0.0";
			}
		}

		public static string UserAgent => AppName + " " + Version;

		public static bool IsDebug => Debugger.IsAttached;

		static App()
		{
			string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			LegacyForkDirectoryPath = Path.Combine(localApplicationData, "Fork");
			LegacyForkDataDirectoryPath = Path.Combine(localApplicationData, "ForkData");
			ForkDirectoryPath = Path.Combine(localApplicationData, "ForkPlus");
			ForkDataDirectoryPath = Path.Combine(localApplicationData, "ForkPlusData");
			MigrateLegacyAppData();
			RepositoriesFilePath = Path.Combine(ForkDataDirectoryPath, "repositories.toml");
			// NativeAOT/单文件应用下 Assembly.Location 返回空字符串，改用 AppContext.BaseDirectory
			// 获取应用目录（与下一行 ForkCredentialHelperPath 路径一致）。
			InstanceDirectory = AppContext.BaseDirectory;
			ForkCredentialHelperPath = Path.Combine(AppContext.BaseDirectory, Consts.ForkPlus.AskPassFilename);
			_defaultCredentialHelper = new string[0];
			_overrideCredentialHelper = new string[6]
			{
				"-c",
				"credential.helper=\"\"",
				"-c",
				"credential.helper=\"" + PathHelper.NormalizeUnix(ForkCredentialHelperPath).EscapeSpaces() + "\"",
				"-c",
				"credential.helper=\"manager\""
			};
			_overrideCredentialHelperBt = new string[6]
			{
				"-c",
				"credential.helper=",
				"-c",
				"credential.helper=" + PathHelper.NormalizeUnix(ForkCredentialHelperPath).EscapeSpaces(),
				"-c",
				"credential.helper=manager"
			};
			EnvironmentGitInstancePath = GetEnvironmentGitInstancePath();
			ForkGitInstancePath = GetForkGitInstancePath();
			AppName = Assembly.GetExecutingAssembly().GetName().Name;
			OSVersion = Environment.OSVersion.Version;
			CliArguments = new CliArguments();
			AppUserModelID = "com.squirrel.ForkPlus.ForkPlus";
			DefaultIpcPipe_StringSeparator = "!#±";
			DefaultIpcPipe_CliRequest = "cli-request";
			DefaultIpcPipe_Handled = "handled";
			_defaultWindowBorderLightBrush = new SolidColorBrush(Color.FromRgb(59, 172, 237));
			_defaultWindowBorderDarkBrush = new SolidColorBrush(Color.FromRgb(59, 172, 237));
			using (Process process = Process.GetCurrentProcess())
			{
				ProcessId = process.Id;
				ProcessIdString = process.Id.ToString();
				StartupTimeReporter.AppStarted(process.StartTime);
			}
			NativeMethods.SetAppUserModelID(AppUserModelID);
			RegisterScrollViewerContentTemplateGuard();
		}

		public App()
		{
			if (IsDebug)
		{
			LogManager.Configuration = new DebugLoggingConfiguration();
			// NOTE (Avalonia limitation): WPF PresentationTraceSources.DataBindingSource 用于捕获绑定错误，
			// Avalonia 无等价 API。绑定诊断改由 Avalonia.Logging.Log 在 Debug 配置下输出。
		}
			else
			{
				LogManager.Configuration = new ProductionLoggingConfiguration();
			}
			RegisterGlobalExceptionLogging();
			LogHelper.LogWelcome();
			// 阶段 6 修复：IpcServer 创建失败（权限/平台限制/命名冲突）不应导致整个应用崩溃。
			// 单实例检测 + AskPass 凭据回调依赖 IPC，但失败后应用仍可运行（仅丢失这两个能力）。
			try
			{
				_askPassIpcServer = new IpcServer(NamedPipeHelper.AskPassPipeName, AskPassIpcMessageHandler);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create AskPass IPC server", ex);
			}
			try
			{
				_defaultIpcServer = new IpcServer(NamedPipeHelper.DefaultPipeName, DefaultIpcMessageHandler);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create default IPC server", ex);
			}
			HandleCommandLineArguments();
		}

		private void RegisterGlobalExceptionLogging()
		{
			// 阶段 6 修复：Avalonia 的 UI 线程未处理异常通过 Dispatcher.UIThread.UnhandledException 捕获。
			// 原代码注释"Avalonia 无 DispatcherUnhandledException 等价"是错误的——Avalonia 确实有这个事件。
			// 不注册此事件会导致 UI 线程的渲染异常、绑定异常等不被记录到日志，"选 git 路径后闪退"无诊断信息。
			Dispatcher.UIThread.UnhandledException += Dispatcher_UnhandledException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
			TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
		}

		private void Dispatcher_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			// 阶段 6：捕获 UI 线程未处理异常（渲染/绑定/控件初始化等）。
			// e.Exception 包含完整异常堆栈，记录到日志后可定位"选 git 路径后闪退"等崩溃根因。
			Log.Error($"UI 线程未处理异常 (sender={sender?.GetType().FullName ?? "<null>"}): {e.Exception?.GetType().FullName ?? "<null>"}: {e.Exception?.Message}{(e.Exception?.StackTrace != null ? Environment.NewLine + e.Exception.StackTrace : "")}");
			// 标记已处理，防止进程被 Avalonia 运行时直接终止（默认行为是终止进程）。
			e.Handled = true;
		}

		private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
		{
			Log.Error("Unhandled UI exception", e.Exception);
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			Exception ex = e.ExceptionObject as Exception;
			if (ex != null)
			{
				Log.Error("Unhandled AppDomain exception", ex);
			}
			else
			{
				Log.Error("Unhandled AppDomain exception: " + e.ExceptionObject);
			}
		}

		private void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
		{
			if (_loggedVisualParentingFirstChanceException || !IsVisualParentingArgumentException(e.Exception))
			{
				return;
			}
			_loggedVisualParentingFirstChanceException = true;
			// 阶段 5：Avalonia Application 无 Windows/MainWindow 属性，需经
			// IClassicDesktopStyleApplicationLifetime 获取窗口列表。
			Window activeWindowObj = GetDesktopWindows().FirstOrDefault((Window x) => x.IsActive);
			string activeWindow = activeWindowObj?.GetType().FullName ?? "<none>";
			// 阶段 5：Avalonia 无 FocusManager.Instance 单例；从活动窗口的 FocusManager 取焦点元素。
			IInputElement focusedInputElement = activeWindowObj?.FocusManager?.GetFocusedElement();
			AvaloniaObject focusedAvaloniaObject = focusedInputElement as AvaloniaObject;
			string focusedElement = DescribeInputElement(focusedInputElement);
			string focusedElementAncestors = DescribeAncestors(focusedAvaloniaObject);
			string scrollContentPresenterDiagnostics = DescribeScrollContentPresenters(activeWindowObj);
			string stackTrace = new StackTrace(1, fNeedFileInfo: true).ToString();
			Log.Warn("First-chance visual parenting exception" + Environment.NewLine + "ActiveWindow: " + activeWindow + Environment.NewLine + "FocusedElement: " + focusedElement + Environment.NewLine + "FocusedElementAncestors: " + focusedElementAncestors + Environment.NewLine + "ScrollContentPresenters:" + Environment.NewLine + scrollContentPresenterDiagnostics + Environment.NewLine + "CurrentStack:" + Environment.NewLine + stackTrace, e.Exception);
		}

		private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
		{
			Log.Error("Unobserved task exception", e.Exception);
		}

		private static bool IsVisualParentingArgumentException(Exception ex)
		{
			ArgumentException argumentException = ex as ArgumentException;
			if (argumentException == null)
			{
				return false;
			}
			string message = argumentException.Message;
			if (string.IsNullOrEmpty(message))
			{
				return false;
			}
			return message.IndexOf("Visual", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		private static void RegisterScrollViewerContentTemplateGuard()
		{
			// NOTE (Avalonia limitation): WPF OverrideMetadata not supported in Avalonia; diagnostic guard stubbed.
		}

		// NOTE (Avalonia limitation): WPF OverrideMetadata callbacks; stubbed since RegisterScrollViewerContentTemplateGuard is no-op.
		private static void ScrollViewerContentTemplateChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e) { }
		private static void ScrollContentPresenterContentTemplateChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e) { }

		private static string DescribeInputElement(IInputElement element)
		{
			if (element == null)
			{
				return "<none>";
			}
			if (!(element is AvaloniaObject dependencyObject))
			{
				return element.GetType().FullName;
			}
			List<string> parts = new List<string>
			{
				VisualTreeAttachmentHelper.Describe(dependencyObject)
			};
			if (dependencyObject is Control frameworkElement)
			{
				parts.Add("DataContext=" + (frameworkElement.DataContext?.GetType().FullName ?? "<null>"));
				parts.Add("TemplatedParent=" + VisualTreeAttachmentHelper.Describe(frameworkElement.TemplatedParent));
			}
			return string.Join(", ", parts);
		}

		private static string DescribeAncestors(AvaloniaObject dependencyObject, int maxDepth = 10)
		{
			if (dependencyObject == null)
			{
				return "<none>";
			}
			List<string> parts = new List<string>();
			AvaloniaObject dependencyObject2 = dependencyObject;
			for (int i = 0; dependencyObject2 != null && i < maxDepth; i++)
			{
				parts.Add(VisualTreeAttachmentHelper.Describe(dependencyObject2));
				dependencyObject2 = GetDebugParent(dependencyObject2);
			}
			if (dependencyObject2 != null)
			{
				parts.Add("...");
			}
			return string.Join(" -> ", parts);
		}

		private static AvaloniaObject GetDebugParent(AvaloniaObject child)
		{
			if (child == null)
			{
				return null;
			}
			// 阶段 5：ILogical.LogicalParent 返回 ILogical（非 AvaloniaObject），需转换。
			ILogical logical = child as ILogical;
			if (logical?.LogicalParent is AvaloniaObject logicalParentObj)
			{
				return logicalParentObj;
			}
			// 阶段 5：Avalonia 11 移除 Visual 接口，改用 Visual + VisualExtensions.GetVisualParent。
			if (child is Visual visual)
			{
				return visual.GetVisualParent();
			}
			return null;
		}

		// 阶段 5：Avalonia Application 不再直接暴露 Windows/MainWindow，需经
		// IClassicDesktopStyleApplicationLifetime 访问桌面窗口集合。
		private static IEnumerable<Window> GetDesktopWindows()
		{
			if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				return desktop.Windows;
			}
			return Enumerable.Empty<Window>();
		}

		public static Window GetDesktopMainWindow()
	{
		if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			return desktop.MainWindow;
		}
		return null;
	}

		private static string DescribeScrollContentPresenters(AvaloniaObject root)
		{
			if (root == null)
			{
				return "<none>";
			}
			List<string> parts = new List<string>();
			CollectScrollContentPresenterDiagnostics(root, parts, 0);
			if (parts.Count == 0)
			{
				return "<none>";
			}
			return string.Join(Environment.NewLine, parts.Take(40));
		}

		private static void CollectScrollContentPresenterDiagnostics(AvaloniaObject item, List<string> parts, int depth)
		{
			if (item == null || depth > 80)
			{
				return;
			}
			try
			{
				if (item is ScrollViewer scrollViewer && scrollViewer.ContentTemplate != null)
				{
					parts.Add("ScrollViewer " + VisualTreeAttachmentHelper.Describe(scrollViewer) + ", Content=" + DescribeObject(scrollViewer.Content) + ", ContentTemplate=" + DescribeObject(scrollViewer.ContentTemplate) + ", Ancestors=" + DescribeAncestors(scrollViewer, 8));
				}
				if (item is ScrollContentPresenter scrollContentPresenter && scrollContentPresenter.ContentTemplate != null)
				{
					parts.Add("ScrollContentPresenter " + VisualTreeAttachmentHelper.Describe(scrollContentPresenter) + ", Content=" + DescribeObject(scrollContentPresenter.Content) + ", ContentTemplate=" + DescribeObject(scrollContentPresenter.ContentTemplate) + ", Ancestors=" + DescribeAncestors(scrollContentPresenter, 8));
				}
				if (item is ContentPresenter contentPresenter && contentPresenter.ContentTemplate != null && item.GetType().Name.IndexOf("Scroll", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					parts.Add("Scroll-like ContentPresenter " + VisualTreeAttachmentHelper.Describe(contentPresenter) + ", Content=" + DescribeObject(contentPresenter.Content) + ", ContentTemplate=" + DescribeObject(contentPresenter.ContentTemplate) + ", Ancestors=" + DescribeAncestors(contentPresenter, 8));
				}
				// 阶段 5：GetVisualChildren() 是 Visual 的扩展方法（Avalonia.VisualTree），
				// AvaloniaObject 无此方法，需先转 Visual。
				if (!(item is Visual visualItem))
				{
					return;
				}
				List<Visual> children = visualItem.GetVisualChildren().ToList();
				for (int i = 0; i < children.Count; i++)
				{
					CollectScrollContentPresenterDiagnostics(children[i], parts, depth + 1);
				}
			}
			catch (Exception ex)
			{
				parts.Add("Diagnostics failed at " + VisualTreeAttachmentHelper.Describe(item) + ": " + ex.Message);
			}
		}

		private static string DescribeObject(object item)
		{
			if (item == null)
			{
				return "<null>";
			}
			if (item is AvaloniaObject dependencyObject)
			{
				return VisualTreeAttachmentHelper.Describe(dependencyObject);
			}
			return item.GetType().FullName;
		}

		public static void RefreshWindowBorderBrush()
		{
			SolidColorBrush solidColorBrush = ForkPlusSettings.Default.Theme.IsDarkBase() ? _defaultWindowBorderDarkBrush : _defaultWindowBorderLightBrush;
			IBrush brush = IsSystemAccentBrushEnabled()
				? (ServiceLocator.ThemeService?.GetSystemBrush(SystemColorType.Accent, solidColorBrush) ?? solidColorBrush)
				: solidColorBrush;
			if (!Equals(brush, _windowBorderBrush))
			{
				_windowBorderBrush = brush;
				Application.Current.Resources["WindowBorderBrush"] = brush;
				Theme.Refresh();
			}
		}

		private void OnStartupLegacy()
		{
			ServiceLocator.Initialize(
				dispatcher: new WpfDispatcher(Dispatcher.UIThread),
				designMode: new WpfDesignModeService(),
				appContext: new WpfAppContext(),
				clipboard: new WpfClipboardService(),
				timer: new WpfTimerService(),
				// 阶段 6：按平台注册 Toast 通知服务。
				// - Windows：WpfToastNotificationService（WinRT Toast）
				// - Linux：LinuxToastNotificationService（notify-send）
				// - macOS：MacOsToastNotificationService（osascript）
				toast: CreateToastService(),
				windowManager: new WpfWindowManagerService()
			);
			// 阶段 6：按平台注册平台抽象服务。
		// - messageBox/process/fileSystemDialog/systemTheme/localization：跨平台用 Wpf* 实现
		//   （这些是 Avalonia 上的等价适配器，命名沿用 Wpf 前缀仅因历史原因）
		// - credential/fileAssociation：按平台分派（Windows/Linux/macOS 各有原生实现）
		// - themeService：AvaloniaThemeService（跨平台，读 PlatformSettings）
		ServiceLocator.RegisterPlatformServices(
			messageBox: new WpfMessageBoxService(),
			process: new WpfProcessService(),
			fileSystemDialog: new WpfFileSystemDialogService(),
			credential: CreateCredentialService(),
			fileAssociation: CreateFileAssociationService(),
			systemTheme: new WpfSystemThemeService(),
			localization: new WpfLocalizationService(),
			themeService: new AvaloniaThemeService()
		);
			_ = IsDebug;
			InitializeRenderMode();
			InitializeTheme();
			RefreshWindowBorderBrush();
			SubscribeToUserPreferences();
			if (!Environment.Is64BitOperatingSystem)
			{
				ServiceLocator.MessageBox.Show(ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Currently Fork doesn't support 32-bit Windows"));
			}
			else
			{
				// 阶段 6 修复"程序一闪而过"根因：Avalonia 11 的主循环（Dispatcher.MainLoop）在
				// OnFrameworkInitializationCompleted 返回后才启动，Startup 期间不能 PushFrame；
				// 同时 Window.ShowDialog(owner) 要求 owner 已可见（IsVisible=true）。
				// 原 WPF 顺序（IsGitInstanceAvailable → ShowDialog → MainWindow.Show）在 WPF 可行
				// 是因为 WPF 的 Dispatcher 在 Application.Startup 之前已运行消息泵。
				// 修复：MainWindow 构造时已设置 desktop.MainWindow=this（参见 MainWindow.xaml.cs），
				// 由 ClassicDesktopStyleApplicationLifetime.StartCore 自动 Show。InitializeForkInstance
				// 中可能弹出 ConfigureGitInstanceWindow / WelcomeWindow 模态对话框，必须延迟到主循环
				// 启动后执行——Dispatcher.UIThread.Post 把回调放到主循环队首，此时 MainWindow 已可见、
				// dispatcher 已就绪，ShowDialog 可正常 PushFrame。失败仍走 DoShutdown()。
				var mainWindow = new MainWindow();
				if (IsDebug)
				{
					ConfigureThreadPool();
				}
				else
				{
					// Post 到 UIThread 队列：OnFrameworkInitializationCompleted 返回后，lifetime.StartCore
					// 调 ShowMainWindow() 显示 MainWindow 并启动 MainLoop，随后 Post 回调执行。
					Dispatcher.UIThread.Post(() =>
					{
						if (InitializeForkInstance())
						{
							ConfigureThreadPool();
						}
						else
						{
							// InitializeForkInstance 内部已调 DoShutdown，此处仅 return 兜底。
						}
					});
				}
			}
		}

		/// <summary>
		/// 阶段 6：按当前运行平台创建 Toast 通知服务实例。
		/// - Windows：WpfToastNotificationService（封装 WinRT ToastNotificationManager）
		/// - Linux：LinuxToastNotificationService（封装 notify-send）
		/// - macOS：MacOsToastNotificationService（封装 osascript display notification）
		/// 单一职责：仅做平台分派，不含业务逻辑。
		/// </summary>
		[Null]
		private static IToastNotificationService CreateToastService()
		{
			if (OperatingSystem.IsWindows())
			{
				return new WpfToastNotificationService();
			}
			if (OperatingSystem.IsLinux())
			{
				return new Services.Avalonia.LinuxToastNotificationService();
			}
			if (OperatingSystem.IsMacOS())
			{
				return new Services.Avalonia.MacOsToastNotificationService();
			}
			// 未识别平台（如 FreeBSD）：返回 null，ServiceLocator.Toast 为 null 时调用方静默降级。
			Log.Warn("No ToastNotificationService available for current platform: " + Environment.OSVersion);
			return null;
		}

		/// <summary>
		/// 阶段 6：按当前运行平台创建凭据存储服务实例。
		/// - Windows：WindowsCredentialService（advapi32 CredManager）
		/// - Linux：LinuxCredentialService（libsecret / secret-tool，fallback ~/.forkplus-credentials）
		/// - macOS：MacOsCredentialService（Security framework Keychain / security CLI）
		/// </summary>
		[Null]
		private static ICredentialService CreateCredentialService()
		{
			if (OperatingSystem.IsWindows())
			{
				return new WindowsCredentialService();
			}
			if (OperatingSystem.IsLinux())
			{
				return new Services.Avalonia.LinuxCredentialService();
			}
			if (OperatingSystem.IsMacOS())
			{
				return new Services.Avalonia.MacOsCredentialService();
			}
			Log.Warn("No CredentialService available for current platform: " + Environment.OSVersion);
			return null;
		}

		/// <summary>
		/// 阶段 6：按当前运行平台创建文件关联查询服务实例。
		/// - Windows：WindowsFileAssociationService（Shlwapi AssocQueryString）
		/// - Linux：LinuxFileAssociationService（xdg-mime + mimeapps.list + .desktop 解析）
		/// - macOS：MacOsFileAssociationService（lsregister + UTI 映射）
		/// </summary>
		[Null]
		private static IFileAssociationService CreateFileAssociationService()
		{
			if (OperatingSystem.IsWindows())
			{
				return new WindowsFileAssociationService();
			}
			if (OperatingSystem.IsLinux())
			{
				return new Services.Avalonia.LinuxFileAssociationService();
			}
			if (OperatingSystem.IsMacOS())
			{
				return new Services.Avalonia.MacOsFileAssociationService();
			}
			Log.Warn("No FileAssociationService available for current platform: " + Environment.OSVersion);
			return null;
		}

		// 阶段 5：从 private 实例方法改为 internal static，供 SwitchApplicationThemeCommand 通过 App.InitializeTheme() 静态调用
		// （WPF 主题切换的入口点，Avalonia 迁移保留同方法名）。所有依赖均为静态成员。
		internal static void InitializeTheme()
		{
			if (ForkPlusSettings.Default.FollowSystemTheme)
			{
				_systemTheme = GetSystemTheme();
				// 跟随系统时只映射到基底 Light/Dark（系统只有明暗二元）
				ForkPlusSettings.Default.Theme = ((_systemTheme != 0) ? ThemeType.Dark : ThemeType.Light);
			}
			// Avalonia: 主题通过 RequestedThemeVariant 切换（替代 WPF MergedDictionaries 加载 Generic.{Skin}.xaml）
			if (ForkPlusSettings.Default.Theme.IsDarkBase())
				Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
			else
				Application.Current.RequestedThemeVariant = ThemeVariant.Light;
			Theme.SubscribeToSystemEvents();
			InitializeTextEditorContextMenuStyle();
			ApplyCustomColors();
		}

		/// <summary>根据 ForkPlusSettings.Default.CustomColors 构建动态 ResourceDictionary 并 merge 到
	/// MergedDictionaries 末尾。仅当 UseCustomColors=true 且 CustomColors 非空时才应用覆盖。
	///
	/// v2.1.2 关键修复：用户反馈"换色后主界面不刷新，必须重启才生效"。根因——
	/// 旧实现只在 MergedDictionaries 末尾 Add 一个含 29 个 Color key 的小 dict，依赖
	/// Brushes.xaml 中 SolidColorBrush.Color = {DynamicResource XXXColor} 的链式通知自动更新。
	/// 但 WPF 在 Style/Template 已实例化、控件已渲染后，对 MergedDictionaries 末尾 Add
	/// 同名 key 的覆盖不会可靠地触发所有 DynamicResource 重新解析——尤其是 Style 中
	/// Setter 引用的 Brush、ContextMenu/Popup 内的控件、已渲染过的 UserControl 等，
	/// 表现为"换色后只有部分 UI 刷新，主界面整体不变化"。
	///
	/// 对比主题切换（SwitchApplicationThemeCommand）能立即刷新——因为它**重新加载
	/// 整个 Generic.{Skin}.xaml 字典**（先 Add 新 dict → 后 Remove 旧 dict），这会强制
	/// WPF 让所有 DynamicResource 失效并重新解析，所有 SolidColorBrush 实例被重建，
	/// 所有引用 Brush 的控件（包括 Style/Popup/已渲染控件）都拿到新 Brush。
	///
	/// 修复策略：模仿主题切换的做法，在 ApplyCustomColors 末尾对当前 Generic 字典
	/// 做一次"Add 新 + Remove 旧"的等效刷新——重新加载同一份 Generic.{Skin}.xaml，
	/// 强制 WPF 全量失效所有 DynamicResource。然后再 Add 自定义颜色覆盖字典。
	/// 这样换色效果和主题切换一样立即生效，性能代价是重新加载一份 ~290 Color + 270 Brush
	/// 的字典（毫秒级，可接受）。
	///
	/// 末尾 raise ApplicationThemeChanged 事件，通知 18 个订阅控件（DiffEditor/Heatmap 等）
	/// 主动刷新缓存的 Color 值（这些控件缓存 Color 值类型，必须靠事件刷新）。</summary>
	public static void ApplyCustomColors()
	{
		// Avalonia: 直接覆盖 Resources 中的键值，无需 MergedDictionaries 字典追踪
		Dictionary<string, string> customColors = ForkPlusSettings.Default.CustomColors;
		bool hasCustomColors = ForkPlusSettings.Default.UseCustomColors && customColors != null && customColors.Count > 0;

		// 关键：重新加载当前主题字典，模仿主题切换的强力刷新机制。
		// ReloadThemeDictionary 在 Avalonia 中转发到 Theme.Refresh()，触发主题服务刷新所有引用 Brush 的控件。
		ReloadThemeDictionary();

		// 直接写入 Resources 覆盖预设颜色（Avalonia IResourceDictionary 索引赋值）
		if (hasCustomColors)
		{
			foreach (KeyValuePair<string, string> kv in customColors)
			{
				try
				{
					string hex = kv.Value;
					Color color = hex.StartsWith("#") ? Color.Parse(hex) : Color.Parse("#" + hex);
					Application.Current.Resources[kv.Key] = color;
				}
				catch (Exception ex)
				{
					Log.Warn("Invalid custom color value for key '" + kv.Key + "': " + kv.Value, ex);
				}
			}
		}
		Theme.Refresh();
		// raise 事件让订阅者刷新缓存的颜色/画刷，实现自定义颜色实时生效。
		NotificationCenter.Current.RaiseApplicationThemeChanged(Application.Current, ForkPlusSettings.Default.Theme);
	}

	/// <summary>重新加载当前主题的 Generic.{Skin}.xaml 字典：先 Add 新 dict → 后 Remove 旧 dict。
	/// 这是 SwitchApplicationThemeCommand 主题切换能立即刷新所有 UI 的核心机制——
	/// 通过替换整个 Generic 字典让 WPF 强制让所有 DynamicResource 失效并重新解析，
	/// 所有 SolidColorBrush 实例被重建，所有引用 Brush 的控件（含 Style/Popup/已渲染控件）
	/// 都拿到新 Brush。自定义颜色变化时同样调用此方法，让换色效果像主题切换一样即时生效。</summary>
	private static void ReloadThemeDictionary()
	{
		try
		{
			// Avalonia: 主题字典刷新通过 ThemeService 完成（替代 WPF MergedDictionaries Add 新 + Remove 旧）
			Theme.Refresh();
		}
		catch (Exception ex)
		{
			Log.Warn("ReloadThemeDictionary failed: " + ex.Message, ex);
		}
	}

		private static void InitializeTextEditorContextMenuStyle()
		{
			// NOTE (Avalonia limitation): WPF TextEditorContextMenu internal type not available in Avalonia.
		}

		private void InitializeRenderMode()
		{
			// NOTE (Avalonia limitation): WPF RenderOptions.ProcessRenderMode not available in Avalonia.
			// Avalonia rendering configuration is handled via RenderOptions edge mode or platform-specific options.
		}

		private void SubscribeToUserPreferences()
		{
			// 阶段 5：Microsoft.Win32.SystemEvents 在 UseWPF=false 的 net10.0-windows 下不再隐式可用，
			// 且该事件用于监听系统主题/配色变化。Avalonia 跨平台方案改由 NotificationCenter.ApplicationThemeChanged
			// 统一处理主题切换；此 Windows 专属订阅暂置空，避免引入 SystemEvents 包依赖。
			try
			{
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
			}
		}

		private void RefreshTheme()
		{
			if (GetDesktopMainWindow() != null)
			{
				SystemTheme systemTheme = GetSystemTheme();
				if (systemTheme != _systemTheme)
				{
					_systemTheme = systemTheme;
					// 跟随系统变化时映射到基底 Light/Dark
					ThemeType newTheme = ((_systemTheme != 0) ? ThemeType.Dark : ThemeType.Light);
					ForkPlus.UI.MainWindow.Commands.SwitchApplicationTheme.Execute(newTheme, followSystemTheme: true);
				}
			}
		}

		private bool InitializeForkInstance()
		{
			// 阶段 6 诊断"选 git 路径后闪退"：逐步记录日志 + try/catch 兜底，定位崩溃点。
			ForkPlusSettings @default = ForkPlusSettings.Default;
			Log.Info($"InitializeForkInstance: start (GitInstanceAvailable={IsGitInstanceAvailable()}, Guid={(string.IsNullOrEmpty(@default.Guid) ? "<empty>" : "<set>")})");
			if (!IsGitInstanceAvailable())
			{
				Log.Info("InitializeForkInstance: showing ConfigureGitInstanceWindow");
				// 阶段 5：使用 WindowDialogExtensions.ShowDialog() 同步扩展（自动选取 owner + DispatcherFrame 嵌套泵）。
				bool? cfgResult;
				try
				{
					cfgResult = new ConfigureGitInstanceWindow().ShowDialog();
				}
				catch (Exception ex)
				{
					Log.Error($"InitializeForkInstance: ConfigureGitInstanceWindow 抛异常: {ex.GetType().FullName}: {ex.Message}{(ex.StackTrace != null ? Environment.NewLine + ex.StackTrace : "")}");
					DoShutdown();
					return false;
				}
				Log.Info($"InitializeForkInstance: ConfigureGitInstanceWindow result={cfgResult}");
				if (!cfgResult.GetValueOrDefault())
				{
					DoShutdown();
					return false;
				}
			}
			Log.Info($"InitializeForkInstance: WarnIfGitVersionUnsupported (GitPath={GitPath})");
			try
			{
				WarnIfGitVersionUnsupported(GitPath);
			}
			catch (Exception ex)
			{
				Log.Error($"InitializeForkInstance: WarnIfGitVersionUnsupported 抛异常: {ex.GetType().FullName}: {ex.Message}{(ex.StackTrace != null ? Environment.NewLine + ex.StackTrace : "")}");
			}
			Log.Info($"InitializeForkInstance: Guid check (empty={string.IsNullOrEmpty(@default.Guid)})");
			if (string.IsNullOrEmpty(@default.Guid))
			{
				Log.Info("InitializeForkInstance: showing WelcomeWindow");
				bool? welcomeResult;
				try
				{
					welcomeResult = new WelcomeWindow().ShowDialog();
				}
				catch (Exception ex)
				{
					Log.Error($"InitializeForkInstance: WelcomeWindow 抛异常: {ex.GetType().FullName}: {ex.Message}{(ex.StackTrace != null ? Environment.NewLine + ex.StackTrace : "")}");
					DoShutdown();
					return false;
				}
				Log.Info($"InitializeForkInstance: WelcomeWindow result={welcomeResult}");
				if (!welcomeResult.GetValueOrDefault())
				{
					DoShutdown();
					return false;
				}
			}
			@default.MigratedToFork2_10_3 = true;
			Log.Info("InitializeForkInstance: done (success)");
			return true;
		}

		public static bool IsGitInstanceAvailable()
		{
			// 仅检查 git.exe 路径是否存在；版本检测由 WarnIfGitVersionUnsupported 统一完成，
			// 避免每次启动重复启动 git version 子进程（原实现会执行 2 次子进程）。
			string gitPath = GitPath;
			return !string.IsNullOrWhiteSpace(gitPath) && File.Exists(gitPath);
		}

		/// <summary>
		/// 检测当前 git 版本，过低时弹警告（不阻止启动）。
		/// </summary>
		private static void WarnIfGitVersionUnsupported(string gitPath)
		{
			try
			{
				GitVersionCheckResult result = GitVersionChecker.Check(gitPath);
				if (result.Status == GitVersionStatus.Unsupported)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string minText = GitVersionChecker.MinimumRequiredVersion.ToString(2);
					string msg = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is older than the required {1}. Some features (diff, status, empty-changes detection) may not work correctly. Please upgrade git.",
						versionText, minText);
					ServiceLocator.MessageBox.Show(msg, ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Git version too old"), MessageBoxButton.OK, MessageBoxImage.Warning);
				}
				else if (result.Status == GitVersionStatus.Outdated)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string recText = GitVersionChecker.RecommendedVersion.ToString(2);
					string msg = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is below the recommended {1}. Consider upgrading for better compatibility.",
						versionText, recText);
					ServiceLocator.MessageBox.Show(msg, ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Git version outdated"), MessageBoxButton.OK, MessageBoxImage.Information);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check git version", ex);
			}
		}

		private void OnExitLegacy()
		{
			ForkPlusSettings.Default.Save();
			// IPC server 可能在 App 构造时创建失败（权限/平台限制），此处需 null 检查。
			_askPassIpcServer?.Dispose();
			_defaultIpcServer?.Dispose();
		}

		private void DoShutdown()
		{
			(ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
		}

		private static string GetEnvironmentGitInstancePath()
		{
			try
			{
				string environmentVariable = Environment.GetEnvironmentVariable(Consts.ForkPlus.GitInstanceEnvVariable);
				if (environmentVariable != null)
				{
					if (environmentVariable.EndsWith("git.exe") && File.Exists(environmentVariable))
					{
						return environmentVariable;
					}
					string text = Path.Combine(environmentVariable, "bin", "git.exe");
					if (File.Exists(text))
					{
						return text;
					}
				}
			}
			catch
			{
			}
			return null;
		}

		private static string GetForkGitInstancePath()
		{
			return Path.Combine(ForkDirectoryPath, "gitInstance", "2.50.1", "bin", "git.exe");
		}

		private static void MigrateLegacyAppData()
		{
			MigrateDirectoryIfNeeded(LegacyForkDirectoryPath, ForkDirectoryPath);
			MigrateDirectoryIfNeeded(LegacyForkDataDirectoryPath, ForkDataDirectoryPath);
		}

		private static void MigrateDirectoryIfNeeded(string sourceDirectory, string destinationDirectory)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(destinationDirectory) || !Directory.Exists(sourceDirectory))
				{
					return;
				}
				CopyDirectory(sourceDirectory, destinationDirectory);
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to migrate legacy app data from '" + sourceDirectory + "' to '" + destinationDirectory + "'", ex);
			}
		}

		private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
		{
			Directory.CreateDirectory(destinationDirectory);
			foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				Directory.CreateDirectory(directory.Replace(sourceDirectory, destinationDirectory));
			}
			foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
			{
				string destinationFile = file.Replace(sourceDirectory, destinationDirectory);
				if (!File.Exists(destinationFile))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(destinationFile));
					File.Copy(file, destinationFile);
				}
			}
		}

		private void HandleCommandLineArguments()
		{
			string[] commandLineArgs = Environment.GetCommandLineArgs();
			if (commandLineArgs.Length <= 1)
			{
				return;
			}
			Process currentProcess = Process.GetCurrentProcess();
			Process process = IReadOnlyListExtensions.FirstItem(Process.GetProcessesByName(currentProcess.ProcessName), (Process x) => x.Id != currentProcess.Id);
			if (process == null)
			{
				return;
			}
			NamedPipeClientStream namedPipeClientStream = NamedPipeHelper.CreatePipeClient(NamedPipeHelper.DefaultPipeName, process);
			string currentDirectory = Directory.GetCurrentDirectory();
			try
			{
				namedPipeClientStream.Connect(100);
				namedPipeClientStream.WriteString(DefaultIpcPipe_CliRequest);
				namedPipeClientStream.WriteString(currentDirectory);
				namedPipeClientStream.WriteString(string.Join(DefaultIpcPipe_StringSeparator, commandLineArgs));
				string text = namedPipeClientStream.ReadString();
				namedPipeClientStream.Close();
				if (text == DefaultIpcPipe_Handled)
				{
					Environment.Exit(0);
				}
			}
			catch (Exception arg)
			{
				Log.Warn($"Can't connect to other Fork process pipe {process.Id.ToString()}. {arg}");
			}
		}

		private void AskPassIpcMessageHandler(NamedPipeServerStream pipeServer)
		{
			string text = ReadStringFromPipe(pipeServer);
			if (text == null)
			{
				return;
			}
			string[] array = text.Split(new char[1], 3);
			string text2 = array[0];
			string repositoryPath = array[1];
			string request = array[2];
			bool noPrompt = text2 == "1" || text2 == "3";
			if (text2 == "2" || text2 == "3")
			{
				CredentialHelperArguments credentialHelperArguments = CredentialHelperArguments.Parse(request);
				if (credentialHelperArguments != null)
				{
					Account account = AccountManager.Current.FindAccount(credentialHelperArguments.Host, credentialHelperArguments.Username);
					if (account != null)
					{
						credentialHelperArguments.Username = account.Username;
						credentialHelperArguments.Password = account.Service.Connection.Authentication.GetHttpsPassword();
						pipeServer.WriteString(credentialHelperArguments.Export());
						return;
					}
				}
				pipeServer.WriteString(string.Empty);
			}
			else
			{
				string askPassResult = string.Empty;
				// 阶段 5：Avalonia Application 无 Dispatcher 属性，用 Dispatcher.UIThread。
				Dispatcher.UIThread.Sync(delegate
				{
					ForkPlus.UI.MainWindow.Commands.ShowAskPassWindow.Execute(request, noPrompt, repositoryPath, out askPassResult);
				});
				pipeServer.WriteString(askPassResult ?? string.Empty);
			}
		}

		private void DefaultIpcMessageHandler(NamedPipeServerStream pipeServer)
		{
			string text = ReadStringFromPipe(pipeServer);
			if (text == null)
			{
				Log.Error("Cannot read ipcMessage from pipe");
			}
			else if (text == DefaultIpcPipe_CliRequest)
			{
				string workingDirectory = ReadStringFromPipe(pipeServer);
				if (workingDirectory == null)
				{
					Log.Error("Cannot read workingDirectory from pipe");
					return;
				}
				string text2 = ReadStringFromPipe(pipeServer);
				if (text2 == null)
				{
					Log.Error("Cannot read cliRequest from pipe");
					return;
				}
				string[] args = text2.Split(new string[1] { DefaultIpcPipe_StringSeparator }, StringSplitOptions.None);
			// 阶段 5：Avalonia Application 无 Dispatcher 属性，用 Dispatcher.UIThread。
			Dispatcher.UIThread.Sync(delegate
			{
				CliCommand.CreateCliCommand(args)?.Run(workingDirectory);
			});
			if (WriteStringToPipe(pipeServer, DefaultIpcPipe_Handled) != -1)
			{
				Log.Error("Cannot read cliRequest from pipe");
			}
			Dispatcher.UIThread.Async(delegate
			{
				Window mainWindow = GetDesktopMainWindow();
					if (mainWindow != null)
					{
						mainWindow.Activate();
						mainWindow.Topmost = true;
						mainWindow.Topmost = false;
					}
				});
			}
			else
			{
				Log.Error("Unknown IPC message '" + text + "'");
			}
		}

		private static void ConfigureThreadPool()
		{
			ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
			ThreadPool.SetMinThreads(Math.Max(workerThreads, 10), completionPortThreads);
		}

		private static bool IsSystemAccentBrushEnabled()
		{
			if (!OperatingSystem.IsWindows())
			{
				// 非 Windows 平台无 DWM 注册表，关闭系统强调色画刷。
				return false;
			}

			try
			{
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\DWM");
				object obj = registryKey?.GetValue("ColorPrevalence");
				if (obj != null)
				{
					return (int)obj > 0;
				}
				return false;
			}
			catch (Exception)
			{
				// 注册表读取失败（权限/策略）时降级为关闭。
				return false;
			}
		}

		private static SystemTheme GetSystemTheme()
		{
			if (!OperatingSystem.IsWindows())
			{
				// 非 Windows 平台无注册表主题信息，回退到用户设置的主题。
				return (ForkPlusSettings.Default.Theme != 0) ? SystemTheme.Dark : SystemTheme.Light;
			}

			try
			{
				using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
				object obj = registryKey?.GetValue("AppsUseLightTheme");
				if (obj != null)
				{
					return ((int)obj <= 0) ? SystemTheme.Dark : SystemTheme.Light;
				}
				return (ForkPlusSettings.Default.Theme != 0) ? SystemTheme.Dark : SystemTheme.Light;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read system theme from Windows registry", ex);
				return SystemTheme.Light;
			}
		}

		[Null]
		private string ReadStringFromPipe(PipeStream pipeStream)
		{
			try
			{
				return pipeStream.ReadString();
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read string from pipe", ex);
				return null;
			}
		}

		private int WriteStringToPipe(PipeStream pipeStream, string stringToWrite)
		{
			try
			{
				return pipeStream.WriteString(stringToWrite);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write string to pipe", ex);
				return -1;
			}
		}

	}
}
