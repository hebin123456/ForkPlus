using System;
using System.Diagnostics;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    // Phase 6.1：IDesignModeService 的 Avalonia 实现。
    //
    // 对照 WPF 工程 src/ForkPlus/Services/Wpf/WpfDesignModeService.cs：
    //   WPF: 三重检测——
    //     1) LicenseManager.UsageMode == LicenseUsageMode.Designtime
    //     2) DesignerProperties.GetIsInDesignMode(new DependencyObject())
    //     3) 进程名检测（XDesProc / DesignToolsServer / DesignToolsServerHost / XamlDesigner）
    //
    // Avalonia 11 实现：
    //   Avalonia 11 没有公开的 Designer.IsDesignMode API（WPF 的 DesignerProperties /
    //   LicenseManager 在 Avalonia 中无等价物，XAML 预览器走内部 XamlIl 上下文）。
    //   沿用 WPF 版的进程名检测 fallback——对 Avalonia XAML 编辑器进程同样有效
    //   （设计器宿主进程名相同，XAML 设计器仍运行在这些进程中）。
    public class AvaloniaDesignModeService : IDesignModeService
    {
        private readonly bool _isInDesignMode;

        public bool IsInDesignMode => _isInDesignMode;

        public AvaloniaDesignModeService()
        {
            _isInDesignMode = ComputeIsDesignMode();
        }

        private static bool ComputeIsDesignMode()
        {
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
