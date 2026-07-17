using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using ForkPlus.Accounts;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.IO.Ipc;
using ForkPlus.Services;
using ForkPlus.Services.Wpf;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using Microsoft.Win32;
using NLog;

namespace ForkPlus
{
	public partial class App : Application
	{
		private class NativeMethods
		{
			[DllImport("shell32.dll", SetLastError = true)]
			private static extern void SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)] string AppID);

			public static void SetAppUserModelID(string appUserModelID)
			{
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

		private static Brush _windowBorderBrush;

		private static ResourceDictionary _windowsBorderResourceDictionary;

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
			InstanceDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
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
				PresentationTraceSources.DataBindingSource.Listeners.Add(new BindingErrorTraceListener());
				PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
			}
			else
			{
				LogManager.Configuration = new ProductionLoggingConfiguration();
			}
			RegisterGlobalExceptionLogging();
			LogHelper.LogWelcome();
			_askPassIpcServer = new IpcServer(NamedPipeHelper.AskPassPipeName, AskPassIpcMessageHandler);
			_defaultIpcServer = new IpcServer(NamedPipeHelper.DefaultPipeName, DefaultIpcMessageHandler);
			HandleCommandLineArguments();
		}

		private void RegisterGlobalExceptionLogging()
		{
			DispatcherUnhandledException += App_DispatcherUnhandledException;
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
			AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
			TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
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
			string activeWindow = Application.Current?.Windows.OfType<Window>().FirstOrDefault((Window x) => x.IsActive)?.GetType().FullName ?? "<none>";
			IInputElement focusedInputElement = Keyboard.FocusedElement;
			DependencyObject focusedDependencyObject = focusedInputElement as DependencyObject;
			string focusedElement = DescribeInputElement(focusedInputElement);
			string focusedElementAncestors = DescribeAncestors(focusedDependencyObject);
			string scrollContentPresenterDiagnostics = DescribeScrollContentPresenters(Application.Current?.Windows.OfType<Window>().FirstOrDefault((Window x) => x.IsActive));
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
			try
			{
				ContentControl.ContentTemplateProperty.OverrideMetadata(typeof(ScrollViewer), new FrameworkPropertyMetadata(null, ScrollViewerContentTemplateChanged));
				ContentPresenter.ContentTemplateProperty.OverrideMetadata(typeof(ScrollContentPresenter), new FrameworkPropertyMetadata(null, ScrollContentPresenterContentTemplateChanged));
			}
			catch (InvalidOperationException)
			{
			}
		}

		private static void ScrollViewerContentTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue == null || !(d is ScrollViewer scrollViewer))
			{
				return;
			}
			try
			{
				scrollViewer.ContentTemplate = null;
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to clear ScrollViewer.ContentTemplate on " + VisualTreeAttachmentHelper.Describe(scrollViewer) + ".", ex);
			}
		}

		private static void ScrollContentPresenterContentTemplateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (e.NewValue == null || !(d is ScrollContentPresenter scrollContentPresenter))
			{
				return;
			}
			try
			{
				scrollContentPresenter.ContentTemplate = null;
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to clear ScrollContentPresenter.ContentTemplate on " + VisualTreeAttachmentHelper.Describe(scrollContentPresenter) + ".", ex);
			}
		}

		private static string DescribeInputElement(IInputElement element)
		{
			if (element == null)
			{
				return "<none>";
			}
			if (!(element is DependencyObject dependencyObject))
			{
				return element.GetType().FullName;
			}
			List<string> parts = new List<string>
			{
				VisualTreeAttachmentHelper.Describe(dependencyObject)
			};
			if (dependencyObject is FrameworkElement frameworkElement)
			{
				parts.Add("DataContext=" + (frameworkElement.DataContext?.GetType().FullName ?? "<null>"));
				parts.Add("TemplatedParent=" + VisualTreeAttachmentHelper.Describe(frameworkElement.TemplatedParent));
			}
			else if (dependencyObject is FrameworkContentElement frameworkContentElement)
			{
				parts.Add("DataContext=" + (frameworkContentElement.DataContext?.GetType().FullName ?? "<null>"));
				parts.Add("TemplatedParent=" + VisualTreeAttachmentHelper.Describe(frameworkContentElement.TemplatedParent));
			}
			return string.Join(", ", parts);
		}

		private static string DescribeAncestors(DependencyObject dependencyObject, int maxDepth = 10)
		{
			if (dependencyObject == null)
			{
				return "<none>";
			}
			List<string> parts = new List<string>();
			DependencyObject dependencyObject2 = dependencyObject;
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

		private static DependencyObject GetDebugParent(DependencyObject child)
		{
			if (child == null)
			{
				return null;
			}
			DependencyObject parent = LogicalTreeHelper.GetParent(child);
			if (parent != null)
			{
				return parent;
			}
			if (child is Visual || child is Visual3D)
			{
				return VisualTreeHelper.GetParent(child);
			}
			return null;
		}

		private static string DescribeScrollContentPresenters(DependencyObject root)
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

		private static void CollectScrollContentPresenterDiagnostics(DependencyObject item, List<string> parts, int depth)
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
				int childrenCount = VisualTreeHelper.GetChildrenCount(item);
				for (int i = 0; i < childrenCount; i++)
				{
					CollectScrollContentPresenterDiagnostics(VisualTreeHelper.GetChild(item, i), parts, depth + 1);
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
			if (item is DependencyObject dependencyObject)
			{
				return VisualTreeAttachmentHelper.Describe(dependencyObject);
			}
			return item.GetType().FullName;
		}

		public static void RefreshWindowBorderBrush()
		{
			SolidColorBrush solidColorBrush = (ForkPlusSettings.Default.Theme.IsDarkBase() ? _defaultWindowBorderDarkBrush : _defaultWindowBorderLightBrush);
			Brush brush = (IsSystemAccentBrushEnabled() ? SystemParameters.WindowGlassBrush : solidColorBrush);
			if (brush != _windowBorderBrush)
			{
				_windowBorderBrush = brush;
				_windowBorderBrush?.Freeze();
				ResourceDictionary resourceDictionary = new ResourceDictionary();
				resourceDictionary.Add("WindowBorderBrush", _windowBorderBrush);
				Application.Current.Resources.MergedDictionaries.Add(resourceDictionary);
				if (_windowsBorderResourceDictionary != null)
				{
					Application.Current.Resources.MergedDictionaries.Remove(_windowsBorderResourceDictionary);
				}
				_windowsBorderResourceDictionary = resourceDictionary;
				Theme.Refresh();
			}
		}

		protected override void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			ServiceLocator.Initialize(
				dispatcher: new WpfDispatcher(Dispatcher.CurrentDispatcher),
				designMode: new WpfDesignModeService(),
				appContext: new WpfAppContext(),
				clipboard: new WpfClipboardService(),
				timer: new WpfTimerService(),
				toast: new WpfToastNotificationService(),
				windowManager: new WpfWindowManagerService()
			);
			_ = IsDebug;
			InitializeRenderMode();
			InitializeTheme();
			RefreshWindowBorderBrush();
			SubscribeToUserPreferences();
			if (!Environment.Is64BitOperatingSystem)
			{
				MessageBox.Show(ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Currently Fork doesn't support 32-bit Windows"));
			}
			else if (IsDebug || InitializeForkInstance())
			{
				ConfigureThreadPool();
				new MainWindow().Show();
			}
		}

		private void InitializeTheme()
		{
			if (ForkPlusSettings.Default.FollowSystemTheme)
			{
				_systemTheme = GetSystemTheme();
				// 跟随系统时只映射到基底 Light/Dark（系统只有明暗二元）
				ForkPlusSettings.Default.Theme = ((_systemTheme != 0) ? ThemeType.Dark : ThemeType.Light);
			}
			ResourceDictionary resourceDictionary = Application.Current.Resources.MergedDictionaries.FirstOrDefault((ResourceDictionary rd) => rd.Source != null && rd.Source.OriginalString.Contains("/ForkPlus;component/Theme/Generic."));
			ResourceDictionary item = new ResourceDictionary
			{
				Source = ForkPlusSettings.Default.Theme.ResourceUri()
			};
			Application.Current.Resources.MergedDictionaries.Add(item);
			if (resourceDictionary != null)
			{
				Application.Current.Resources.MergedDictionaries.Remove(resourceDictionary);
			}
			Theme.SubscribeToSystemEvents();
			InitializeTextEditorContextMenuStyle();
		}

		private void InitializeTextEditorContextMenuStyle()
		{
			try
			{
				Type nestedType = typeof(TextElement).Assembly.GetType("System.Windows.Documents.TextEditorContextMenu").GetNestedType("EditorContextMenu", BindingFlags.NonPublic);
				Style value = Application.Current.Resources[typeof(ContextMenu)] as Style;
				Application.Current.Resources.Add(nestedType, value);
			}
			catch (Exception ex)
			{
				Log.Error("Cannot initialize TextEditorContextMenu style: " + ex.Message);
			}
		}

		private void InitializeRenderMode()
		{
			if (ForkPlusSettings.Default.DisableHardwareAcceleration)
			{
				RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
			}
		}

		private void SubscribeToUserPreferences()
		{
			try
			{
				SystemEvents.UserPreferenceChanged += delegate(object s, UserPreferenceChangedEventArgs e)
				{
					Log.Info($"System event: UserPreferenceChanged ({e.Category})");
					if (e.Category == UserPreferenceCategory.General)
					{
						RefreshWindowBorderBrush();
						RefreshTheme();
					}
				};
			}
			catch (Exception ex)
			{
				Log.Error(ex.Message);
			}
		}

		private void RefreshTheme()
		{
			if (Application.Current.MainWindow != null)
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
			ForkPlusSettings @default = ForkPlusSettings.Default;
			if (!IsGitInstanceAvailable())
			{
				if (!new ConfigureGitInstanceWindow().ShowDialog().GetValueOrDefault())
				{
					DoShutdown();
					return false;
				}
			}
			WarnIfGitVersionUnsupported(GitPath);
			if (string.IsNullOrEmpty(@default.Guid))
			{
				if (!new WelcomeWindow().ShowDialog().GetValueOrDefault())
				{
					DoShutdown();
					return false;
				}
			}
			@default.MigratedToFork2_10_3 = true;
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
					MessageBox.Show(msg, ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Git version too old"), MessageBoxButton.OK, MessageBoxImage.Warning);
				}
				else if (result.Status == GitVersionStatus.Outdated)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string recText = GitVersionChecker.RecommendedVersion.ToString(2);
					string msg = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is below the recommended {1}. Consider upgrading for better compatibility.",
						versionText, recText);
					MessageBox.Show(msg, ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Current("Git version outdated"), MessageBoxButton.OK, MessageBoxImage.Information);
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check git version", ex);
			}
		}

		protected override void OnExit(ExitEventArgs e)
		{
			ForkPlusSettings.Default.Save();
			_askPassIpcServer.Dispose();
			_defaultIpcServer.Dispose();
			base.OnExit(e);
		}

		private void DoShutdown()
		{
			Shutdown();
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
				Application.Current.Dispatcher.Sync(delegate
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
				base.Dispatcher.Sync(delegate
				{
					CliCommand.CreateCliCommand(args)?.Run(workingDirectory);
				});
				if (WriteStringToPipe(pipeServer, DefaultIpcPipe_Handled) != -1)
				{
					Log.Error("Cannot read cliRequest from pipe");
				}
				base.Dispatcher.Async(delegate
				{
					Window mainWindow = Application.Current.MainWindow;
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
			using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\DWM");
			object obj = registryKey?.GetValue("ColorPrevalence");
			if (obj != null)
			{
				return (int)obj > 0;
			}
			return false;
		}

		private static SystemTheme GetSystemTheme()
		{
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
