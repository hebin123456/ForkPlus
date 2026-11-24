using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.Services;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class InitGitMmRepositoryWindow : ForkPlusDialogWindow
	{
		// 清单仓库在临时目录里的克隆副本，用于读取分支与对应分支下的 xml 清单文件。
		private string _manifestClonePath;
		// 避免分支/清单文件变化时重复加载同一份 group 列表。
		private string _lastGroupLoadKey;

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (string.IsNullOrWhiteSpace(ManifestUrlTextBox.Text.Trim()))
				{
					return false;
				}
				if (string.IsNullOrWhiteSpace(ParentDirectoryTextBox.Text.Trim()))
				{
					return false;
				}
				if (CreateSubdirectoryCheckBox.IsChecked == true && string.IsNullOrWhiteSpace(RepositoryNameTextBox.Text.Trim()))
				{
					return false;
				}
				return base.IsSubmitAllowed;
			}
		}

		public InitGitMmRepositoryWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("Initialize git mm Repository");
			base.DialogDescription = Translate("Initialize a git mm workspace from a manifest repository");
			base.SubmitButtonTitle = Translate("Initialize");
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			ParentDirectoryTextBox.Text = RepositoryManager.Instance.DefaultSourceDir();
			LoadManifestButton.Content = Translate("Load");
			CreateSubdirectoryCheckBox.IsChecked = false;
			RepositoryNameTextBox.IsEnabled = false;
			ManifestBranchComboBox.SelectionChanged += ManifestBranchComboBox_SelectionChanged;
			ManifestBranchComboBox.PropertyChanged += ManifestComboBox_PropertyChanged;
			ManifestFileComboBox.PropertyChanged += ManifestComboBox_PropertyChanged;
			ManifestGroupComboBox.PropertyChanged += ManifestComboBox_PropertyChanged;
			RestoreDefaults();
			UpdateSubmitButton();
			RefreshCommandPreview();
			base.Loaded += delegate
			{
				Dispatcher.Post(new Action(RefreshCommandPreview), global::Avalonia.Threading.DispatcherPriority.Loaded);
				ManifestUrlTextBox.Focus();
			};
		}

		protected override async void OnSubmit()
		{
			try
			{
				string url = ManifestUrlTextBox.Text.Trim();
				string parentDirectory = ParentDirectoryTextBox.Text.Trim();
				bool createSubdirectory = CreateSubdirectoryCheckBox.IsChecked == true;
				string destinationDirectory = parentDirectory;
				if (createSubdirectory)
				{
					string repositoryName = RepositoryNameTextBox.Text.Trim();
					destinationDirectory = System.IO.Path.Combine(parentDirectory, repositoryName);
				}
				else if (!Directory.Exists(parentDirectory))
				{
					Directory.CreateDirectory(parentDirectory);
				}
				string manifest = GetComboBoxText(ManifestFileComboBox);
				if (string.IsNullOrWhiteSpace(manifest))
				{
					manifest = "dependency.xml";
				}
				string branch = GetComboBoxText(ManifestBranchComboBox);
				if (string.IsNullOrWhiteSpace(branch))
				{
					branch = "master";
				}
				string group = GetComboBoxText(ManifestGroupComboBox);
				if (string.IsNullOrWhiteSpace(group))
				{
					group = "default";
				}
				if (createSubdirectory && !ValidateDestination(destinationDirectory))
				{
					return;
				}
				SaveDefaults(url, manifest, branch, group);
				DisableEditableControls();
				SetStatus(ForkPlusDialogStatus.InProgress, Translate("Initializing git mm repository..."));
				GitRequestResult result = await Task.Run(delegate
				{
					return RunInit(destinationDirectory, url, manifest, branch, group);
				});
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				EnableEditableControls();
				if (!result.Success)
				{
					new MessageBoxWindow("git mm init failed", result.FullReadableOutput(), "Close", "Cancel", showCancelButton: false).ShowDialog();
					return;
				}
				Application.Current.TabManager().OpenRepository(destinationDirectory);
				Close();
			}
			catch (Exception ex)
			{
				Log.Error("OnSubmit failed", ex);
			}
		}

		// 点击 URL 输入框后的加载按钮：把清单仓库克隆到系统临时目录，
		// 再用克隆结果填充「清单分支」下拉框。
		private async void LoadManifestButton_Click(object sender, RoutedEventArgs e)
		{
			string url = ManifestUrlTextBox.Text?.Trim();
			if (string.IsNullOrWhiteSpace(url))
			{
				new MessageBoxWindow(Translate("Missing repository URL"), Translate("Please enter the manifest repository URL first."), "Close", "Cancel", showCancelButton: false).ShowDialog();
				return;
			}

			if (!string.IsNullOrWhiteSpace(_manifestClonePath) && Directory.Exists(_manifestClonePath))
			{
				try
				{
					Directory.Delete(_manifestClonePath, true);
				}
				catch (Exception ex)
				{
					Log.Error("Cleanup previous manifest clone failed", ex);
				}
			}

			string clonePath = Path.Combine(Path.GetTempPath(), "ForkPlusGitMm_" + Guid.NewGuid().ToString("N"));
			LoadManifestButton.IsEnabled = false;
			SetStatus(ForkPlusDialogStatus.InProgress, Translate("Loading manifest repository..."));
			try
			{
				GitRequestResult result = await Task.Run(delegate
				{
					return default(GitRequest)
						.Command(new GitCommand("clone", url, clonePath))
						.Env(new (string, string)[1] { ("GIT_TERMINAL_PROMPT", "0") })
						.Execute(new JobMonitor(), silent: true);
				});
				if (!result.Success)
				{
					new MessageBoxWindow(Translate("Failed to load manifest repository"), result.FullReadableOutput(), "Close", "Cancel", showCancelButton: false).ShowDialog();
					return;
				}

				_manifestClonePath = clonePath;
				_lastGroupLoadKey = null;
				List<string> branches = await Task.Run(delegate
				{
					return ReadBranches(clonePath);
				});
				ManifestBranchComboBox.ItemsSource = branches;
				string preferred = branches.FirstOrDefault(x => x.Equals("master", StringComparison.OrdinalIgnoreCase))
					?? branches.FirstOrDefault();
				if (preferred != null)
				{
					ManifestBranchComboBox.SelectedItem = preferred;
				}
				else
				{
					ManifestBranchComboBox.Text = string.Empty;
					ManifestFileComboBox.ItemsSource = null;
					ManifestFileComboBox.Text = string.Empty;
				}
			}
			catch (Exception ex)
			{
				Log.Error("Load manifest repository failed", ex);
				new MessageBoxWindow(Translate("Failed to load manifest repository"), ex.Message, "Close", "Cancel", showCancelButton: false).ShowDialog();
			}
			finally
			{
				SetStatus(ForkPlusDialogStatus.None, string.Empty);
				LoadManifestButton.IsEnabled = true;
			}
		}

		private static List<string> ReadBranches(string repoPath)
		{
			List<string> branches = new List<string>();
			GitRequestResult result = default(GitRequest)
				.CurrentDir(repoPath)
				.Command(new GitCommand("for-each-ref", "--format=%(refname:short)", "refs/remotes/origin"))
				.Execute(new JobMonitor(), silent: true);
			foreach (string line in SplitLines(result.Stdout))
			{
				string branch = line.Trim();
				if (branch.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
				{
					branch = branch.Substring("origin/".Length);
				}
				if (!string.IsNullOrWhiteSpace(branch) && !branch.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
				{
					branches.Add(branch);
				}
			}

			if (branches.Count == 0)
			{
				result = default(GitRequest)
					.CurrentDir(repoPath)
					.Command(new GitCommand("for-each-ref", "--format=%(refname:short)", "refs/heads"))
					.Execute(new JobMonitor(), silent: true);
				foreach (string line in SplitLines(result.Stdout))
				{
					string branch = line.Trim();
					if (!string.IsNullOrWhiteSpace(branch))
					{
						branches.Add(branch);
					}
				}
			}

			return branches.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		// 选中清单分支后，从该分支的 git 树里读取所有 xml 文件填充「清单文件」下拉框。
		private async void ManifestBranchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (string.IsNullOrWhiteSpace(_manifestClonePath))
			{
				return;
			}
			string branch = GetComboBoxText(ManifestBranchComboBox);
			if (string.IsNullOrWhiteSpace(branch))
			{
				return;
			}
			List<string> files = await Task.Run(delegate
			{
				return ReadXmlFiles(_manifestClonePath, ResolveRef(branch));
			});
			ManifestFileComboBox.ItemsSource = files;
			string preferred = files.FirstOrDefault(x => x.Equals("dependency.xml", StringComparison.OrdinalIgnoreCase))
				?? files.FirstOrDefault();
			if (preferred != null)
			{
				ManifestFileComboBox.SelectedItem = preferred;
			}
			else
			{
				ManifestFileComboBox.Text = string.Empty;
			}
		}

		private string ResolveRef(string branch)
		{
			string name = branch?.Trim();
			if (string.IsNullOrWhiteSpace(name))
			{
				return name;
			}
			if (name.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
			{
				name = name.Substring("origin/".Length);
			}
			if (string.IsNullOrWhiteSpace(_manifestClonePath))
			{
				return name;
			}

			foreach (string fullRef in new string[2] { "refs/remotes/origin/" + name, "refs/heads/" + name })
			{
				GitRequestResult check = default(GitRequest)
					.CurrentDir(_manifestClonePath)
					.Command(new GitCommand("rev-parse", "--verify", "--quiet", fullRef))
					.Execute(new JobMonitor(), silent: true);
				if (check.Success)
				{
					return fullRef;
				}
			}
			return name;
		}

		private static List<string> ReadXmlFiles(string repoPath, string treeish)
		{
			List<string> files = new List<string>();
			if (string.IsNullOrWhiteSpace(treeish))
			{
				return files;
			}
			GitRequestResult result = default(GitRequest)
				.CurrentDir(repoPath)
				.Command(new GitCommand("ls-tree", "-r", "--name-only", treeish))
				.Execute(new JobMonitor(), silent: true);
			foreach (string line in SplitLines(result.Stdout))
			{
				string path = line.Trim();
				if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
				{
					files.Add(path);
				}
			}
			return files.Distinct(StringComparer.Ordinal)
				.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		// 选中清单文件后，从 xml 中的 groups="a,b,c" 节点提取分组，去重、按字母序排列，
		// default 固定排最前（没有也补一个），填充「Manifest Group」下拉框。
		private async void LoadGroupsForSelectedManifest()
		{
			if (string.IsNullOrWhiteSpace(_manifestClonePath))
			{
				return;
			}
			string branch = GetComboBoxText(ManifestBranchComboBox);
			string manifest = GetComboBoxText(ManifestFileComboBox);
			if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(manifest))
			{
				return;
			}
			string key = branch + "|" + manifest;
			if (string.Equals(key, _lastGroupLoadKey, StringComparison.Ordinal))
			{
				return;
			}
			_lastGroupLoadKey = key;

			List<string> groups = await Task.Run(delegate
			{
				return ReadGroups(_manifestClonePath, ResolveRef(branch), manifest);
			});
			ManifestGroupComboBox.ItemsSource = groups;
			string current = GetComboBoxText(ManifestGroupComboBox);
			if (string.IsNullOrWhiteSpace(current) || !groups.Any(x => x.Equals(current, StringComparison.OrdinalIgnoreCase)))
			{
				string preferred = groups.FirstOrDefault(x => x.Equals("default", StringComparison.OrdinalIgnoreCase))
					?? groups.FirstOrDefault();
				if (preferred != null)
				{
					ManifestGroupComboBox.Text = preferred;
				}
			}
		}

		private static List<string> ReadGroups(string repoPath, string treeish, string filePath)
		{
			List<string> groups = new List<string>();
			if (string.IsNullOrWhiteSpace(treeish) || string.IsNullOrWhiteSpace(filePath))
			{
				return groups;
			}
			GitRequestResult result = default(GitRequest)
				.CurrentDir(repoPath)
				.Command(new GitCommand("show", treeish + ":" + filePath))
				.Execute(new JobMonitor(), silent: true);
			if (!result.Success)
			{
				return groups;
			}

			HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (Match match in Regex.Matches(result.Stdout, "groups\\s*=\\s*([\"'])(.*?)\\1", RegexOptions.IgnoreCase | RegexOptions.Singleline))
			{
				foreach (string item in match.Groups[2].Value.Split(','))
				{
					string group = item.Trim();
					if (!string.IsNullOrWhiteSpace(group))
					{
						unique.Add(group);
					}
				}
			}
			groups.AddRange(unique);
			groups.Sort(delegate(string a, string b)
			{
				bool aDefault = a.Equals("default", StringComparison.OrdinalIgnoreCase);
				bool bDefault = b.Equals("default", StringComparison.OrdinalIgnoreCase);
				if (aDefault && !bDefault)
				{
					return -1;
				}
				if (!aDefault && bDefault)
				{
					return 1;
				}
				return StringComparer.OrdinalIgnoreCase.Compare(a, b);
			});
			if (!groups.Any(x => x.Equals("default", StringComparison.OrdinalIgnoreCase)))
			{
				groups.Insert(0, "default");
			}
			return groups;
		}

		private void ManifestComboBox_PropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
		{
			if (e.Property == ComboBox.TextProperty)
			{
				UpdateSubmitButton();
				RefreshCommandPreview();
				if (sender == ManifestFileComboBox)
				{
					LoadGroupsForSelectedManifest();
				}
			}
		}

		private static string[] SplitLines(string text)
		{
			return string.IsNullOrEmpty(text)
				? Array.Empty<string>()
				: text.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
		}

		private static string GetComboBoxText(ComboBox comboBox)
		{
			string text = comboBox?.Text?.Trim();
			if (!string.IsNullOrWhiteSpace(text))
			{
				return text;
			}
			return (comboBox?.SelectedItem as string)?.Trim() ?? string.Empty;
		}

		private bool ValidateDestination(string destinationDirectory)
		{
			try
			{
				if (Directory.Exists(destinationDirectory))
				{
					if (ForkPlus.UI.UserControls.GitMmUserControl.IsGitMmWorkspace(destinationDirectory))
					{
						new MessageBoxWindow("Already a git mm repository", string.Format(Translate("'{0}' is already a git mm repository."), destinationDirectory), "Close", "Cancel", showCancelButton: false).ShowDialog();
						return false;
					}
					if (Directory.GetFileSystemEntries(destinationDirectory).Length > 0)
					{
						new MessageBoxWindow("Destination folder is not empty", string.Format(Translate("'{0}' already exists. Please choose another name."), destinationDirectory), "Close", "Cancel", showCancelButton: false).ShowDialog();
						return false;
					}
				}
				else
				{
					Directory.CreateDirectory(destinationDirectory);
				}
				return true;
			}
			catch (Exception ex)
			{
				new MessageBoxWindow("Failed to create destination folder", ex.Message, "Close", "Cancel", showCancelButton: false).ShowDialog();
				return false;
			}
		}

		private GitRequestResult RunInit(string destinationDirectory, string url, string manifest, string branch, string group)
		{
			GitCommand command = new GitCommand("mm");
			command.AddRange(new string[9] { "init", "-u", url, "-m", manifest, "-b", branch, "-g", group });
			return default(GitRequest)
				.CurrentDir(destinationDirectory)
				.Command(command)
				.Env(new (string, string)[1] { ("GIT_TERMINAL_PROMPT", "0") })
				.Execute(new JobMonitor());
		}

		// 勾选「Create subdirectory」后 Name 输入框才可用；不勾选时直接在父目录执行 git mm init。
		private void CreateSubdirectoryCheckBox_IsCheckedChanged(object sender, RoutedEventArgs e)
		{
			RepositoryNameTextBox.IsEnabled = CreateSubdirectoryCheckBox.IsChecked == true;
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
			if (OpenDialog.SelectDirectory(this, Translate("Select location"), initialDirectory, out var directoryPath))
			{
				ParentDirectoryTextBox.Text = directoryPath;
				ParentDirectoryTextBox.Focus();
			}
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		// Bug 修复（2026-09-07，"初始化 git mm 仓库弹窗命令预览右侧没有复制按钮"）：git mm 四弹窗
		//（Init/Start/Sync/Upload）的命令预览是 XAML 内联实现，不走
		// ForkPlusDialogWindow.AddCommandPreview 基类路径——基类那条路自带复制按钮，内联版漏掉了。
		// 点击复制预览命令到剪贴板（按钮本体/图标见 axaml，样式与基类实现一致）。
		private void CommandPreviewCopyButton_Click(object sender, RoutedEventArgs e)
		{
			if (!string.IsNullOrWhiteSpace(CommandPreviewTextBlock.Text))
			{
				ServiceLocator.Clipboard.SetText(CommandPreviewTextBlock.Text);
			}
		}

		private void RestoreDefaults()
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			ManifestUrlTextBox.Text = settings.InitUrl ?? "";
			ManifestFileComboBox.Text = settings.InitManifest;
			ManifestBranchComboBox.Text = settings.InitBranch;
			ManifestGroupComboBox.Text = settings.InitGroup;
			RefreshCommandPreview();
		}

		private void RefreshCommandPreview()
		{
			if (CommandPreviewTextBlock == null)
			{
				return;
			}
			string cmd = GitMmCommandPreviewHelper.Format(CreateInitArgs());
			CommandPreviewTextBlock.Text = cmd;
			// 鼠标悬停显示完整命令文本（预览区可能因 MaxHeight 截断）
			global::Avalonia.Controls.ToolTip.SetTip(CommandPreviewTextBlock,cmd);
		}

		private string[] CreateInitArgs()
		{
			string url = ManifestUrlTextBox.Text.Trim();
			string manifest = GetComboBoxText(ManifestFileComboBox);
			if (string.IsNullOrWhiteSpace(manifest))
			{
				manifest = "dependency.xml";
			}
			string branch = GetComboBoxText(ManifestBranchComboBox);
			if (string.IsNullOrWhiteSpace(branch))
			{
				branch = "master";
			}
			string group = GetComboBoxText(ManifestGroupComboBox);
			if (string.IsNullOrWhiteSpace(group))
			{
				group = "default";
			}
			return new string[9] { "init", "-u", url, "-m", manifest, "-b", branch, "-g", group };
		}

		private void SaveDefaults(string url, string manifest, string branch, string group)
		{
			ForkPlusSettings.GitMmSettings settings = ForkPlusSettings.Default.GitMm;
			ForkPlusSettings.Default.GitMm = new ForkPlusSettings.GitMmSettings(
				settings.Workspaces ?? new string[0],
				settings.ActiveWorkspace,
				settings.ActiveSubrepo,
				settings.ActiveSubrepos,
				settings.SubrepoOrders,
				settings.VisibleSubrepos,
				settings.CommandOutputCollapsed,
				settings.CommandOutputHeight,
				settings.CommandHistory,
				settings.UploadLinks,
				settings.UploadLinksByWorkspace,
				settings.SyncJobs,
				settings.StartBranch,
				url,
				manifest,
				branch,
				group,
				settings.DialogOptions);
			ForkPlusSettings.Default.Save();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
