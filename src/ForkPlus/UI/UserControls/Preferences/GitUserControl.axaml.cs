using System;
using ForkPlus.UI.WpfCompat;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class GitUserControl : UserControl
	{
		public class GitInstanceItem
		{
			public string FileName { get; }

			public string GitPath { get; }

			public GitInstanceType GitInstanceType { get; }

			public static GitInstanceItem CreateEnvironmentGitInstance()
			{
				string text = GitVersion(App.EnvironmentGitInstancePath);
				if (text != null)
				{
					return new GitInstanceItem(text + " - ENV git instance " + App.EnvironmentGitInstancePath, App.EnvironmentGitInstancePath, GitInstanceType.Environment);
				}
				return null;
			}

			public static GitInstanceItem CreateLocalGitInstance()
			{
				string text = GitVersion(App.ForkGitInstancePath);
				if (text != null)
				{
					return new GitInstanceItem(text + " - Fork git instance", App.ForkGitInstancePath, GitInstanceType.Local);
				}
				return null;
			}

			public static GitInstanceItem CreateCustomGitInstance(string normalizedPath)
			{
				if (ValidatePath(normalizedPath))
				{
					string text = GitVersion(normalizedPath);
					if (text != null)
					{
						return new GitInstanceItem(text + " - " + normalizedPath, normalizedPath, GitInstanceType.Custom);
					}
				}
				return null;
			}

			public static GitInstanceItem CreateSystemGitInstance()
			{
				string text = TryFindExistingInstance(new string[3] { "%programfiles(x86)%\\Git\\bin\\git.exe", "%programfiles%\\Git\\bin\\git.exe", "%ProgramW6432%\\Git\\bin\\git.exe" });
				if (text != null)
				{
					string text2 = GitVersion(text);
					if (text2 != null)
					{
						return new GitInstanceItem(text2 + " - " + text, text, GitInstanceType.System);
					}
				}
				return null;
			}

			public static GitInstanceItem CreateSeparator()
			{
				return new GitInstanceItem(string.Empty, string.Empty, GitInstanceType.Separator);
			}

			public static GitInstanceItem CreateAddCustomGitInstance()
			{
				return new GitInstanceItem(PreferencesLocalization.Current("Custom Git Instance..."), string.Empty, GitInstanceType.AddCustom);
			}

			public static GitInstanceItem CreateAddCustomGitMmInstance()
			{
				return new GitInstanceItem(PreferencesLocalization.Current("Custom git-mm Instance..."), string.Empty, GitInstanceType.AddCustom);
			}

			public static GitInstanceItem CreateAddCustomGitAiInstance()
			{
				return new GitInstanceItem(PreferencesLocalization.Current("Custom git-ai Instance..."), string.Empty, GitInstanceType.AddCustom);
			}

			internal GitInstanceItem(string fileName, string path, GitInstanceType itemType)
			{
				FileName = fileName;
				GitPath = path;
				GitInstanceType = itemType;
			}

		private static string GitVersion(string path)
		{
			return GitUserControl.GetOrProbeVersionText("git", path);
		}

			private static string TryFindExistingInstance(string[] possiblePaths)
			{
				foreach (string text in possiblePaths)
				{
					try
					{
						string text2 = Environment.ExpandEnvironmentVariables(text);
						if (File.Exists(text2))
						{
							return text2;
						}
					}
					catch (Exception ex)
					{
						Log.Error("Failed to check '" + text + "' existence", ex);
					}
				}
				return null;
			}

			private static bool ValidatePath(string gitExecutablePath)
		{
			try
			{
				if (!File.Exists(gitExecutablePath))
				{
					new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find git instance at: '{0}'", gitExecutablePath)).ShowDialog();
					return false;
				}
				// Migration note：git 二进制名跨平台（原 "git.exe" 硬编码在 Unix 永远 false）。
				if (!SystemEnvironment.IsGitExecutable(gitExecutablePath))
				{
					new ErrorWindow(PreferencesLocalization.FormatCurrent("Invalid git binary: '{0}'", gitExecutablePath)).ShowDialog();
					return false;
				}
				string directoryName = Path.GetDirectoryName(gitExecutablePath);
				if (Directory.Exists(directoryName))
				{
					// Migration note：bash/sh 配套校验跨平台。Windows Git-for-Windows 布局在 git 同目录
					// 提供 bash.exe/sh.exe；Unix 上 bash/sh 通常在系统目录而非 git 同目录，
					// 故 Unix 下同目录不存在时回退系统 PATH 探测。
					bool isUnix = !OperatingSystem.IsWindows();
					string bashName = isUnix ? "bash" : "bash.exe";
					string shName = isUnix ? "sh" : "sh.exe";
					bool bashOk = File.Exists(Path.Combine(directoryName, bashName)) || (isUnix && SystemEnvironment.ExistsOnPath(bashName));
					bool shOk = File.Exists(Path.Combine(directoryName, shName)) || (isUnix && SystemEnvironment.ExistsOnPath(shName));
					if (!bashOk)
					{
						new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find git instance at: '{0}'. Missing bash.exe", gitExecutablePath)).ShowDialog();
						return false;
					}
					if (!shOk)
					{
						new ErrorWindow(PreferencesLocalization.FormatCurrent("Cannot find git instance at: '{0}'. Missing sh.exe", gitExecutablePath)).ShowDialog();
						return false;
					}
				}
				}
				catch (Exception ex)
				{
					Log.Error("Path validation failed '" + gitExecutablePath + "'", ex);
				}
				return true;
			}
		}

		public enum GitInstanceType
		{
			Environment,
			Local,
			System,
			Custom,
			Separator,
			AddCustom
		}

		private static readonly string VerboseGitOutputTooltip = "GIT_TRACE=true\nEnables general trace output. Shows internal Git operations like command execution, file operations, and subprocess spawning.\n\nGIT_TRACE_CURL=true\nEnables verbose output from libcurl for HTTP/HTTPS operations. Shows request/response headers, SSL handshake details, and transfer progress when using HTTP-based remotes.\n\nGIT_SSH_COMMAND=\"ssh -vvv\"\nSets the SSH command with maximum verbosity (-vvv). Shows detailed SSH connection debugging: key exchange, authentication attempts, channel operations, and protocol negotiation when using SSH-based remotes.\n\nGIT_TRACE_PACKFILE=true\nTraces packfile operations. Shows details about how Git packs and unpacks objects during fetch/push operations.\n\nGIT_TRACE_PERFORMANCE=true\nShows performance timing data. Reports how long various Git operations take, useful for diagnosing slow operations.";

		private DelayedAction<UserIdentity> _updateAvatarAction;

	private ForkPlusDialogWindow _parentWindow;

	private bool _isRefreshingGitMm;

	/// <summary>刷新 git-ai 实例下拉框期间的程序化选中抑制（与 git-mm 同模式）。</summary>
	private bool _isRefreshingGitAi;

		// Migration note：RefreshGitInstanceComboBox 程序化设置 SelectedItem 会触发
		// SelectionChanged → WarnIfGitVersionUnsupported，导致每次打开偏好设置都弹一次
		// 版本警告（启动时 GitVersionChecker 已弹过，重复噪音）。刷新期间抑制。
		private bool _suppressVersionWarning;

		// 优化（2026-09-18，"偏好设置打开有点慢"）：三个实例下拉的版本探测（git/git-mm/
		// git-ai --version）各是一次子进程 spawn，打开偏好设置时最多 ~10 次串行执行
		// （Windows 单次 50-300ms）全部在 UI 线程构造器里跑，窗口要 1-3s 才出现且每次
		// 打开都重来。改为按 工具|路径 缓存探测 Task（进程生命周期内每个可执行文件只探
		// 一次），并新增 WarmVersionProbes 在窗口构造时后台并行预热——Initialize 里的同步
		// 读取与预热经 GetOrAdd 共享同一 in-flight Task，首开阻塞 ≈ 最慢单次探测而非串行
		// 总和，重开命中缓存零子进程。探测本体（GitRequest/ShellRequest.Execute）是纯
		// 进程 I/O 无 UI 依赖，可安全在线程池执行。
		private static readonly ConcurrentDictionary<string, Task<string>> _versionTextProbes = new ConcurrentDictionary<string, Task<string>>();

		private static string ProbeVersionText(string toolKey, string path)
		{
			switch (toolKey)
			{
			case "git":
			{
				GitCommandResult<string> gitCommandResult = new GetGitVersionGitCommand().Execute(path);
				return gitCommandResult.Succeeded ? gitCommandResult.Result : null;
			}
			case "gitmm":
			{
				GitCommandResult<string> result = new GetGitMmVersionShellCommand().Execute(path);
				return result.Succeeded ? result.Result : null;
			}
			case "gitai":
			{
				GitAiVersionCheckResult check = GitAiVersionChecker.Check(path);
				if (check.Status == GitAiVersionStatus.NotFound || check.Status == GitAiVersionStatus.Unknown || check.Version == null)
				{
					return null;
				}
				return check.Version.ToString(3);
			}
			default:
				return null;
			}
		}

		/// <summary>启动（或复用）指定 工具|路径 的后台探测 Task，不等待结果。</summary>
		private static void BeginProbe(string toolKey, string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return;
			}
			_versionTextProbes.GetOrAdd(toolKey + "|" + path, delegate
			{
				return Task.Run(delegate
				{
					return ProbeVersionText(toolKey, path);
				});
			});
		}

		/// <summary>同步取版本文本：命中缓存（或等待预热中的同一 Task）后返回；失败返回 null。</summary>
		private static string GetOrProbeVersionText(string toolKey, string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return null;
			}
			BeginProbe(toolKey, path);
			try
			{
				return _versionTextProbes.TryGetValue(toolKey + "|" + path, out Task<string> task) ? task.Result : null;
			}
			catch (Exception ex)
			{
				Log.Error("Version probe failed for '" + path + "'", ex);
				return null;
			}
		}

		// 优化（2026-09-18 续，"打开偏好设置仍卡很长时间"）：git-mm/git-ai 常为 Node CLI，
		// --version 冷启动秒级，即使经 WarmVersionProbes 并行预热，Initialize 里同步等
		// task.Result 仍会把首开卡在最慢单次探测上。版本文本只影响下拉项 label（项的存在
		// 性由路径候选决定、选中按路径匹配，两者都不依赖版本结果），故改为：探测未完成先
		// 显示占位符，探测完成后回 UI 线程异步刷新——打开路径上不再等待任何慢探测。
		private const string PendingVersionText = "…";

		/// <summary>非阻塞取探测结果：任务已完成返回 true（text 为结果，探测失败为 null）；未完成/未启动返回 false。</summary>
		private static bool TryGetCompletedVersionText(string toolKey, string path, out string text)
		{
			text = null;
			if (string.IsNullOrEmpty(path))
			{
				return true;
			}
			if (_versionTextProbes.TryGetValue(toolKey + "|" + path, out Task<string> task) && task.IsCompleted)
			{
				try
				{
					if (task.Status != TaskStatus.Faulted)
					{
						text = task.Result;
					}
				}
				catch
				{
				}
				return true;
			}
			return false;
		}

		/// <summary>下拉项版本文本：探测已完成 → 真实文本（失败回退 unknown）；未完成 → 启动/复用探测、登记 pending 并显示占位符。</summary>
		private static string VersionLabelText(string toolKey, string path, List<string> pendingPaths)
		{
			if (TryGetCompletedVersionText(toolKey, path, out string version))
			{
				return version ?? PreferencesLocalization.Current("unknown");
			}
			BeginProbe(toolKey, path);
			pendingPaths.Add(path);
			return PendingVersionText;
		}

		/// <summary>等待 pending 探测完成后回 UI 线程重跑 refresh（await 捕获 UI 线程上下文；
		/// refresh 幂等——届时探测已全部完成，标签取真实值且不再登记 pending，循环终止）。</summary>
		private static async void RefreshWhenProbesCompleteAsync(string toolKey, List<string> pendingPaths, Action refresh)
		{
			List<Task<string>> pending = new List<Task<string>>();
			foreach (string path in pendingPaths)
			{
				if (_versionTextProbes.TryGetValue(toolKey + "|" + path, out Task<string> task))
				{
					pending.Add(task);
				}
			}
			if (pending.Count == 0)
			{
				return;
			}
			try
			{
				await Task.WhenAll(pending);
			}
			catch
			{
			}
			refresh();
		}

		/// <summary>
		/// 后台并行预热 git/git-mm/git-ai 版本探测缓存（候选与三个 Refresh*InstanceComboBox
		/// 同口径：git 的 ENV/内置/系统安装位/自定义 + git-mm/git-ai 的 PATH/git 同目录/
		/// 系统位置/自定义）。偏好窗口构造时调用。
		/// </summary>
		public static void WarmVersionProbes()
		{
			Task.Run(delegate
			{
				try
				{
					// git 实例
					BeginProbe("git", App.EnvironmentGitInstancePath);
					BeginProbe("git", App.ForkGitInstancePath);
					string[] array = new string[3] { "%programfiles(x86)%\\Git\\bin\\git.exe", "%programfiles%\\Git\\bin\\git.exe", "%ProgramW6432%\\Git\\bin\\git.exe" };
					foreach (string text in array)
					{
						try
						{
							string expanded = Environment.ExpandEnvironmentVariables(text);
							if (File.Exists(expanded))
							{
								BeginProbe("git", expanded);
							}
						}
						catch
						{
						}
					}
					string savedGit = ForkPlusSettings.Default.GitInstancePath;
					if (!string.IsNullOrWhiteSpace(savedGit) && File.Exists(savedGit))
					{
						BeginProbe("git", savedGit);
					}
					// git-mm 实例
					BeginProbe("gitmm", App.GitMmPathFromPath);
					BeginProbe("gitmm", GitSiblingPath(App.GitMmExecutableName));
					BeginProbe("gitmm", App.GitMmPathFromSystemLocations);
					string savedGitMm = ForkPlusSettings.Default.GitMmInstancePath;
					if (!string.IsNullOrWhiteSpace(savedGitMm) && File.Exists(savedGitMm))
					{
						BeginProbe("gitmm", savedGitMm);
					}
					// git-ai 实例
					BeginProbe("gitai", App.GitAiPathFromPath);
					BeginProbe("gitai", GitSiblingPath(OperatingSystem.IsWindows() ? "git-ai.exe" : "git-ai"));
					BeginProbe("gitai", App.GitAiPathFromSystemLocations);
					string savedGitAi = ForkPlusSettings.Default.GitAiInstancePath;
					if (!string.IsNullOrWhiteSpace(savedGitAi) && File.Exists(savedGitAi))
					{
						BeginProbe("gitai", savedGitAi);
					}
				}
				catch (Exception ex)
				{
					Log.Error("WarmVersionProbes failed", ex);
				}
			});
		}

		/// <summary>git 可执行文件同目录下的指定工具路径（供预热，失败返回 null）。</summary>
		private static string GitSiblingPath(string executableName)
		{
			try
			{
				string gitDir = Path.GetDirectoryName(App.GitPath);
				return (gitDir == null) ? null : Path.Combine(gitDir, executableName);
			}
			catch
			{
				return null;
			}
		}

		public GitUserControl()
		{
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			_updateAvatarAction = new DelayedAction<UserIdentity>(UpdateAvatar, 0.3);
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
	{
		_parentWindow = parentWindow;
		RefreshGitInstanceComboBox();
		RefreshGitMmInstanceComboBox();
		RefreshGitAiInstanceComboBox();
		VerboseGitOutputCheckBox.IsChecked = ForkPlusSettings.Default.VerboseGitOutput;
		global::Avalonia.Controls.ToolTip.SetTip(VerboseGitOutputCheckBox,new TextBlock
		{
			MaxWidth = 500.0,
			TextWrapping = TextWrapping.Wrap,
			Text = VerboseGitOutputTooltip
		});
		// git-ai AI 归属开关（Blame 徽标 / 统计）。tooltip 用原文英文，
		// 由 PreferencesWindow 的本地化遍历按当前语言翻译（字符串 tip 才会被翻译）。
		AiAttributionCheckBox.IsChecked = ForkPlusSettings.Default.AiAttributionEnabled;
		AiCheckpointReportingCheckBox.IsChecked = ForkPlusSettings.Default.AiCheckpointReportingEnabled;
		global::Avalonia.Controls.ToolTip.SetTip(AiCheckpointReportingCheckBox, "When ForkPlus AI (AI development / code review) modifies files, report a git-ai checkpoint so the edits are attributed as AI-generated");
		UserIdentity result = new GetGlobalUserIdentityGitCommand().Execute().Result;
		UserNameTextBox.Text = result.Name ?? "";
		EmailTextBox.Text = result.Email ?? "";
		_updateAvatarAction.InvokeNow(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
	}

		private void VerboseGitOutputCheckBox_Checked(object sender, RoutedEventArgs e)
		{
			ForkPlusSettings.Default.VerboseGitOutput = VerboseGitOutputCheckBox.IsChecked.GetValueOrDefault();
		}

		private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
		{
			e.Uri.OpenInBrowser();
			e.Handled = true;
		}

		private void UserNameTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			SetGlobalUserIdentity();
		}

		private void EmailTextBox_LostFocus(object sender, RoutedEventArgs e)
		{
			SetGlobalUserIdentity();
		}

		private void UserNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
		}

		private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			_updateAvatarAction.InvokeWithDelay(new UserIdentity(UserNameTextBox.Text, EmailTextBox.Text));
		}

		private void UpdateAvatar(UserIdentity userIdentity)
		{
			AuthorAvatarImage.ShowAvatarNoCache(userIdentity);
		}

		private async void SetGlobalUserIdentity()
		{
			try
			{
				string userName = UserNameTextBox.Text.Trim();
				string email = EmailTextBox.Text.Trim();
				GitCommandResult gitCommandResult = await Task.Run(delegate
				{
					GitCommandResult gitCommandResult2 = new SetGlobalUserIdentityGitCommand().Execute(new UserIdentity(userName, email));
					return (!gitCommandResult2.Succeeded) ? gitCommandResult2 : GitCommandResult.Success();
				});
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(null, gitCommandResult.Error).ShowDialog();
				}
			}
			catch (Exception ex)
			{
				Log.Error("SetGlobalUserIdentity failed", ex);
			}
		}

		private void GitInstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			GitInstanceItem selectedItem = ((e.RemovedItems.Count > 0) ? (e.RemovedItems[0] as GitInstanceItem) : null);
			if (!(GitInstanceComboBox.SelectedItem is GitInstanceItem gitInstanceItem))
			{
				return;
			}
			switch (gitInstanceItem.GitInstanceType)
			{
			case GitInstanceType.Local:
				ForkPlusSettings.Default.GitInstancePath = null;
				break;
			case GitInstanceType.System:
				ForkPlusSettings.Default.GitInstancePath = gitInstanceItem.GitPath;
				break;
			case GitInstanceType.Custom:
				ForkPlusSettings.Default.GitInstancePath = gitInstanceItem.GitPath;
				break;
			case GitInstanceType.AddCustom:
			{
				// Bug 修复（2026-09-07，"自定义 git 实例的下拉框在弹出选择文件的资源管理器后没法消失，
				// 只有资源管理器关了才消失"）：SelectionChanged 里同步阻塞弹文件对话框（StorageProvider
				// 经 PushFrame 嵌套消息循环），ComboBox 自己"点击项后收起下拉"的处理排在本次事件之后，
				// 阻塞期间下拉恒开。弹对话框前显式收起（Linux/macOS 的 portal 对话框是独立进程，
				// 不会夺走本窗口焦点触发 Avalonia 的失焦自动收起）。
				GitInstanceComboBox.IsDropDownOpen = false;
				string initialDirectory = SystemEnvironment.UserProfileDirectory;
				if (OpenDialog.SelectExecutableFile(_parentWindow, PreferencesLocalization.Current("Select git instance"), initialDirectory, out var filePath))
				{
					string gitInstancePath = PathHelper.Normalize(filePath);
					ForkPlusSettings.Default.GitInstancePath = gitInstancePath;
					RefreshGitInstanceComboBox();
				}
				else
				{
					GitInstanceComboBox.SelectedItem = selectedItem;
				}
				break;
			}
			}
			Log.Info("Git Location: " + App.GitPath);
			if (!_suppressVersionWarning)
			{
				WarnIfGitVersionUnsupported(App.GitPath);
			}
		}

		/// <summary>
		/// 选中的 git 版本过低时弹警告（不阻止选择）。
		/// </summary>
		private static void WarnIfGitVersionUnsupported(string gitPath)
		{
			try
			{
				GitVersionCheckResult result = GitVersionChecker.Check(gitPath);
				if (result.Status == GitVersionStatus.Unsupported)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string minText = GitVersionChecker.MinimumRequiredVersion.ToString(2);
					new ErrorWindow(PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is older than the required {1}. Some features (diff, status, empty-changes detection) may not work correctly. Please upgrade git.",
						versionText, minText)).ShowDialog();
				}
				else if (result.Status == GitVersionStatus.Outdated)
				{
					string versionText = result.Version != null ? result.Version.ToString(3) : "?";
					string recText = GitVersionChecker.RecommendedVersion.ToString(2);
					new ErrorWindow(PreferencesLocalization.FormatCurrent(
						"Detected git version {0} is below the recommended {1}. Consider upgrading for better compatibility.",
						versionText, recText)).ShowDialog();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check git version on selection", ex);
			}
		}

		private void RefreshGitInstanceComboBox()
		{
			_suppressVersionWarning = true;
			try
			{
				DoRefreshGitInstanceComboBox();
			}
			finally
			{
				_suppressVersionWarning = false;
			}
		}

		private void DoRefreshGitInstanceComboBox()
		{
			List<GitInstanceItem> list = new List<GitInstanceItem>(5);
			GitInstanceItem gitInstanceItem = GitInstanceItem.CreateEnvironmentGitInstance();
			if (gitInstanceItem != null)
			{
				list.Add(gitInstanceItem);
			}
			GitInstanceItem gitInstanceItem2 = GitInstanceItem.CreateLocalGitInstance();
			if (gitInstanceItem2 != null)
			{
				list.Add(gitInstanceItem2);
			}
			GitInstanceItem gitInstanceItem3 = GitInstanceItem.CreateSystemGitInstance();
			if (gitInstanceItem3 != null)
			{
				list.Add(gitInstanceItem3);
			}
			string currentGitInstancePath = ForkPlusSettings.Default.GitInstancePath;
			GitInstanceItem gitInstanceItem4 = null;
			if (currentGitInstancePath != null && !list.ContainsItem((GitInstanceItem x) => x.GitPath == currentGitInstancePath))
			{
				gitInstanceItem4 = GitInstanceItem.CreateCustomGitInstance(currentGitInstancePath);
				if (gitInstanceItem4 != null)
				{
					list.Add(gitInstanceItem4);
				}
			}
			list.Add(GitInstanceItem.CreateSeparator());
			list.Add(GitInstanceItem.CreateAddCustomGitInstance());
			GitInstanceComboBox.ItemsSource = list.ToArray();
			GitInstanceComboBox.IsEnabled = true;
			if (gitInstanceItem != null)
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem;
				GitInstanceComboBox.IsEnabled = false;
			}
			else if (currentGitInstancePath == null)
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem2;
			}
			else if (gitInstanceItem3 != null && currentGitInstancePath == gitInstanceItem3.GitPath)
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem3;
			}
			else
			{
				GitInstanceComboBox.SelectedItem = gitInstanceItem4 ?? gitInstanceItem2;
			}
		}

		/// <summary>
	/// 填充 git-mm 实例下拉框。候选项：PATH 中发现的 git-mm、git 同目录与系统位置
	/// （系统 git exec-path / 用户 bin）发现的 git-mm、用户已保存的自定义路径、
	/// 以及"添加自定义..."入口。未找到任何 git-mm 时仍展示"添加自定义..."以便用户手动指定。
	/// </summary>
	private void RefreshGitMmInstanceComboBox()
	{
		_isRefreshingGitMm = true;
		try
		{
			List<string> pending = new List<string>();
			List<GitInstanceItem> list = new List<GitInstanceItem>(4);
			// 1. PATH 中查找的 git-mm（走缓存；版本文本经 VersionLabelText 非阻塞取值，未完成显示占位符）
			string pathCandidate = App.GitMmPathFromPath;
			if (!string.IsNullOrWhiteSpace(pathCandidate))
			{
				string label = VersionLabelText("gitmm", pathCandidate, pending) + " - " + pathCandidate;
				list.Add(new GitInstanceItem(label, pathCandidate, GitInstanceType.System));
			}
			// 2. git 可执行文件同目录的 git-mm（跨平台命名，2026-09-07：原硬编码 git-mm.exe 在 Unix 上永远找不到）
			try
			{
				string gitDir = Path.GetDirectoryName(App.GitPath);
				if (gitDir != null)
				{
					string sibling = Path.Combine(gitDir, App.GitMmExecutableName);
					if (File.Exists(sibling) && (pathCandidate == null || !string.Equals(pathCandidate, sibling, StringComparison.OrdinalIgnoreCase)))
					{
						string label = VersionLabelText("gitmm", sibling, pending) + " - " + sibling;
						list.Add(new GitInstanceItem(label, sibling, GitInstanceType.Local));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check git-mm in git directory", ex);
			}
			// 2b. 系统位置发现的 git-mm（系统 git 的 exec-path / 用户 bin——Linux 上"命令行可用
			// 但 GUI 找不到"的两处根因位置，2026-09-07 git mm 子命令可见性修复的探测面）
			string systemCandidate = App.GitMmPathFromSystemLocations;
			if (!string.IsNullOrWhiteSpace(systemCandidate) && !list.ContainsItem((GitInstanceItem x) => string.Equals(x.GitPath, systemCandidate, StringComparison.OrdinalIgnoreCase)))
			{
				string label = VersionLabelText("gitmm", systemCandidate, pending) + " - " + systemCandidate;
				list.Add(new GitInstanceItem(label, systemCandidate, GitInstanceType.System));
			}
			// 3. 用户已保存的自定义路径（若不在上述候选中）
			string savedPath = ForkPlusSettings.Default.GitMmInstancePath;
			if (!string.IsNullOrWhiteSpace(savedPath) && !list.ContainsItem((GitInstanceItem x) => string.Equals(x.GitPath, savedPath, StringComparison.OrdinalIgnoreCase)))
			{
				if (File.Exists(savedPath))
				{
					string label = VersionLabelText("gitmm", savedPath, pending) + " - " + savedPath;
					list.Add(new GitInstanceItem(label, savedPath, GitInstanceType.Custom));
				}
			}
			list.Add(GitInstanceItem.CreateSeparator());
			list.Add(GitInstanceItem.CreateAddCustomGitMmInstance());
			GitMmInstanceComboBox.ItemsSource = list.ToArray();
			// 选中当前生效的路径；未找到时不选中任何项（不 fallback 到 AddCustom，避免在构造期间弹出文件对话框）
			string current = App.GitMmPath;
			GitInstanceItem match = list.FirstOrDefault((GitInstanceItem x) => x.GitInstanceType != GitInstanceType.Separator && x.GitInstanceType != GitInstanceType.AddCustom && string.Equals(x.GitPath, current, StringComparison.OrdinalIgnoreCase));
			GitMmInstanceComboBox.SelectedItem = match;
			// 版本探测仍在跑（占位标签）→ 完成后异步刷新为真实标签
			if (pending.Count > 0)
			{
				RefreshWhenProbesCompleteAsync("gitmm", pending, RefreshGitMmInstanceComboBox);
			}
		}
		finally
		{
			_isRefreshingGitMm = false;
		}
	}

	private void GitMmInstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		// 刷新期间程序化设置 SelectedItem 会触发 SelectionChanged，跳过避免副作用（弹文件对话框/写磁盘）
		if (_isRefreshingGitMm)
		{
			return;
		}
		GitInstanceItem previous = (e.RemovedItems.Count > 0) ? (e.RemovedItems[0] as GitInstanceItem) : null;
		if (!(GitMmInstanceComboBox.SelectedItem is GitInstanceItem item))
		{
			return;
		}
		switch (item.GitInstanceType)
		{
		case GitInstanceType.System:
		case GitInstanceType.Local:
		case GitInstanceType.Custom:
			ForkPlusSettings.Default.GitMmInstancePath = item.GitPath;
			break;
		case GitInstanceType.AddCustom:
		{
			// Bug 修复（2026-09-07，"自定义 git-mm 实例的下拉框在弹出选择文件的资源管理器后没法消失"）：
			// 同 GitInstanceComboBox_SelectionChanged——弹对话框前显式收起下拉（详见彼处注释）。
			GitMmInstanceComboBox.IsDropDownOpen = false;
			string initialDirectory = SystemEnvironment.UserProfileDirectory;
			if (OpenDialog.SelectExecutableFile(_parentWindow, PreferencesLocalization.Current("Select git-mm instance"), initialDirectory, out var filePath))
			{
				string normalized = PathHelper.Normalize(filePath);
				ForkPlusSettings.Default.GitMmInstancePath = normalized;
				ForkPlusSettings.Default.Save();
				RefreshGitMmInstanceComboBox();
			}
			else
			{
				GitMmInstanceComboBox.SelectedItem = previous;
			}
			break;
		}
		}
		Log.Info("git-mm Location: " + (App.GitMmPath ?? "(none)"));
	}

	/// <summary>
	/// 填充 git-ai 实例下拉框（与 git-mm 同模式）。候选项：PATH 中发现的 git-ai、
	/// git 可执行文件同目录的 git-ai、用户已保存的自定义路径，以及"添加自定义..."入口。
	/// 未找到任何 git-ai 时仍展示"添加自定义..."以便用户手动指定（未安装时 AI 归属自动降级）。
	/// </summary>
	private void RefreshGitAiInstanceComboBox()
	{
		_isRefreshingGitAi = true;
		try
		{
			List<string> pending = new List<string>();
			List<GitInstanceItem> list = new List<GitInstanceItem>(4);
			// 1. PATH 中查找的 git-ai（走缓存；版本文本经 VersionLabelText 非阻塞取值，未完成显示占位符）
			string pathCandidate = App.GitAiPathFromPath;
			if (!string.IsNullOrWhiteSpace(pathCandidate))
			{
				string label = VersionLabelText("gitai", pathCandidate, pending) + " - " + pathCandidate;
				list.Add(new GitInstanceItem(label, pathCandidate, GitInstanceType.System));
			}
			// 2. git 可执行文件同目录的 git-ai（跨平台可执行名）
			try
			{
				string gitDir = Path.GetDirectoryName(App.GitPath);
				if (gitDir != null)
				{
					string sibling = Path.Combine(gitDir, OperatingSystem.IsWindows() ? "git-ai.exe" : "git-ai");
					if (File.Exists(sibling) && (pathCandidate == null || !string.Equals(pathCandidate, sibling, StringComparison.OrdinalIgnoreCase)))
					{
						string label = VersionLabelText("gitai", sibling, pending) + " - " + sibling;
						list.Add(new GitInstanceItem(label, sibling, GitInstanceType.Local));
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to check git-ai in git directory", ex);
			}
			// 2b. 系统位置发现的 git-ai（各 git 的 exec-path / 用户 bin / 用户 shell 环境——
			// git-ai 官方 install.sh 装到 ~/.git-ai/bin 且只把 PATH 写进 shell rc，桌面启动的
			// GUI 进程两处都看不到，2026-09-07 git-ai 可见性修复的探测面，与 git-mm 同模式）
			string systemCandidate = App.GitAiPathFromSystemLocations;
			if (!string.IsNullOrWhiteSpace(systemCandidate) && !list.ContainsItem((GitInstanceItem x) => string.Equals(x.GitPath, systemCandidate, StringComparison.OrdinalIgnoreCase)))
			{
				string label = VersionLabelText("gitai", systemCandidate, pending) + " - " + systemCandidate;
				list.Add(new GitInstanceItem(label, systemCandidate, GitInstanceType.System));
			}
			// 3. 用户已保存的自定义路径（若不在上述候选中）
			string savedPath = ForkPlusSettings.Default.GitAiInstancePath;
			if (!string.IsNullOrWhiteSpace(savedPath) && !list.ContainsItem((GitInstanceItem x) => string.Equals(x.GitPath, savedPath, StringComparison.OrdinalIgnoreCase)))
			{
				if (File.Exists(savedPath))
				{
					string label = VersionLabelText("gitai", savedPath, pending) + " - " + savedPath;
					list.Add(new GitInstanceItem(label, savedPath, GitInstanceType.Custom));
				}
			}
			list.Add(GitInstanceItem.CreateSeparator());
			list.Add(GitInstanceItem.CreateAddCustomGitAiInstance());
			GitAiInstanceComboBox.ItemsSource = list.ToArray();
			// 选中当前生效的路径（原始解析结果——staging 链接路径对用户无意义）；未找到时不选中任何项
			// （不 fallback 到 AddCustom，避免在构造期间弹出文件对话框）
			string current = App.GitAiResolvedPath;
			GitInstanceItem match = list.FirstOrDefault((GitInstanceItem x) => x.GitInstanceType != GitInstanceType.Separator && x.GitInstanceType != GitInstanceType.AddCustom && string.Equals(x.GitPath, current, StringComparison.OrdinalIgnoreCase));
			GitAiInstanceComboBox.SelectedItem = match;
			// 版本探测仍在跑（占位标签）→ 完成后异步刷新为真实标签
			if (pending.Count > 0)
			{
				RefreshWhenProbesCompleteAsync("gitai", pending, RefreshGitAiInstanceComboBox);
			}
		}
		finally
		{
			_isRefreshingGitAi = false;
		}
	}

	private void GitAiInstanceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		// 刷新期间程序化设置 SelectedItem 会触发 SelectionChanged，跳过避免副作用（弹文件对话框/写磁盘）
		if (_isRefreshingGitAi)
		{
			return;
		}
		GitInstanceItem previous = (e.RemovedItems.Count > 0) ? (e.RemovedItems[0] as GitInstanceItem) : null;
		if (!(GitAiInstanceComboBox.SelectedItem is GitInstanceItem item))
		{
			return;
		}
		switch (item.GitInstanceType)
		{
		case GitInstanceType.System:
		case GitInstanceType.Local:
		case GitInstanceType.Custom:
			ForkPlusSettings.Default.GitAiInstancePath = item.GitPath;
			ForkPlusSettings.Default.Save();
			break;
		case GitInstanceType.AddCustom:
		{
			// Bug 修复（2026-09-07）：同 GitInstanceComboBox_SelectionChanged——弹对话框前显式收起下拉。
			GitAiInstanceComboBox.IsDropDownOpen = false;
			string initialDirectory = SystemEnvironment.UserProfileDirectory;
			if (OpenDialog.SelectExecutableFile(_parentWindow, PreferencesLocalization.Current("Select git-ai instance"), initialDirectory, out var filePath))
			{
				string normalized = PathHelper.Normalize(filePath);
				ForkPlusSettings.Default.GitAiInstancePath = normalized;
				ForkPlusSettings.Default.Save();
				RefreshGitAiInstanceComboBox();
			}
			else
			{
				GitAiInstanceComboBox.SelectedItem = previous;
			}
			break;
		}
		}
		Log.Info("git-ai Location: " + (App.GitAiPath ?? "(none)"));
	}

	/// <summary>AI 归属总开关（Blame 徽标 / 统计）：git-ai 未安装时关闭亦无害（功能本就降级隐藏）。</summary>
	private void AiAttributionCheckBox_Checked(object sender, RoutedEventArgs e)
	{
		ForkPlusSettings.Default.AiAttributionEnabled = AiAttributionCheckBox.IsChecked.GetValueOrDefault();
		ForkPlusSettings.Default.Save();
	}

	/// <summary>checkpoint 上报开关：仅控制 ForkPlus 内置 AI 修改的上报，独立于归属总开关。</summary>
	private void AiCheckpointReportingCheckBox_Checked(object sender, RoutedEventArgs e)
	{
		ForkPlusSettings.Default.AiCheckpointReportingEnabled = AiCheckpointReportingCheckBox.IsChecked.GetValueOrDefault();
		ForkPlusSettings.Default.Save();
	}

	}
}
