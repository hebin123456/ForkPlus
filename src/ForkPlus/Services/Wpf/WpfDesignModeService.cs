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

			try
			{
				var element = new System.Windows.DependencyObject();
				if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(element))
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
