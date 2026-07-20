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

		/// <summary>用户自定义颜色覆盖字典（动态构建，merge 到 MergedDictionaries 末尾覆盖预设皮肤颜色）。</summary>
		private static ResourceDictionary _customColorsResourceDictionary;

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
			// Phase 0.3a：构造 LocalizationService 并注册到 ServiceLocator。
			// - IAppContext 提供 ForkDataDirectoryPath（用于加载用户自定义语言文件）
			// - Func<string> 提供当前激活语言（从 ForkPlusSettings.Default.UiLanguage 取）
			// 业务层迁移到 Core 后将通过 ServiceLocator.Localization.Xxx 调用，主工程的
			// PreferencesLocalization 5 个字符串方法亦委托到此处（见 PreferencesLocalization.cs）。
			// Phase 0.2c-prep：注入 WpfGitEnvironment，业务层（Git/ 等）将通过
			// ServiceLocator.GitEnvironment.* 访问 Git 路径 / credential helper 等运行时属性。
			// Phase 0.2c：注入 WpfUserSettings，业务层通过 ServiceLocator.UserSettings.Xxx
			// 访问 ForkPlusSettings.Default 的 11 个 Git/ 所需属性（ForkPlusSettings 本身暂未迁入 Core）。
			WpfAppContext appContext = new WpfAppContext();
			LocalizationService localization = new LocalizationService(appContext, () => ForkPlusSettings.Default.UiLanguage);
			WpfGitEnvironment gitEnvironment = new WpfGitEnvironment();
			WpfUserSettings userSettings = new WpfUserSettings();
			ServiceLocator.Initialize(
				dispatcher: new WpfDispatcher(Dispatcher.CurrentDispatcher),
				designMode: new WpfDesignModeService(),
				appContext: appContext,
				clipboard: new WpfClipboardService(),
				timer: new WpfTimerService(),
				toast: new WpfToastNotificationService(),
				windowManager: new WpfWindowManagerService(),
				localization: localization,
				gitEnvironment: gitEnvironment,
				userSettings: userSettings
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
		// 移除旧的自定义颜色字典
		if (_customColorsResourceDictionary != null)
		{
			Application.Current.Resources.MergedDictionaries.Remove(_customColorsResourceDictionary);
			_customColorsResourceDictionary = null;
		}
		// 仅当用户启用自定义颜色且有自定义项时才应用覆盖；否则使用当前主题原色。
		Dictionary<string, string> customColors = ForkPlusSettings.Default.CustomColors;
		bool hasCustomColors = ForkPlusSettings.Default.UseCustomColors && customColors != null && customColors.Count > 0;

		// 关键：重新加载当前主题的 Generic.{Skin}.xaml 字典，模仿主题切换的强力刷新机制。
		// 这一步强制 WPF 让所有 DynamicResource 失效并重新解析，所有 SolidColorBrush 实例
		// 被重建，所有引用 Brush 的控件（含 Style/Popup/已渲染控件）都会刷新——
		// 这是"主题切换能立即生效"的根因，自定义颜色同样需要走这条路径。
		ReloadThemeDictionary();

		// 构建新的覆盖字典并 Add 到 MergedDictionaries 末尾
		if (hasCustomColors)
		{
			ResourceDictionary dict = new ResourceDictionary();
			foreach (KeyValuePair<string, string> kv in customColors)
			{
				try
				{
					string hex = kv.Value;
					Color color;
					if (hex.StartsWith("#") && hex.Length == 9)
						color = (Color)ColorConverter.ConvertFromString(hex);
					else if (hex.StartsWith("#") && hex.Length == 7)
						color = (Color)ColorConverter.ConvertFromString(hex);
					else
						color = (Color)ColorConverter.ConvertFromString("#" + hex);
					dict[kv.Key] = color;
				}
				catch (Exception ex)
				{
					Log.Warn("Invalid custom color value for key '" + kv.Key + "': " + kv.Value, ex);
				}
			}
			if (dict.Count > 0)
			{
				Application.Current.Resources.MergedDictionaries.Add(dict);
				_customColorsResourceDictionary = dict;
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
			// 找到当前的 Generic.{Skin}.xaml 字典（Source 非空且匹配 /Theme/Generic.*.xaml）
			ResourceDictionary oldThemeDict = null;
			foreach (ResourceDictionary rd in Application.Current.Resources.MergedDictionaries)
			{
				if (rd.Source != null &&
				    System.Text.RegularExpressions.Regex.Match(
					    rd.Source.OriginalString,
					    @"\/ForkPlus;component\/Theme\/Generic\.\w+\.xaml").Success)
				{
					oldThemeDict = rd;
					break;
				}
			}
			if (oldThemeDict == null)
				return;  // 未找到主题字典（启动早期或异常状态），跳过刷新

			// 先 Add 新 dict（同一 Source 重新加载），后 Remove 旧 dict——
			// 这个顺序与 SwitchApplicationThemeCommand 一致，确保资源查找不出现空窗。
			ResourceDictionary newThemeDict = new ResourceDictionary
			{
				Source = ForkPlusSettings.Default.Theme.ResourceUri()
			};
			Application.Current.Resources.MergedDictionaries.Add(newThemeDict);
			Application.Current.Resources.MergedDictionaries.Remove(oldThemeDict);
		}
		catch (Exception ex)
		{
			Log.Warn("ReloadThemeDictionary failed: " + ex.Message, ex);
		}
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
