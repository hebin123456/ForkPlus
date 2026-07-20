using System;
using ForkPlus.Services;

namespace ForkPlus
{
 /// <summary>
 /// 设计时模式检测。优先使用 ServiceLocator 中的 IDesignModeService，
 /// 回退到进程名检测（兼容无 ServiceLocator 的启动早期阶段）。
 /// </summary>
 internal static class DesignTimeHelper
 {
  private static readonly bool _isDesignMode;

  static DesignTimeHelper()
  {
   if (ServiceLocator.IsInitialized)
   {
    _isDesignMode = ServiceLocator.DesignMode.IsInDesignMode;
   }
   else
   {
    _isDesignMode = DetectByProcessName();
   }
  }

	public static bool IsInDesignMode()
	{
		return _isDesignMode;
	}

	private static bool DetectByProcessName()
  {
   try
   {
    string processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
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
