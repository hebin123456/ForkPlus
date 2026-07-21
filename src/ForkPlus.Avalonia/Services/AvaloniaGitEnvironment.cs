using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Services
{
    /// <summary>
    /// IGitEnvironment 的 Avalonia 跨平台 spike 实现。
    ///
    /// 对照 WPF 实现 src/ForkPlus/Services/Wpf/WpfGitEnvironment.cs（34 行，纯转发壳）：
    /// WPF 版所有属性委托到 App 静态属性（App.GitPath / App.ShellPath / App.BashPath 等），
    /// 这些静态属性在 App.xaml.cs 启动时从 ForkPlusSettings.Default + PATH + 内置 fallback 计算。
    ///
    /// Avalonia 工程当前没有等价的 App 静态属性初始化逻辑（ForkPlusSettings 未迁入 Core）。
    /// spike 阶段采用以下策略：
    /// - GitPath：用 which（Unix）/ where（Windows）命令查找 PATH 中的 git，缓存到字段
    /// - EnvironmentGitInstancePath：与 GitPath 同值
    /// - ForkGitInstancePath：null（Linux/macOS 没有内置 git；Windows spike 不打包便携 git）
    /// - ShellPath / BashPath：从 GitPath 派生（Windows）或返回 "sh" / "bash"（Unix）
    /// - OverrideCredentialHelper / OverrideCredentialHelperBt：null（无法读 ForkPlusSettings，使用默认）
    /// - AppName："ForkPlus"
    /// - CliArguments：Environment.GetCommandLineArgs()
    ///
    /// Core 工程中 95 处引用 ServiceLocator.GitEnvironment.Xxx（遍布 Git/Shell/Jobs/UI/Accounts）。
    /// spike 阶段确保所有调用拿到非 null 值，避免 NRE。
    /// Phase 0.4 把 ForkPlusSettings 迁入 Core 后升级为真实持久化实现（Phase 6.6b）。
    /// </summary>
    public class AvaloniaGitEnvironment : IGitEnvironment
    {
        // 懒加载字段（首次访问时计算并缓存，避免每次 which git 都 spawn 进程）
        private string _gitPath;
        private bool _gitPathResolved;
        private string _shellPath;
        private string _bashPath;

        /// <summary>用户在 Preferences 中配置的 credential helper 覆盖命令。
        /// spike 阶段无法读 ForkPlusSettings，返回 null（使用 git 默认 credential helper）。</summary>
        public string[] OverrideCredentialHelper => null;

        /// <summary>Biturbo 路径下的 credential helper 覆盖命令。
        /// spike 阶段无法读 ForkPlusSettings，返回 null。</summary>
        public string[] OverrideCredentialHelperBt => null;

        /// <summary>git 可执行文件完整路径。
        /// Unix 用 `which git`，Windows 用 `where git` 查找 PATH 中的 git。
        /// 找不到返回 null（Core 端 GitRequest 会报错，但不会 NRE）。</summary>
        public string GitPath
        {
            get
            {
                if (!_gitPathResolved)
                {
                    _gitPath = ResolveGitPathFromPath();
                    _gitPathResolved = true;
                }
                return _gitPath;
            }
        }

        /// <summary>环境 PATH 中的 git 实例路径。spike 阶段与 GitPath 同值。</summary>
        public string EnvironmentGitInstancePath => GitPath;

        /// <summary>Fork 内置的 git 实例路径。
        /// spike 阶段未打包便携 git，返回 null（Linux/macOS 永远是 null）。</summary>
        public string ForkGitInstancePath => null;

        /// <summary>应用名称。固定 "ForkPlus"（与 WPF App.AppName 一致）。</summary>
        public string AppName => "ForkPlus";

        /// <summary>原始命令行参数数组（等价 Environment.GetCommandLineArgs()）。</summary>
        public string[] CliArguments => Environment.GetCommandLineArgs();

        /// <summary>sh 路径。Windows 派生自 GitPath（&lt;gitDir&gt;\usr\bin\sh.exe），
        /// Unix 直接返回 "sh"（PATH 中即可）。</summary>
        public string ShellPath
        {
            get
            {
                if (_shellPath == null)
                {
                    _shellPath = ResolveShellPath("sh");
                }
                return _shellPath;
            }
        }

        /// <summary>bash 路径。Windows 派生自 GitPath（&lt;gitDir&gt;\usr\bin\bash.exe），
        /// Unix 直接返回 "bash"（PATH 中即可）。</summary>
        public string BashPath
        {
            get
            {
                if (_bashPath == null)
                {
                    _bashPath = ResolveShellPath("bash");
                }
                return _bashPath;
            }
        }

        /// <summary>从 PATH 中查找 git 可执行文件完整路径。
        /// Unix 用 `which git`，Windows 用 `where git`。失败返回 null。</summary>
        private static string ResolveGitPathFromPath()
        {
            string finder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
            string arg = "git";
            try
            {
                Process p = new Process();
                p.StartInfo = new ProcessStartInfo
                {
                    FileName = finder,
                    Arguments = arg,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit(3000);
                if (p.ExitCode != 0)
                {
                    return null;
                }
                // where/which 可能返回多行，取第一行；trim 末尾换行
                string[] lines = stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0)
                {
                    return null;
                }
                string path = lines[0].Trim();
                if (File.Exists(path))
                {
                    return path;
                }
                return null;
            }
            catch
            {
                // which/where 不存在或执行失败，返回 null 让 Core 端报错
                return null;
            }
        }

        /// <summary>派生 shell/bash 路径。
        /// Windows：从 GitPath 父目录 + \usr\bin\&lt;name&gt;.exe（便携 git 目录结构）。
        /// Unix：直接返回 name（sh/bash 在 PATH 中，ProcessStartInfo 不需要绝对路径）。</summary>
        private string ResolveShellPath(string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string git = GitPath;
                if (!string.IsNullOrEmpty(git))
                {
                    try
                    {
                        string gitDir = Path.GetDirectoryName(git);
                        if (!string.IsNullOrEmpty(gitDir))
                        {
                            string candidate = Path.Combine(gitDir, "usr", "bin", name + ".exe");
                            if (File.Exists(candidate))
                            {
                                return candidate;
                            }
                        }
                    }
                    catch
                    {
                        // 路径拼接失败，fallthrough 返回 name 让 Windows 自己找
                    }
                }
                return name + ".exe";
            }
            // Unix：sh/bash 一定在 /bin 或 /usr/bin，直接返回 name 即可
            return name;
        }
    }
}
