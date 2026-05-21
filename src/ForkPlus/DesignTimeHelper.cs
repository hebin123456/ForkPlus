using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;

namespace ForkPlus
{
	internal static class DesignTimeHelper
	{
		private static readonly bool _isDesignMode = ComputeIsDesignMode();

		public static bool IsInDesignMode()
		{
			return _isDesignMode;
		}

		public static bool IsInDesignMode(DependencyObject dependencyObject)
		{
			if (dependencyObject != null && DesignerProperties.GetIsInDesignMode(dependencyObject))
			{
				return true;
			}
			return _isDesignMode;
		}

		private static bool ComputeIsDesignMode()
		{
			if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
			{
				return true;
			}
			try
			{
				if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
				{
					return true;
				}
			}
			catch
			{
			}
			try
			{
				string processName = Process.GetCurrentProcess().ProcessName;
				return processName.Equals("XDesProc", StringComparison.OrdinalIgnoreCase) || processName.Equals("DesignToolsServer", StringComparison.OrdinalIgnoreCase) || processName.Equals("DesignToolsServerHost", StringComparison.OrdinalIgnoreCase) || processName.IndexOf("XamlDesigner", StringComparison.OrdinalIgnoreCase) >= 0;
			}
			catch
			{
				return false;
			}
		}
	}
}
