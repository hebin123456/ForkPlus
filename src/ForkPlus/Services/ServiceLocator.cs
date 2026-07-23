using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 轻量级服务定位器，用于解耦业务层对 WPF 的直接依赖。
	/// 迁移完成后可替换为正式 DI 容器。
	/// </summary>
	public static class ServiceLocator
	{
		public static IDispatcher Dispatcher { get; private set; }
		public static IDesignModeService DesignMode { get; private set; }
		public static IAppContext AppContext { get; private set; }
		public static IClipboardService Clipboard { get; private set; }
		public static ITimerService Timer { get; private set; }
		public static IToastNotificationService Toast { get; private set; }
		public static IWindowManagerService WindowManager { get; private set; }

		// ===== 阶段 0 新增抽象（可空，渐进式注册）=====
		public static IMessageBoxService MessageBox { get; private set; }
		public static IProcessService Process { get; private set; }
		public static IFileSystemDialogService FileSystemDialog { get; private set; }
		public static ICredentialService Credential { get; private set; }
		public static IFileAssociationService FileAssociation { get; private set; }
		public static ISystemThemeService SystemTheme { get; private set; }

		// ===== 阶段 1 新增抽象（领域层本地化访问）=====
		public static ILocalizationService Localization { get; private set; }

		public static bool IsInitialized { get; private set; }

		public static void Initialize(
			IDispatcher dispatcher,
			IDesignModeService designMode,
			IAppContext appContext,
			IClipboardService clipboard,
			ITimerService timer = null,
			IToastNotificationService toast = null,
			IWindowManagerService windowManager = null)
		{
			Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
			DesignMode = designMode ?? throw new ArgumentNullException(nameof(designMode));
			AppContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
			Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
			Timer = timer;
			Toast = toast;
			WindowManager = windowManager;
			IsInitialized = true;
		}

		/// <summary>
		/// 注册阶段 0 新增的平台抽象服务。全部可选（null 表示该服务暂不注册）。
		/// 应在 <see cref="Initialize"/> 之后调用。
		/// </summary>
		public static void RegisterPlatformServices(
			IMessageBoxService messageBox = null,
			IProcessService process = null,
			IFileSystemDialogService fileSystemDialog = null,
			ICredentialService credential = null,
			IFileAssociationService fileAssociation = null,
			ISystemThemeService systemTheme = null,
			ILocalizationService localization = null)
		{
			if (messageBox != null) MessageBox = messageBox;
			if (process != null) Process = process;
			if (fileSystemDialog != null) FileSystemDialog = fileSystemDialog;
			if (credential != null) Credential = credential;
			if (fileAssociation != null) FileAssociation = fileAssociation;
			if (systemTheme != null) SystemTheme = systemTheme;
			if (localization != null) Localization = localization;
		}

		public static void Reset()
		{
			Dispatcher = null;
			DesignMode = null;
			AppContext = null;
			Clipboard = null;
			Timer = null;
			Toast = null;
			WindowManager = null;
			MessageBox = null;
			Process = null;
			FileSystemDialog = null;
			Credential = null;
			FileAssociation = null;
			SystemTheme = null;
			Localization = null;
			IsInitialized = false;
		}
	}
}
