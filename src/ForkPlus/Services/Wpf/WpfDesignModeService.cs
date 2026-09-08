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
			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
				return true;

			// Migration note：WPF DesignerProperties.GetIsInDesignMode(DependencyObject) →
			// Avalonia.Controls.Design.IsDesignMode 静态属性。
			try
			{
				if (global::Avalonia.Controls.Design.IsDesignMode)
					return true;
			}
			catch { }

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
