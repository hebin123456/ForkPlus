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

			Application app = Application.Launch(startInfo);
			var automation = new UIA3Automation();
			var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(30));

			return new LaunchedApp(app, automation, window);
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
			File.WriteAllText(settingsPath, json, Encoding.UTF8);
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
			string arguments = string.Join(" ", args);
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
