using System;
using System.Collections.Generic;
using System.IO;
// Avalonia spike 版 ExternalRepositoryEditor（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/ExternalRepositoryEditor.cs（374 行）：
//   - WPF: public abstract class ExternalRepositoryEditor
//   - 子类：Antigravity / Cursor / Fleet / OpenCode / VSCode / VSCodeInsiders /
//     SublimeText / Atom / WebStorm / Zed
//   - 属性：Name / ApplicationPath / Icon (ImageSource)
//   - GetAvailableEditors()：遍历 TryFindInstance 检测各编辑器是否存在
//   - FindExistingInstance(patterns)：ExpandEnvironmentVariables + 通配符匹配
//   - FindExecutableInPath(name)：在 PATH 中查找可执行文件
//   - 依赖：System.Windows.Media.ImageSource / IconTools.GetImageSourceForFile
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ImageSource → spike 用 object 替代（避免 Avalonia.Media.IImage 依赖）
//   2. WPF IconTools.GetImageSourceForFile → spike 跳过（返回 null）
//   3. WPF %programfiles% / %localappdata% 等 → spike 用 Environment.GetFolderPath
//   4. spike 跳过 OpenCode 的 FindExecutableInPath 复杂逻辑（简化为直接返回 null）
//
// spike 简化（task spec 关键 API）：
//   - 子类结构 + GetAvailableEditors + FindExistingInstance
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    public abstract class ExternalRepositoryEditor
    {
        public class Antigravity : ExternalRepositoryEditor
        {
            public override string Name => "Antigravity";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Antigravity", "Antigravity.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Antigravity", "Antigravity.exe")
                });
            }

            public Antigravity(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class Cursor : ExternalRepositoryEditor
        {
            public override string Name => "Cursor";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "cursor", "Cursor.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "cursor", "Cursor.exe")
                });
            }

            public Cursor(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class Fleet : ExternalRepositoryEditor
        {
            public override string Name => "Fleet";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Fleet", "Fleet.exe")
                });
            }

            public Fleet(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class OpenCode : ExternalRepositoryEditor
        {
            public override string Name => "OpenCode";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                // spike 版简化：跳过 FindExecutableInPath 逻辑
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "opencode.cmd"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "OpenCode.cmd"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "opencode.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm", "OpenCode.exe")
                });
            }

            public OpenCode(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class VSCode : ExternalRepositoryEditor
        {
            public override string Name => "Visual Studio Code";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code", "Code.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code", "Code.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code", "Code.exe")
                });
            }

            public VSCode(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class VSCodeInsiders : ExternalRepositoryEditor
        {
            public override string Name => "Visual Studio Code Insiders";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Microsoft VS Code Insiders", "Code - Insiders.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft VS Code Insiders", "Code - Insiders.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft VS Code Insiders", "Code - Insiders.exe")
                });
            }

            public VSCodeInsiders(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class SublimeText : ExternalRepositoryEditor
        {
            public override string Name => "Sublime Text";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                return FindExistingInstance(new string[] {
                    Path.Combine(pf, "Sublime Text", "sublime_text.exe"),
                    Path.Combine(pf, "Sublime Text 3", "sublime_text.exe"),
                    Path.Combine(pf, "Sublime Text 4", "sublime_text.exe")
                });
            }

            public SublimeText(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class Atom : ExternalRepositoryEditor
        {
            public override string Name => "Atom";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "atom", "atom.exe")
                });
            }

            public Atom(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class WebStorm : ExternalRepositoryEditor
        {
            public override string Name => "WebStorm";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains", "*", "bin", "webstorm64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WebStorm", "bin", "webstorm64.exe")
                });
            }

            public WebStorm(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        public class Zed : ExternalRepositoryEditor
        {
            public override string Name => "Zed";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Zed", "Zed.exe")
                });
            }

            public Zed(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        // ===== 基类 API =====
        public abstract string Name { get; }
        public abstract string ApplicationPath { get; }
        public abstract object Icon { get; }

        public static ExternalRepositoryEditor[] GetAvailableEditors()
        {
            List<ExternalRepositoryEditor> list = new List<ExternalRepositoryEditor>(5);
            string t = Antigravity.TryFindInstance();
            if (t != null) list.Add(new Antigravity(t));
            t = Atom.TryFindInstance();
            if (t != null) list.Add(new Atom(t));
            t = Cursor.TryFindInstance();
            if (t != null) list.Add(new Cursor(t));
            t = Fleet.TryFindInstance();
            if (t != null) list.Add(new Fleet(t));
            t = OpenCode.TryFindInstance();
            if (t != null) list.Add(new OpenCode(t));
            t = SublimeText.TryFindInstance();
            if (t != null) list.Add(new SublimeText(t));
            t = VSCode.TryFindInstance();
            if (t != null) list.Add(new VSCode(t));
            t = VSCodeInsiders.TryFindInstance();
            if (t != null) list.Add(new VSCodeInsiders(t));
            t = WebStorm.TryFindInstance();
            if (t != null) list.Add(new WebStorm(t));
            t = Zed.TryFindInstance();
            if (t != null) list.Add(new Zed(t));
            return list.ToArray();
        }

        protected static string FindExistingInstance(string[] patterns)
        {
            foreach (string pattern in patterns)
            {
                try
                {
                    string expanded = Environment.ExpandEnvironmentVariables(pattern);
                    string[] parts = expanded.Split(new string[] { "*\\" }, StringSplitOptions.None);
                    if (parts.Length == 1)
                    {
                        if (File.Exists(expanded)) return expanded;
                        continue;
                    }
                    if (parts.Length == 2)
                    {
                        string dir = parts[0];
                        string filePart = parts[1];
                        if (!Directory.Exists(dir)) continue;
                        string[] directories = Directory.GetDirectories(dir);
                        Array.Sort(directories, (x, y) => -1 * x.CompareTo(y));
                        foreach (string d in directories)
                        {
                            string candidate = Path.Combine(d, filePart);
                            if (File.Exists(candidate)) return candidate;
                        }
                        continue;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Log.Error($"Failed to find existing instance for '{pattern}'", ex);
                }
            }
            return null;
        }

        // spike 版：跳过 FindExecutableInPath 复杂逻辑（简化为直接返回 null）
        protected static string FindExecutableInPath(string executableName)
        {
            return null;
        }
    }
}
