using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
// Avalonia spike 版 ExternalProjectEditor（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/ExternalProjectEditor.cs（323 行）：
//   - WPF: public abstract class ExternalProjectEditor
//   - 子类：IntelliJIdea / GoLand / PhpStorm / PyCharm / Rider / VisualStudio
//   - 属性：Name / ApplicationPath / Icon (ImageSource) / ProjectExtensions
//   - GetAvailableEditors()：遍历 TryFindInstance 检测各编辑器是否存在
//   - GetProjectFilePaths(gitDir)：按 ProjectExtensions 搜 .sln/.csproj 等文件
//   - OpenProject(path)：StartProcess(ApplicationPath, [path])
//   - FindExistingInstance(patterns)：ExpandEnvironmentVariables + 通配符匹配
//   - 依赖：System.Windows.Media.ImageSource / IconTools.GetImageSourceForFile
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ImageSource → spike 用 object 替代（避免 Avalonia.Media.IImage 依赖）
//   2. WPF IconTools.GetImageSourceForFile → spike 跳过（返回 null）
//   3. WPF Consts.Env.ProgramFiles / ProgramFiles86 → spike 用 Environment.GetFolderPath
//   4. WPF string[].Quotify() → spike 用 string.Join(" ", args)
//   5. spike 跳过 ProjectExtensions 多文件类型搜索（保留基类默认空数组）
//
// spike 简化（task spec 关键 API）：
//   - 子类结构 + GetAvailableEditors + GetProjectFilePaths + OpenProject
namespace ForkPlus.Avalonia
{
    // spike 版：放在 ForkPlus.Avalonia 命名空间（task spec：Manager 类用此命名空间）。
    public abstract class ExternalProjectEditor
    {
        // ===== IntelliJIdea 系列 =====
        public class IntelliJIdea : ExternalProjectEditor
        {
            public override string Name => "IntelliJ IDEA";
            public override string ApplicationPath { get; }
            public override object Icon { get; }

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains", "*", "bin", "idea64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "*", "bin", "idea64.exe")
                });
            }

            public IntelliJIdea(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null; // spike 版跳过 IconTools
            }
        }

        public class GoLand : IntelliJIdea
        {
            public override string Name => "GoLand";

            public new static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains", "*", "bin", "goland64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "GoLand", "bin", "goland64.exe")
                });
            }

            public GoLand(string applicationPath) : base(applicationPath) { }
        }

        public class PhpStorm : IntelliJIdea
        {
            public override string Name => "PhpStorm";

            public new static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains", "*", "bin", "phpstorm64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "PhpStorm", "bin", "phpstorm64.exe")
                });
            }

            public PhpStorm(string applicationPath) : base(applicationPath) { }
        }

        public class PyCharm : IntelliJIdea
        {
            public override string Name => "PyCharm";

            public new static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains", "*", "bin", "pycharm64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "*", "bin", "pycharm64.exe")
                });
            }

            public PyCharm(string applicationPath) : base(applicationPath) { }
        }

        // ===== Rider =====
        public class Rider : ExternalProjectEditor
        {
            public override string Name => "Rider";
            public override string ApplicationPath { get; }
            public override object Icon { get; }
            protected override string[] ProjectExtensions => new string[] { "*.sln", "*.slnf", "*.slnx", "*.csproj", "*.uproject" };

            public static string TryFindInstance()
            {
                return FindExistingInstance(new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "JetBrains", "*", "bin", "rider64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Rider", "bin", "rider64.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JetBrains", "Installations", "*", "bin", "rider64.exe")
                });
            }

            public Rider(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        // ===== Visual Studio =====
        public class VisualStudio : ExternalProjectEditor
        {
            public override string Name => "Visual Studio";
            public override string ApplicationPath { get; }
            public override object Icon { get; }
            protected override string[] ProjectExtensions => new string[] { "*.sln", "*.slnf", "*.slnx" };

            public static string TryFindInstance()
            {
                var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                return FindExistingInstance(new string[] {
                    Path.Combine(pf86, "Common Files", "Microsoft Shared", "MSEnv", "VSLauncher.exe"),
                    Path.Combine(pf, "Microsoft Visual Studio", "2022", "Enterprise", "Common7", "IDE", "devenv.exe"),
                    Path.Combine(pf, "Microsoft Visual Studio", "2022", "Professional", "Common7", "IDE", "devenv.exe"),
                    Path.Combine(pf, "Microsoft Visual Studio", "2022", "Community", "Common7", "IDE", "devenv.exe"),
                    Path.Combine(pf86, "Microsoft Visual Studio", "2019", "Enterprise", "Common7", "IDE", "devenv.exe"),
                    Path.Combine(pf86, "Microsoft Visual Studio", "2019", "Professional", "Common7", "IDE", "devenv.exe"),
                    Path.Combine(pf86, "Microsoft Visual Studio", "2019", "Community", "Common7", "IDE", "devenv.exe")
                });
            }

            public VisualStudio(string applicationPath)
            {
                ApplicationPath = applicationPath;
                Icon = null;
            }
        }

        // ===== 基类 API =====
        public abstract string Name { get; }
        public abstract string ApplicationPath { get; }
        public abstract object Icon { get; }

        protected virtual string[] ProjectExtensions => Array.Empty<string>();

        public static ExternalProjectEditor[] GetAvailableEditors()
        {
            List<ExternalProjectEditor> list = new List<ExternalProjectEditor>(2);
            string t = GoLand.TryFindInstance();
            if (t != null) list.Add(new GoLand(t));
            t = IntelliJIdea.TryFindInstance();
            if (t != null) list.Add(new IntelliJIdea(t));
            t = PhpStorm.TryFindInstance();
            if (t != null) list.Add(new PhpStorm(t));
            t = PyCharm.TryFindInstance();
            if (t != null) list.Add(new PyCharm(t));
            t = Rider.TryFindInstance();
            if (t != null) list.Add(new Rider(t));
            t = VisualStudio.TryFindInstance();
            if (t != null) list.Add(new VisualStudio(t));
            return list.ToArray();
        }

        public virtual string[] GetProjectFilePaths(string gitDirectory)
        {
            List<string> list = new List<string>();
            try
            {
                string[] projectExtensions = ProjectExtensions;
                foreach (string ext in projectExtensions)
                {
                    string trimExt = ext.TrimStart('*');
                    string[] files = Directory.GetFiles(gitDirectory, ext, SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        if (file.EndsWith(trimExt))
                        {
                            list.Add(file);
                        }
                    }
                    string srcPath = Path.Combine(gitDirectory, "src");
                    if (!Directory.Exists(srcPath)) continue;
                    files = Directory.GetFiles(srcPath, ext, SearchOption.TopDirectoryOnly);
                    foreach (string file in files)
                    {
                        if (file.EndsWith(trimExt))
                        {
                            list.Add(file);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
            }
            return list.ToArray();
        }

        public void OpenProject(string absoluteProjectFilePath)
        {
            StartProcess(ApplicationPath, new string[] { absoluteProjectFilePath });
        }

        protected static void StartProcess(string path, string[] arguments)
        {
            string argsStr = string.Join(" ", arguments);
            Process process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = path,
                Arguments = argsStr
            };
            Log.Info($"Running External Project Editor '{path} {argsStr}'");
            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to run external project editor '{path} {argsStr}'", ex);
            }
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
                    Log.Error($"Failed to find project editor instance for '{pattern}'", ex);
                }
            }
            return null;
        }
    }
}
