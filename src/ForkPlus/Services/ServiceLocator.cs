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

		public static void Reset()
		{
			Dispatcher = null;
			DesignMode = null;
			AppContext = null;
			Clipboard = null;
			IsInitialized = false;
		}
	}
}
