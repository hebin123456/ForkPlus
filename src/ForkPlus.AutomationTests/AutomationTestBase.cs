using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using FlaUI.Core;
using FlaUI.UIA3;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 系统测试基类：处理应用启动前置条件（git 路径、settings.json 预写）、
	/// 进程生命周期管理、临时 git 仓库创建。
	/// </summary>
	public abstract class AutomationTestBase : IDisposable
	{
		private const string SettingsDirName = "ForkPlus";
		private const string SettingsFileName = "settings.json";
		private const string GitInstanceEnvVariable = "forkgitinstance";
		private const string AutomationExeEnvVariable = "FORKPLUS_AUTOMATION_EXE";

		private static readonly object ProcessLock = new object();
		private bool _disposed;

		/// <summary>
		/// 启动 ForkPlus 并返回主窗口自动化对象。
		/// 调用方负责在使用完毕后 Dispose（基类的 Dispose 会关闭进程）。
		/// </summary>
		protected LaunchedApp LaunchApp(string arguments = null)
		{
			string exePath = ResolveAppPath();
			if (!File.Exists(exePath))
			{
				throw new FileNotFoundException(
					$"ForkPlus.exe 未找到于 {exePath}。请设置 {AutomationExeEnvVariable} 环境变量指向已编译的 exe。", exePath);
			}

			EnsureStartupPrerequisites();

			// 启动重试：CI 上偶尔出现进程刚启动即被关闭（FlaUI 抛 "Could not find process with id"）。
			// 重试 3 次，每次间隔 1 秒，覆盖应用启动期短暂抖动。
			Exception lastError = null;
			for (int attempt = 1; attempt <= 3; attempt++)
			{
				var startInfo = new ProcessStartInfo
				{
					FileName = exePath,
					UseShellExecute = false,
				};
				if (!string.IsNullOrWhiteSpace(arguments))
				{
					startInfo.Arguments = arguments;
				}

				// 设置 forkgitinstance 环境变量，让应用跳过 ConfigureGitInstanceWindow。
				// 优先使用 PATH 中的 git，其次使用常见安装路径。
				string gitPath = FindSystemGitPath();
				if (gitPath != null)
				{
					startInfo.EnvironmentVariables[GitInstanceEnvVariable] = gitPath;
				}

				Application app = null;
				UIA3Automation automation = null;
				try
				{
					app = Application.Launch(startInfo);
					automation = new UIA3Automation();
					// 等待主窗口出现：给应用充分时间初始化（CI runner 慢，30s 是经验值）。
					var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));
					if (window != null)
					{
						return new LaunchedApp(app, automation, window);
					}
				}
				catch (Exception ex)
				{
					lastError = ex;
					// 清理本次失败的实例，避免进程残留干扰下一次重试。
					try { automation?.Dispose(); } catch { }
					try { if (app != null && !app.HasExited) app.Kill(); } catch { }
					try { if (app != null) app.Dispose(); } catch { }
					if (attempt < 3)
					{
						System.Threading.Thread.Sleep(1000);
					}
				}
			}
			throw new InvalidOperationException("ForkPlus 启动 3 次均失败，详见内部异常。", lastError);
		}

		/// <summary>
		/// 等待指定标题的顶级窗口出现（用于对话框/子窗口检测）。
		/// 超时返回 null，调用方自行断言。
		/// </summary>
		protected FlaUI.Core.AutomationElements.Window WaitForTopLevelWindow(
			LaunchedApp app, string titleSubstring, TimeSpan? timeout = null)
		{
			var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
			while (DateTime.UtcNow < deadline)
			{
				// FlaUI 3.x: Application.GetAllTopLevelWindows(AutomationBase) 返回该进程的所有顶级窗口
				foreach (var win in app.Application.GetAllTopLevelWindows(app.Automation))
				{
					string title = win.Title ?? "";
					if (title.IndexOf(titleSubstring, StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return win;
					}
				}
				System.Threading.Thread.Sleep(300);
			}
			return null;
		}

		/// <summary>
		/// 在窗口后代里按文本查找第一个 MenuItem（用于菜单点击）。
		/// WPF 菜单未展开时子项不在 UIA 树里，调用方需先展开父菜单。
		/// 文本匹配会去掉 WPF 访问键前缀 "_"（如 "_File" → "File"），且不区分大小写。
		/// </summary>
		/// <remarks>
		/// 查找策略：先在主窗口后代里找；找不到再从桌面根找。
		/// 桌面根 fallback 覆盖两种场景：
		/// 1. WPF ContextMenu 是独立 popup HWND，不在主窗口视觉树里
		///    （Appearance 下拉的主题项、Custom Colors 等都在此列）
		/// 2. PART_MainMenu 在 CustomWindow 的 ControlTemplate 里，
		///    UIA 有时不把它当作 Window 的后代
		/// </remarks>
		protected FlaUI.Core.AutomationElements.MenuItem FindMenuItemByText(
			FlaUI.Core.AutomationElements.Window window, string text)
		{
			var found = FindMenuItemInElement(window, text);
			if (found != null)
			{
				return found;
			}
			// Fallback：从桌面根找（popup ContextMenu + ControlTemplate 元素）
			try
			{
				var desktop = window.Automation.GetDesktop();
				return FindMenuItemInElement(desktop, text);
			}
			catch
			{
				return null;
			}
		}

		private static FlaUI.Core.AutomationElements.MenuItem FindMenuItemInElement(
			FlaUI.Core.AutomationElements.AutomationElement root, string text)
		{
			var items = root.FindAllDescendants(
				cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.MenuItem));
			foreach (var item in items)
			{
				string name = (item.Name ?? "").Replace("_", "");
				if (name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return item as FlaUI.Core.AutomationElements.MenuItem;
				}
			}
			return null;
		}

		/// <summary>
		/// 点击工具栏 "Appearance" 下拉按钮，展开其上下文菜单。
		/// Appearance 按钮已通过 AutomationProperties.Name="Appearance" 暴露给 UIA。
		/// </summary>
		protected bool OpenAppearanceDropdown(LaunchedApp app)
		{
			// 优先按 AutomationId（x:Name）查找，最稳定，不依赖本地化
			var btn = app.Window.FindFirstDescendant(cf => cf.ByAutomationId("AppearanceToolbarDropdownButton"));
			if (btn == null)
			{
				// 备选：按 Name 查找（AutomationProperties.Name 设为 "Appearance"，未本地化）
				btn = app.Window.FindFirstDescendant(cf => cf.ByName("Appearance"));
			}
			if (btn == null)
			{
				// 最后备选：遍历多语言候选名
				string[] candidates = { "外观", "外觀", "Apparence", "Erscheinung", "Apariencia" };
				foreach (string name in candidates)
				{
					btn = app.Window.FindFirstDescendant(cf => cf.ByName(name));
					if (btn != null) break;
				}
			}
			if (btn == null) return false;
			try { btn.Click(); System.Threading.Thread.Sleep(800); return true; } catch { return false; }
		}

		/// <summary>
		/// 关闭指定窗口（按 Alt+F4 或点击关闭按钮）。
		/// </summary>
		protected void CloseWindow(FlaUI.Core.AutomationElements.Window window)
		{
			try { window.Close(); } catch { }
		}

		/// <summary>
		/// 展开主菜单的某个根项（File/Repository/View 等），让子菜单项出现在 UIA 树里。
		/// WPF 菜单是懒加载的：子项第一次展开时才创建，必须先 Expand/Click 父项才能找到子项。
		/// </summary>
		protected bool ExpandRootMenu(LaunchedApp app, string menuName)
		{
			var menu = FindMenuItemByText(app.Window, menuName);
			if (menu == null) return false;
			// 优先用 ExpandCollapse pattern（WPF MenuItem 支持）
			try
			{
				menu.Expand();
				System.Threading.Thread.Sleep(1000);
				return true;
			}
			catch { }
			// 备选：Click 触发展开
			try
			{
				menu.Click();
				System.Threading.Thread.Sleep(1500);
				return true;
			}
			catch { }
			return false;
		}

		/// <summary>
		/// 创建一个包含一次提交的临时 git 仓库，返回仓库根目录路径。
		/// 测试结束后由 Dispose 清理。
		/// </summary>
		protected string CreateTempGitRepo(string repoName = "test-repo")
		{
			string tempDir = Path.Combine(Path.GetTempPath(), "ForkPlus-ST-" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(tempDir);

			RunGit(tempDir, "init");
			RunGit(tempDir, "config", "user.name", "ST Test");
			RunGit(tempDir, "config", "user.email", "st@test.local");
			RunGit(tempDir, "config", "commit.gpgsign", "false");

			File.WriteAllText(Path.Combine(tempDir, "README.md"), "# Test Repository\n\nThis is a test repository for system testing.\n");
			RunGit(tempDir, "add", "README.md");
			RunGit(tempDir, "commit", "-m", "Initial commit");

			return tempDir;
		}

		/// <summary>
		/// 在已有仓库中追加一次提交，返回新增的文件路径。
		/// </summary>
		protected string AddCommit(string repoPath, string fileName, string content, string message)
		{
			string filePath = Path.Combine(repoPath, fileName);
			File.WriteAllText(filePath, content);
			RunGit(repoPath, "add", fileName);
			RunGit(repoPath, "commit", "-m", message);
			return filePath;
		}

		/// <summary>
		/// 在已有仓库中创建一个未提交的变更（修改工作目录）。
		/// </summary>
		protected void CreateUnstagedChange(string repoPath, string fileName, string content)
		{
			File.WriteAllText(Path.Combine(repoPath, fileName), content);
		}

		/// <summary>
		/// 解析 ForkPlus.exe 路径。优先使用环境变量，其次尝试 Debug/Release 默认输出路径。
		/// </summary>
		protected static string ResolveAppPath()
		{
			string configured = Environment.GetEnvironmentVariable(AutomationExeEnvVariable);
			if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
			{
				return configured;
			}

			string baseDir = AppDomain.CurrentDomain.BaseDirectory;
			string[] candidates =
			{
				Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\ForkPlus\bin\Debug\net472\ForkPlus.exe")),
				Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\ForkPlus\bin\Release\net472\ForkPlus.exe")),
			};
			foreach (string candidate in candidates)
			{
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}
			return configured ?? candidates[0];
		}

		/// <summary>
		/// 确保启动前置条件满足：预写 settings.json（跳过 WelcomeWindow）。
		/// </summary>
		private void EnsureStartupPrerequisites()
		{
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string settingsDir = Path.Combine(localAppData, SettingsDirName);
			Directory.CreateDirectory(settingsDir);

			string settingsPath = Path.Combine(settingsDir, SettingsFileName);

			// 写入最小化的 settings.json，包含非空 Guid 以跳过 WelcomeWindow。
			// 如果已有 settings.json，保留其内容但确保 Guid 非空。
			string json;
			if (File.Exists(settingsPath))
			{
				json = File.ReadAllText(settingsPath);
				if (!json.Contains("\"Guid\""))
				{
					// 如果没有 Guid 字段，补充一个
					json = json.TrimEnd('}', ' ', '\n', '\r', '\t') + ",\n  \"Guid\": \"st-test-guid-00000000\"\n}";
				}
			}
			else
			{
				json = "{\n  \"Guid\": \"st-test-guid-00000000\"\n}\n";
			}

			// 写入重试：CI 上多测试可能并发，settings.json 被另一进程占用时
			// File.WriteAllText 抛 IOException "being used by another process"。
			// 重试 10 次，每次间隔 200ms，总等待 2 秒覆盖典型文件锁窗口。
			WriteFileWithRetry(settingsPath, json);
		}

		/// <summary>
		/// 写文件带重试，处理 settings.json 在 CI 上被并发占用的场景。
		/// </summary>
		private static void WriteFileWithRetry(string path, string content)
		{
			for (int attempt = 1; attempt <= 10; attempt++)
			{
				try
				{
					File.WriteAllText(path, content, Encoding.UTF8);
					return;
				}
				catch (IOException) when (attempt < 10)
				{
					System.Threading.Thread.Sleep(200);
				}
			}
		}

		/// <summary>
		/// 查找系统 git.exe 路径，用于设置 forkgitinstance 环境变量。
		/// </summary>
		private static string FindSystemGitPath()
		{
			// 优先检查环境变量
			string existing = Environment.GetEnvironmentVariable(GitInstanceEnvVariable);
			if (!string.IsNullOrWhiteSpace(existing))
			{
				return existing;
			}

			// 检查常见安装路径
			string[] commonPaths =
			{
				@"C:\Program Files\Git",
				@"C:\Program Files (x86)\Git",
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Git"),
			};
			foreach (string dir in commonPaths)
			{
				if (Directory.Exists(dir))
				{
					return dir;
				}
			}

			// 尝试从 PATH 中查找 git
			try
			{
				using (var proc = new Process
				{
					StartInfo = new ProcessStartInfo
					{
						FileName = "where",
						Arguments = "git",
						UseShellExecute = false,
						RedirectStandardOutput = true,
						CreateNoWindow = true,
					},
				})
				{
					proc.Start();
					string output = proc.StandardOutput.ReadLine();
					proc.WaitForExit(5000);
					if (!string.IsNullOrWhiteSpace(output) && File.Exists(output))
					{
						// where git 返回 git.exe 路径，forkgitinstance 需要 git 安装根目录
						return Path.GetDirectoryName(Path.GetDirectoryName(output));
					}
				}
			}
			catch
			{
			}
			return null;
		}

		/// <summary>
		/// 在指定目录中执行 git 命令。
		/// </summary>
		private static void RunGit(string workingDir, params string[] args)
		{
			// 注意：参数不能直接用 string.Join(" ", args) 拼接，否则含空格的值
			// （如 commit message "Initial commit" 或 user.name "ST Test"）会被 git 拆成多个
			// token，导致 "Initial" 当 value、"commit" 当 pathspec，进而报
			// "pathspec 'commit' did not match any file(s)" 或 "not in a git directory"。
			// 这里手动给每个参数加双引号，含空格的值才会被正确作为一个参数传递。
			string arguments = string.Join(" ", System.Array.ConvertAll(args, QuoteArgument));
			using (var proc = new Process
			{
				StartInfo = new ProcessStartInfo
				{
					FileName = "git",
					Arguments = arguments,
					WorkingDirectory = workingDir,
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
				},
			})
			{
				proc.Start();
				proc.WaitForExit(15000);
				if (proc.ExitCode != 0)
				{
					string error = proc.StandardError.ReadToEnd();
					throw new InvalidOperationException($"git {arguments} failed in {workingDir}: {error}");
				}
			}
		}

		/// <summary>
		/// 给单个命令行参数加双引号（含空格或特殊字符时）。
		/// net472 的 ProcessStartInfo 不支持 .NET Core 的 ArgumentList，需手动引用。
		/// </summary>
		private static string QuoteArgument(string arg)
		{
			if (string.IsNullOrEmpty(arg))
			{
				return "\"\"";
			}
			// 已带引号或不含空格/特殊字符的参数无需再处理
			if (arg.StartsWith("\"") || (!arg.Contains(" ") && !arg.Contains("\t")))
			{
				return arg;
			}
			// 转义内部双引号并整体加双引号
			return "\"" + arg.Replace("\"", "\\\"") + "\"";
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}
			if (disposing)
			{
				// 清理可能残留的 ForkPlus 进程
				try
				{
					foreach (var proc in Process.GetProcessesByName("ForkPlus"))
					{
						try
						{
							proc.Kill();
							proc.WaitForExit(5000);
						}
						catch
						{
						}
					}
				}
				catch
				{
				}

				// 清理临时仓库
				try
				{
					string tempRoot = Path.GetTempPath();
					foreach (string dir in Directory.GetDirectories(tempRoot, "ForkPlus-ST-*"))
					{
						try
						{
							Directory.Delete(dir, recursive: true);
						}
						catch
						{
						}
					}
				}
				catch
				{
				}
			}
			_disposed = true;
		}
	}

	/// <summary>
	/// 已启动的应用实例，封装 FlaUI 对象，Dispose 时自动关闭。
	/// </summary>
	public sealed class LaunchedApp : IDisposable
	{
		private bool _disposed;

		public Application Application { get; }
		public UIA3Automation Automation { get; }
		public FlaUI.Core.AutomationElements.Window Window { get; private set; }

		public LaunchedApp(Application app, UIA3Automation automation, FlaUI.Core.AutomationElements.Window window)
		{
			Application = app;
			Automation = automation;
			Window = window;
		}

		/// <summary>
		/// 重新获取主窗口（在窗口可能被重新创建后调用）。
		/// </summary>
		public FlaUI.Core.AutomationElements.Window RefreshMainWindow()
		{
			Window = Application.GetMainWindow(Automation, TimeSpan.FromSeconds(30));
			return Window;
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			try
			{
				Automation?.Dispose();
			}
			catch
			{
			}
			try
			{
				if (Application != null && !Application.HasExited)
				{
					Application.Close();
					Application.Dispose();
				}
			}
			catch
			{
				try
				{
					Application?.Kill();
				}
				catch
				{
				}
			}
			_disposed = true;
		}
	}
}
