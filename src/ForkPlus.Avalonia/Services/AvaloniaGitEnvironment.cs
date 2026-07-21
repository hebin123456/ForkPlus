using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using ForkPlus.Services;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Services
{
    /// <summary>
    /// IGitEnvironment 的 Avalonia 跨平台真实实现（Phase 0.4 / 6.6b 升级）。
    ///
    /// Phase 0.4 已把 ForkPlusSettings 从 WPF 迁入 Core，本实现可以直接读
    /// ForkPlusSettings.Default.GitInstancePath（用户在 Preferences 中配置的 git 路径）。
    ///
    /// GitPath 解析优先级（对照 WPF App.xaml.cs 第 142 行）：
    ///   1. EnvironmentGitInstancePath（环境变量 `forkgitinstance` 或 which/where git）
    ///   2. ForkPlusSettings.Default.GitInstancePath（用户偏好设置中的 git 路径）
    ///   3. ForkGitInstancePath（内置便携 git，仅 Windows 打包，Unix 永远 null）
    ///
    /// ShellPath / BashPath（对照 WPF App.xaml.cs 第 144/146 行）：
    ///   - Windows：派生自 GitPath 父目录（sh.exe / bash.exe）
    ///   - Unix：直接返回 "sh" / "bash"（PATH 中即可，ProcessStartInfo 不需要绝对路径）
    ///
    /// OverrideCredentialHelper / OverrideCredentialHelperBt：依赖 AccountManager
    /// （WPF 版 App.OverrideCredentialHelper 在 AccountManager.Current.Accounts.Length
    /// 为 0 时返回 _defaultCredentialHelper，否则返回 _overrideCredentialHelper）。
    /// Avalonia 工程尚未接入 AccountManager，本实现先返回 null（让 git 用默认
    /// credential helper），等 Phase 6.7+ AccountManager 接入后升级。
    /// </summary>
    public class AvaloniaGitEnvironment : IGitEnvironment
    {
        // 懒加载字段（首次访问时计算并缓存，避免每次 which git 都 spawn 进程）
        private string _environmentGitInstancePath;
        private bool _environmentGitInstancePathResolved;
        private string _gitPath;
        private bool _gitPathResolved;
        private string _shellPath;
        private string _bashPath;

        /// <summary>用户在 Preferences 中配置的 credential helper 覆盖命令。
        /// 依赖 AccountManager，Avalonia 工程尚未接入，先返回 null（使用 git 默认）。</summary>
        public string[] OverrideCredentialHelper => null;

        /// <summary>Biturbo 路径下的 credential helper 覆盖命令。
        /// 依赖 AccountManager，先返回 null。</summary>
        public string[] OverrideCredentialHelperBt => null;

        /// <summary>git 可执行文件完整路径。
        /// 优先级：环境变量 → ForkPlusSettings.Default.GitInstancePath → ForkGitInstancePath。</summary>
        public string GitPath
        {
            get
            {
                if (!_gitPathResolved)
                {
                    _gitPath = EnvironmentGitInstancePath
                        ?? ForkPlusSettings.Default.GitInstancePath
                        ?? ForkGitInstancePath;
                    _gitPathResolved = true;
                }
                return _gitPath;
            }
        }

        /// <summary>环境 PATH 中的 git 实例路径。
        /// 优先从环境变量 `forkgitinstance` 读取（对照 WPF App.GetEnvironmentGitInstancePath），
        /// 失败则用 which（Unix）/ where（Windows）查找 PATH 中的 git。
        /// 找不到返回 null（Core 端 GitRequest 会报错，但不会 NRE）。</summary>
        public string EnvironmentGitInstancePath
        {
            get
            {
                if (!_environmentGitInstancePathResolved)
                {
                    _environmentGitInstancePath = ResolveFromEnvVariable() ?? ResolveGitPathFromPath();
                    _environmentGitInstancePathResolved = true;
                }
                return _environmentGitInstancePath;
            }
        }

        /// <summary>Fork 内置的 git 实例路径。
        /// Windows：&lt;ForkDirectoryPath&gt;/gitInstance/2.50.1/bin/git.exe（便携 git，仅打包 Windows）。
        /// Unix：null（Linux/macOS 不打包便携 git，依赖系统 git）。</summary>
        public string ForkGitInstancePath
        {
            get
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return null;
                }
                // 对照 WPF App.GetForkGitInstancePath（App.xaml.cs 第 978 行）
                // ForkDirectoryPath 在 AvaloniaAppContext 中等价于 %LOCALAPPDATA%/ForkPlus
                string forkDirectoryPath = ServiceLocator.AppContext?.ForkDirectoryPath;
                if (string.IsNullOrEmpty(forkDirectoryPath))
                {
                    return null;
                }
                return Path.Combine(forkDirectoryPath, "gitInstance", "2.50.1", "bin", "git.exe");
            }
        }

        /// <summary>应用名称。固定 "ForkPlus"（与 WPF App.AppName 一致）。</summary>
        public string AppName => "ForkPlus";

        /// <summary>原始命令行参数数组（等价 Environment.GetCommandLineArgs()）。</summary>
        public string[] CliArguments => Environment.GetCommandLineArgs();

        /// <summary>sh 路径。
        /// Windows 派生自 GitPath 父目录的 sh.exe（对照 WPF App.ShellPath）。
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

        /// <summary>bash 路径。
        /// Windows 派生自 GitPath 父目录的 bash.exe（对照 WPF App.BashPath）。
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

        /// <summary>从环境变量 `forkgitinstance` 读取 git 路径。
        /// 对照 WPF App.GetEnvironmentGitInstancePath（App.xaml.cs 第 952-974 行）：
        ///   - 若值以 "git.exe" 结尾且文件存在，直接返回
        ///   - 否则把值当作目录，拼接 bin/git.exe 后返回（若存在）
        ///   - 不匹配则返回 null
        /// 注意：WPF 版只检查 "git.exe"，但 Unix 上环境变量可能直接指向 git，
        /// 所以这里同时检查 "git"（无扩展名）以兼容 Unix。</summary>
        private static string ResolveFromEnvVariable()
        {
            try
            {
                string envValue = Environment.GetEnvironmentVariable(Consts.ForkPlus.GitInstanceEnvVariable);
                if (envValue == null)
                {
                    return null;
                }
                bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                string gitExeName = isWindows ? "git.exe" : "git";

                // 直接指向 git 可执行文件
                if (envValue.EndsWith(gitExeName) && File.Exists(envValue))
                {
                    return envValue;
                }
                // 指向目录，拼接 bin/git(.exe)
                string combined = Path.Combine(envValue, "bin", gitExeName);
                if (File.Exists(combined))
                {
                    return combined;
                }
            }
            catch
            {
                // 环境变量读取失败，返回 null 让 ResolveGitPathFromPath 兜底
            }
            return null;
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
        /// Windows：从 GitPath 父目录查找 &lt;name&gt;.exe（对照 WPF App.ShellPath/BashPath，
        ///   WPF 版直接 Path.Combine 不检查存在性，这里加 File.Exists 防止返回不存在的路径）。
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
                            string candidate = Path.Combine(gitDir, name + ".exe");
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
