using System;

namespace ForkPlus.Services
{
	/// <summary>
	/// 轻量级服务定位器，用于解耦业务层对 WPF 的直接依赖。
	/// 迁移完成后可替换为正式 DI 容器。
	///
	/// Phase 0.1 新增三个可选接口（Localization / GitEnvironment / Dialogs），
	/// 主工程在 ServiceLocator.Initialize 时按需注入实现。
	/// 业务层迁移时（Phase 0.3+）会逐步把 PreferencesLocalization.Current / App.OverrideCredentialHelper
	/// 等调用改为 ServiceLocator.Localization.Current / ServiceLocator.GitEnvironment.OverrideCredentialHelper。
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

		// Phase 0.1 新增：三个抽象接口（暂未注册实现，主工程下个子阶段补 WpfLocalizationService 等）
		public static ILocalizationService Localization { get; private set; }
		public static IGitEnvironment GitEnvironment { get; private set; }
		public static IDialogService Dialogs { get; private set; }
		// Phase 0.2c 新增：用户设置抽象（ForkPlusSettings 暂未迁入 Core，先通过接口暴露 Git/ 所需属性）
		public static IUserSettings UserSettings { get; private set; }

		public static bool IsInitialized { get; private set; }

		public static void Initialize(
			IDispatcher dispatcher,
			IDesignModeService designMode,
			IAppContext appContext,
			IClipboardService clipboard,
			ITimerService timer = null,
			IToastNotificationService toast = null,
			IWindowManagerService windowManager = null,
			ILocalizationService localization = null,
			IGitEnvironment gitEnvironment = null,
			IDialogService dialogs = null,
			IUserSettings userSettings = null)
		{
			Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
			DesignMode = designMode ?? throw new ArgumentNullException(nameof(designMode));
			AppContext = appContext ?? throw new ArgumentNullException(nameof(appContext));
			Clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
			Timer = timer;
			Toast = toast;
			WindowManager = windowManager;
			Localization = localization;
			GitEnvironment = gitEnvironment;
			Dialogs = dialogs;
			UserSettings = userSettings;
			IsInitialized = true;
		}

		public static void Reset()
		{
			Dispatcher = null;
			DesignMode = null;
			AppContext = null;
			Clipboard = null;
			Localization = null;
			GitEnvironment = null;
			Dialogs = null;
			UserSettings = null;
			IsInitialized = false;
		}
	}
}
