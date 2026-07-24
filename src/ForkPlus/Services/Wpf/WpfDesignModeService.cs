using System;
using System.ComponentModel;
using System.Diagnostics;

namespace ForkPlus.Services.Wpf
{
	public class WpfDesignModeService : IDesignModeService
	{
		private readonly bool _isDesignMode;

		public bool IsInDesignMode => _isDesignMode;

		public WpfDesignModeService()
		{
			_isDesignMode = ComputeIsDesignMode();
		}

		private static bool ComputeIsDesignMode()
		{
			// 阶段 4.5：WPF DesignerProperties.GetIsInDesignMode(new DependencyObject()) 在 Avalonia 中无等价 API。
			// Avalonia 设计时检测改用 Designer.IsDesignMode（Avalonia.Markup.Xaml），但服务层无控件引用，
			// 此处仅保留 LicenseManager + 进程名检测（跨平台有效）。
			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
				return true;

			try
			{
				string processName = Process.GetCurrentProcess().ProcessName;
				return processName.Equals("XDesProc", StringComparison.OrdinalIgnoreCase)
					|| processName.Equals("DesignToolsServer", StringComparison.OrdinalIgnoreCase)
					|| processName.Equals("DesignToolsServerHost", StringComparison.OrdinalIgnoreCase)
					|| processName.IndexOf("XamlDesigner", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch
			{
				return false;
			}
		}
	}
}
