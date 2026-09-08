using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Microsoft.Win32;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Dialogs
{
	public partial class ConfigureGitInstanceWindow : ForkPlusDialogWindow
	{
		public sealed class GitCandidate
		{
			public string Source { get; }

			public string Version { get; }

			public string Path { get; }

			public string Title => Source + " - " + Version;

			public GitCandidate(string source, string version, string path)
			{
				Source = source;
				Version = version;
				Path = path;
			}
		}

		protected override bool IsSubmitAllowed => IsGitPathValid(GitPathTextBox.Text.Trim()) && base.IsSubmitAllowed;

		public ConfigureGitInstanceWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("Configure Git");
			base.DialogDescription = Translate("ForkPlus requires a valid Git executable to continue.");
			base.SubmitButtonTitle = Translate("Continue");
			base.CancelButtonTitle = Translate("Exit");
			base.ShowWarningIcon = true;
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			List<GitCandidate> candidates = GetGitCandidates();
			GitCandidatesListBox.ItemsSource = candidates;
			GitPathTextBox.Text = candidates.FirstOrDefault()?.Path ?? ExistingGitPath();
			base.Loaded += delegate
			{
				SelectCurrentCandidate();
				GitPathTextBox.Focus();
				UpdateSubmitButton();
			};
		}

		protected override void OnSubmit()
		{
			string gitPath = PathHelper.Normalize(GitPathTextBox.Text.Trim());
			if (!ValidateGitPath(gitPath, showError: true))
			{
				UpdateSubmitButton();
				return;
			}
			ForkPlusSettings.Default.GitInstancePath = gitPath;
			ForkPlusSettings.Default.Save();
			CloseWithOk();
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string initialDirectory = Directory.Exists(Path.GetDirectoryName(GitPathTextBox.Text)) ? Path.GetDirectoryName(GitPathTextBox.Text) : Environment.ExpandEnvironmentVariables("%programfiles%");
				OpenFileDialog dialog = new OpenFileDialog
				{
					Title = Translate("Select git instance"),
					InitialDirectory = initialDirectory,
					// Migration note：跨平台过滤器。原 "(*.exe)|*.exe" 在 Unix 上选不到无扩展名的 git。
					Filter = Translate("Applications") + (OperatingSystem.IsWindows()
						? " (*.exe)|*.exe"
						: " (git;git.exe)|git;git.exe"),
					CheckFileExists = true,
					Multiselect = false
				};
				bool? result = dialog.ShowDialog(); // Migration note：shim 的 ShowDialog() 无参，自动取活动窗口作 owner。
				if (result.GetValueOrDefault())
				{
					GitPathTextBox.Text = dialog.FileName;
					GitPathTextBox.Focus();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to show git instance picker", ex);
				new ErrorWindow(Translate("Unable to open file picker. Please type the git.exe path manually.")).ShowDialog();
			}
		}

		private void GitPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			SelectCurrentCandidate();
			UpdateSubmitButton();
		}

		private void GitCandidatesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (GitCandidatesListBox.SelectedItem is GitCandidate candidate && !string.Equals(GitPathTextBox.Text, candidate.Path, StringComparison.OrdinalIgnoreCase))
			{
				GitPathTextBox.Text = candidate.Path;
				GitPathTextBox.Focus();
			}
		}

		private void SelectCurrentCandidate()
		{
			if (GitCandidatesListBox == null)
			{
				return;
			}
			string currentPath = GitPathTextBox.Text?.Trim();
			GitCandidate candidate = GitCandidatesListBox.Items.OfType<GitCandidate>().FirstOrDefault((GitCandidate item) => IsSamePath(item.Path, currentPath));
			if (!Equals(GitCandidatesListBox.SelectedItem, candidate))
			{
				GitCandidatesListBox.SelectedItem = candidate;
			}
		}

		private static string ExistingGitPath()
		{
			if (!string.IsNullOrWhiteSpace(ForkPlusSettings.Default.GitInstancePath))
			{
				return ForkPlusSettings.Default.GitInstancePath;
			}
			// Migration note：Unix 上直接探测常见安装位置（原 %programfiles% 展开在 Unix 永远失败）。
			if (!OperatingSystem.IsWindows())
			{
				foreach (string path in SystemEnvironment.GetUnixCommonGitPaths())
				{
					if (File.Exists(path))
					{
						return path;
					}
				}
				return "";
			}
			string programFiles = Environment.ExpandEnvironmentVariables("%programfiles%\\Git\\bin\\git.exe");
			if (File.Exists(programFiles))
			{
				return programFiles;
			}
			string programFilesX86 = Environment.ExpandEnvironmentVariables("%programfiles(x86)%\\Git\\bin\\git.exe");
			return File.Exists(programFilesX86) ? programFilesX86 : "";
		}

		private static List<GitCandidate> GetGitCandidates()
		{
			List<GitCandidate> result = new List<GitCandidate>();
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			AddCandidate(result, seen, Translate("Saved Git"), ForkPlusSettings.Default.GitInstancePath);
			AddCandidate(result, seen, Translate("Environment Git"), App.EnvironmentGitInstancePath);
			AddCandidate(result, seen, Translate("ForkPlus Git"), App.ForkGitInstancePath);
			foreach (string path in GetPathGitCandidates())
			{
				AddCandidate(result, seen, Translate("System PATH"), path);
			}
			foreach (string path in GetCommonGitCandidates())
			{
				AddCandidate(result, seen, Translate("Common location"), path);
			}
			foreach (string path in GetPortableGitCandidates())
			{
				AddCandidate(result, seen, Translate("PortableGit"), path);
			}
			return result;
		}

		private static IEnumerable<string> GetPathGitCandidates()
		{
			string pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
			foreach (string directory in pathVariable.Split(Path.PathSeparator))
			{
				if (string.IsNullOrWhiteSpace(directory))
				{
					continue;
				}
				// Migration note：git 二进制名跨平台（Unix 无 .exe 扩展名）。
				string gitPath = Path.Combine(Environment.ExpandEnvironmentVariables(directory.Trim()), SystemEnvironment.GitExecutableName);
				if (File.Exists(gitPath))
				{
					yield return gitPath;
				}
			}
		}

		private static IEnumerable<string> GetCommonGitCandidates()
		{
			// Migration note：Unix 常见安装位置（/usr/bin、/opt/homebrew 等）。
			if (!OperatingSystem.IsWindows())
			{
				foreach (string path in SystemEnvironment.GetUnixCommonGitPaths())
				{
					if (File.Exists(path))
					{
						yield return path;
					}
				}
				yield break;
			}
			string[] paths =
			{
				"%programfiles%\\Git\\bin\\git.exe",
				"%programfiles%\\Git\\cmd\\git.exe",
				"%programfiles(x86)%\\Git\\bin\\git.exe",
				"%programfiles(x86)%\\Git\\cmd\\git.exe",
				"%localappdata%\\Programs\\Git\\bin\\git.exe",
				"%localappdata%\\Programs\\Git\\cmd\\git.exe"
			};
			foreach (string path in paths)
			{
				string expanded = Environment.ExpandEnvironmentVariables(path);
				if (File.Exists(expanded))
				{
					yield return expanded;
				}
			}
		}

		private static IEnumerable<string> GetPortableGitCandidates()
		{
			// Migration note：PortableGit 是 Windows 特有的便携版布局，Unix 直接跳过。
			if (!OperatingSystem.IsWindows())
			{
				yield break;
			}
			string[] roots =
			{
				Environment.GetEnvironmentVariable("SystemDrive") + "\\",
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
				"C:\\develop"
			};
			foreach (string root in roots.Where((string path) => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase))
			{
				foreach (string directory in SafeGetDirectories(root, "PortableGit*"))
				{
					string gitPath = Path.Combine(directory, "bin", "git.exe");
					if (File.Exists(gitPath))
					{
						yield return gitPath;
					}
				}
			}
		}

		private static IEnumerable<string> SafeGetDirectories(string path, string searchPattern)
		{
			try
			{
				return Directory.GetDirectories(path, searchPattern, SearchOption.TopDirectoryOnly);
			}
			catch
			{
				return new string[0];
			}
		}

		private static void AddCandidate(List<GitCandidate> result, HashSet<string> seen, string source, string gitPath)
		{
			// Migration note：文件名校验跨平台（原 git.exe 硬编码在 Unix 永远 false）。
			if (string.IsNullOrWhiteSpace(gitPath) || !File.Exists(gitPath) || !SystemEnvironment.IsGitExecutable(gitPath))
			{
				return;
			}
			string normalizedPath = PathHelper.Normalize(gitPath);
			if (!seen.Add(normalizedPath))
			{
				return;
			}
			GitCommandResult<string> versionResult = new GetGitVersionGitCommand().Execute(normalizedPath);
			if (versionResult.Succeeded)
			{
				result.Add(new GitCandidate(source, versionResult.Result, normalizedPath));
			}
		}

		private static bool IsGitPathValid(string gitPath)
		{
			return ValidateGitPath(gitPath, showError: false);
		}

		private static bool ValidateGitPath(string gitPath, bool showError)
		{
			// Migration note：文件名校验跨平台（原 git.exe 硬编码在 Unix 永远 false，"继续"按钮永禁用）。
			if (string.IsNullOrWhiteSpace(gitPath) || !File.Exists(gitPath) || !SystemEnvironment.IsGitExecutable(gitPath))
			{
				if (showError)
				{
					new ErrorWindow(Translate("Please select a valid git.exe file.")).ShowDialog();
				}
				return false;
			}
			if (!new GetGitVersionGitCommand().Execute(gitPath).Succeeded)
			{
				if (showError)
				{
					new ErrorWindow(Translate("Unable to run selected Git executable.")).ShowDialog();
				}
				return false;
			}
			return true;
		}

		private static bool IsSamePath(string left, string right)
		{
			if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
			{
				return false;
			}
			return string.Equals(PathHelper.Normalize(left), PathHelper.Normalize(right), StringComparison.OrdinalIgnoreCase);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
