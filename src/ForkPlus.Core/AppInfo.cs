using System;
using System.Reflection;

namespace ForkPlus
{
    /// <summary>
    /// Phase 4.17b：跨平台 App 信息（应用名 / 版本 / UserAgent）。
    ///
    /// 背景：WPF 工程 src/ForkPlus/App.xaml.cs 中的 App.Version / App.UserAgent / App.AppName
    /// 读取 Assembly.GetExecutingAssembly()，仅 WPF 主工程可访问。Core 工程的 UpdateChecker
    /// 需要版本和 UserAgent 信息向 GitHub API 发请求。
    ///
    /// 抽象策略：用 Assembly.GetEntryAssembly() 获取入口程序集（即实际运行的可执行文件），
    /// 取其 AssemblyInformationalVersionAttribute / Name / Version。GetEntryAssembly() 在
    /// WPF（ForkPlus.exe）和 Avalonia（ForkPlus.Avalonia.dll）下都返回启动该进程的程序集。
    /// 在测试场景（无 EntryAssembly）下回退到 GetExecutingAssembly()。
    ///
    /// WPF App.Version / App.UserAgent / App.AppName 保留不变（向后兼容），
    /// 内部委托给 AppInfo.Version / AppInfo.UserAgent / AppInfo.Name。
    /// 新代码（UpdateChecker、Avalonia App）应使用 AppInfo 而非 App.*。
    /// </summary>
    public static class AppInfo
    {
        private static readonly Assembly _entryAssembly =
            Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        /// <summary>应用名（入口程序集 Name）。</summary>
        public static string Name
        {
            get
            {
                try
                {
                    return _entryAssembly.GetName().Name ?? "ForkPlus";
                }
                catch
                {
                    return "ForkPlus";
                }
            }
        }

        /// <summary>应用版本（优先 AssemblyInformationalVersion，回退到 AssemblyVersion，再回退 "0.0.0.0"）。</summary>
        public static string Version
        {
            get
            {
                try
                {
                    AssemblyInformationalVersionAttribute informationalVersion =
                        _entryAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
                    if (informationalVersion != null && !string.IsNullOrEmpty(informationalVersion.InformationalVersion))
                    {
                        return informationalVersion.InformationalVersion;
                    }
                    System.Version version = _entryAssembly.GetName().Version;
                    if (version != null)
                    {
                        return version.ToString();
                    }
                }
                catch
                {
                    // ignore
                }
                return "0.0.0.0";
            }
        }

        /// <summary>User-Agent 字符串（HTTP 请求头使用）。</summary>
        public static string UserAgent => Name + " " + Version;
    }
}
