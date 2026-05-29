using ForkPlus;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ForkPlus.UI.Dialogs
{
	public partial class AiDevelopmentWindow : CustomWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		private Job _activeJob;

		private readonly List<AiFileChange> _fileChanges = new List<AiFileChange>();

		private readonly DispatcherTimer _statusTimer;

		private List<AiSkillEntry> _skillEntries;

		// Queue for pending requests when one is in progress
		private readonly Queue<string> _pendingRequests = new Queue<string>();

		private bool _isProcessing;

		public AiDevelopmentWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			base.Title = PreferencesLocalization.Current("AI-Assisted Development");
			InputTextBox.TextChanged += InputTextBox_TextChanged;
			InputTextBox.PreviewKeyDown += InputTextBox_PreviewKeyDown;
			Loaded += AiDevelopmentWindow_Loaded;
			_statusTimer = new DispatcherTimer
			{
				Interval = TimeSpan.FromMilliseconds(500)
			};
			_statusTimer.Tick += StatusTimer_Tick;
			_skillEntries = new List<AiSkillEntry>();
			LoadSkillList();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode(this))
			{
				return;
			}
			if (Application.Current?.MainWindow?.WindowState == WindowState.Maximized)
			{
				WindowState = WindowState.Maximized;
			}
		}

		private void AiDevelopmentWindow_Loaded(object sender, RoutedEventArgs e)
		{
			ApplySendMode();
			InputTextBox.Focus();
		}

		private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSendButton();
		}

		private void InputTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			bool sendOnEnter = ForkPlusSettings.Default.AiDevSendMode == "Enter";
			bool enterPressed = e.Key == System.Windows.Input.Key.Enter;
			bool shiftPressed = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftShift) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightShift);
			bool ctrlPressed = System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.LeftCtrl) || System.Windows.Input.Keyboard.IsKeyDown(System.Windows.Input.Key.RightCtrl);

			if (sendOnEnter)
			{
				// Enter 发送，Shift+Enter 换行
				if (enterPressed && !shiftPressed && !ctrlPressed)
				{
					e.Handled = true;
					SendRequest();
				}
			}
			else
			{
				// Ctrl+Enter 发送，Enter 换行
				if (enterPressed && ctrlPressed)
				{
					e.Handled = true;
					SendRequest();
				}
			}
		}

		private void UpdateSendButton()
		{
			SendButton.IsEnabled = !string.IsNullOrWhiteSpace(InputTextBox.Text);
		}

		private void SendButton_Click(object sender, RoutedEventArgs e)
		{
			SendRequest();
		}

		private void SendModeMenuItem_Click(object sender, RoutedEventArgs e)
		{
			MenuItem item = sender as MenuItem;
			if (item == null) return;

			bool isEnterMode = item == SendModeEnter;
			ForkPlusSettings.Default.AiDevSendMode = isEnterMode ? "Enter" : "CtrlEnter";
			ForkPlusSettings.Default.Save();
			ApplySendMode();
		}

		private void ApplySendMode()
		{
			bool isEnter = ForkPlusSettings.Default.AiDevSendMode == "Enter";
			SendModeEnter.IsChecked = isEnter;
			SendModeCtrlEnter.IsChecked = !isEnter;
			SendButton.Content = PreferencesLocalization.Current(isEnter ? "发送 (Enter)" : "发送 (Ctrl+Enter)");
		}

		private void StatusTimer_Tick(object sender, EventArgs e)
		{
			if (_activeJob != null)
			{
				if (_activeJob.Status == JobStatus.Running)
				{
					UpdateProcessingStatus(PreferencesLocalization.Current("AI 正在生成代码..."));
					_statusTimer.Stop();
				}
				else if (_activeJob.Status == JobStatus.Finished || _activeJob.Monitor.IsCanceled)
				{
					_statusTimer.Stop();
				}
			}
		}

		private void UpdateProcessingStatus(string message)
		{
			// Show status by updating progress bar and status text
			if (!string.IsNullOrEmpty(message))
			{
				AddStatusMessage(message, Brushes.Gray);
			}
		}

		private void SendRequest()
		{
			string requirement = InputTextBox.Text.Trim();
			if (string.IsNullOrWhiteSpace(requirement))
			{
				return;
			}

			// Add user's requirement as a message
			AddUserMessage(requirement);
			InputTextBox.Text = "";
			UpdateSendButton();

			if (_isProcessing)
			{
				// Queue the request if one is already in progress
				_pendingRequests.Enqueue(requirement);
				AddStatusMessage(
					PreferencesLocalization.FormatCurrent("⏳ 已加入队列 (队列中有 {0} 个待处理请求)", _pendingRequests.Count),
					Brushes.Gray);
				return;
			}

			ProcessRequest(requirement);
		}

		private void ProcessRequest(string requirement)
		{
			_isProcessing = true;
			InputTextBox.IsEnabled = false;
			SendButton.IsEnabled = false;
			ProgressBar.Visibility = Visibility.Visible;
			AddStatusMessage(PreferencesLocalization.Current("排队中..."), Brushes.Gray);

			// Start timer to track job status (Pending → Running)
			_statusTimer.Start();

			// Save current file state for diff later
			Dictionary<string, string> beforeContents = GetCurrentFileContents();

			_activeJob = _repositoryUserControl.JobQueue.Add(
				PreferencesLocalization.Current("AI 开发"),
				delegate (JobMonitor monitor)
				{
					try
					{
						OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
						string prompt = BuildDevelopmentPrompt(requirement);
						base.Dispatcher.Async(delegate
						{
							AddStatusMessage(PreferencesLocalization.FormatCurrent("正在请求 AI ({0})...", ForkPlusSettings.Default.AiReviewSelectedModel), Brushes.Gray);
						});

						ServiceResult<OpenAiResponse> result = aiService.OpenAiRequest(prompt);
						if (monitor.IsCanceled)
						{
							FinishRequest();
							return;
						}

						if (!result.Succeeded)
						{
							base.Dispatcher.Async(delegate
							{
								AddStatusMessage(PreferencesLocalization.FormatCurrent("AI 请求失败: {0}", result.Error.FriendlyMessage), Brushes.Red);
								FinishRequest();
							});
							return;
						}

						string aiResponse = result.Result.Message;
						monitor.AppendOutputLine(aiResponse);

						// Parse AI response for file changes
						ParsedAiChanges parsedChanges = ParseAiResponse(aiResponse);

						base.Dispatcher.Async(delegate
						{
							try
							{
								// Apply file changes
								List<AiFileChange> appliedChanges = ApplyFileChanges(parsedChanges, beforeContents);

								if (appliedChanges.Count > 0)
								{
									_fileChanges.Clear();
									_fileChanges.AddRange(appliedChanges);
									ShowDiffResults(appliedChanges);
									AddStatusMessage(
										PreferencesLocalization.FormatCurrent("✅ AI 修改了 {0} 个文件", appliedChanges.Count),
										Brushes.Green);
								}
								else
								{
									// No file changes, show the AI response text
									AddAiResponseMessage(aiResponse);
								}

								// Refresh repository status to clear stale entries
								RefreshRepositoryStatus();
							}
							catch (Exception ex)
							{
								AddStatusMessage(PreferencesLocalization.FormatCurrent("应用变更时出错: {0}", ex.Message), Brushes.Red);
							}
							finally
							{
								FinishRequest();
							}
						});
					}
					catch (Exception ex)
					{
						base.Dispatcher.Async(delegate
						{
							AddStatusMessage(PreferencesLocalization.FormatCurrent("AI 请求出错: {0}", ex.Message), Brushes.Red);
							FinishRequest();
						});
					}
				},
				JobFlags.Hidden
			);
		}

		private void FinishRequest()
		{
			ProgressBar.Visibility = Visibility.Collapsed;
			_statusTimer.Stop();
			_activeJob = null;

			// Process next queued request
			if (_pendingRequests.Count > 0)
			{
				string next = _pendingRequests.Dequeue();
				if (_pendingRequests.Count > 0)
				{
					AddStatusMessage(
						PreferencesLocalization.FormatCurrent("🔄 开始处理下一个请求 (剩余 {0} 个)", _pendingRequests.Count),
						Brushes.Gray);
				}
				ProcessRequest(next);
			}
			else
			{
				_isProcessing = false;
				InputTextBox.IsEnabled = true;
				UpdateSendButton();
				InputTextBox.Focus();
			}
		}

		private void RefreshRepositoryStatus()
		{
			try
			{
				// Force git to re-check file statuses by touching the git index
				// This helps clear stale "modified" entries caused by file writes
				if (_gitModule != null)
				{
					_repositoryUserControl?.InvalidateAndRefresh(SubDomain.Status | SubDomain.ChangedFiles, null, RepositoryViewMode.CommitViewMode);
				}
			}
			catch
			{
				// Ignore refresh errors
			}
		}

		private void AddUserMessage(string message)
		{
			Border userBorder = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(25, 0, 120, 215)),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(10, 6, 10, 6),
				Margin = new Thickness(0, 4, 0, 4),
				MaxWidth = 600,
				HorizontalAlignment = HorizontalAlignment.Right
			};

			TextBlock header = new TextBlock
			{
				Text = PreferencesLocalization.Current("🧑 我的需求"),
				FontSize = 11,
				FontWeight = FontWeights.SemiBold,
				Foreground = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
				Margin = new Thickness(0, 0, 0, 2)
			};

			TextBox content = new TextBox
			{
				Text = message,
				FontSize = 13,
				TextWrapping = TextWrapping.Wrap,
				Foreground = Brushes.Black,
				IsReadOnly = true,
				BorderThickness = new Thickness(0),
				Background = Brushes.Transparent,
				Padding = new Thickness(0),
				IsTabStop = false
			};

			StackPanel innerPanel = new StackPanel();
			innerPanel.Children.Add(header);
			innerPanel.Children.Add(content);
			userBorder.Child = innerPanel;

			MessagePanel.Children.Add(userBorder);
			ScrollToEnd();
		}

		private void AddAiResponseMessage(string response)
		{
			Border aiBorder = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(10, 6, 10, 6),
				Margin = new Thickness(0, 4, 0, 4),
				MaxWidth = 700
			};

			TextBlock header = new TextBlock
			{
				Text = PreferencesLocalization.Current("🤖 AI 响应"),
				FontSize = 11,
				FontWeight = FontWeights.SemiBold,
				Foreground = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
				Margin = new Thickness(0, 0, 0, 4)
			};

			TextBox content = new TextBox
			{
				Text = response,
				FontSize = 13,
				TextWrapping = TextWrapping.Wrap,
				Foreground = Brushes.Black,
				IsReadOnly = true,
				BorderThickness = new Thickness(0),
				Background = Brushes.Transparent,
				Padding = new Thickness(0),
				IsTabStop = false
			};

			StackPanel innerPanel = new StackPanel();
			innerPanel.Children.Add(header);
			innerPanel.Children.Add(content);
			aiBorder.Child = innerPanel;

			MessagePanel.Children.Add(aiBorder);
			ScrollToEnd();
		}

		private void AddStatusMessage(string message, Brush foreground)
		{
			TextBlock statusBlock = new TextBlock
			{
				Text = message,
				FontSize = 12,
				TextWrapping = TextWrapping.Wrap,
				Foreground = foreground,
				Margin = new Thickness(0, 2, 0, 2)
			};
			MessagePanel.Children.Add(statusBlock);
			ScrollToEnd();
		}

		private void ScrollToEnd()
		{
			base.Dispatcher.BeginInvoke(new Action(() =>
			{
				MainScrollViewer.ScrollToEnd();
			}), DispatcherPriority.Background);
		}

		private void SaveSkillList()
		{
			var array = new JArray();
			foreach (var entry in _skillEntries)
			{
				array.Add(new JObject
				{
					["Name"] = entry.Name,
					["Content"] = entry.Content
				});
			}
			ForkPlusSettings.Default.AiDevSkillList = array.ToString(Newtonsoft.Json.Formatting.None);
			ForkPlusSettings.Default.Save();
		}

		private void LoadSkillList()
		{
			string json = ForkPlusSettings.Default.AiDevSkillList?.Trim();
			if (string.IsNullOrWhiteSpace(json)) return;
			try
			{
				var array = JArray.Parse(json);
				foreach (var item in array)
				{
					string name = item["Name"]?.Value<string>() ?? "";
					string content = item["Content"]?.Value<string>() ?? "";
					if (!string.IsNullOrWhiteSpace(name))
					{
						_skillEntries.Add(new AiSkillEntry { Name = name, Content = content });
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to load skill list: " + ex.Message);
			}
		}

		private string BuildDevelopmentPrompt(string requirement)
		{
			string repoPath = _gitModule?.Path ?? "";
			string prompt = $@"You are an AI coding assistant integrated into ForkPlus, a Git client. 
The user has the following development requirement:

{requirement}

Current repository path: {repoPath}

Please analyze the requirement and generate necessary code changes.
Respond with structured file changes in the following format for each file you want to modify:

===FILE: relative/file/path===
```language
// FULL file content after changes (complete file, not just the diff)
```

If you need to create a new file, include the full content.
If you need to modify an existing file, include the complete updated file content.
If you need to delete a file, respond with:
===FILE: relative/file/path===
DELETE

Only include files that actually need to change. Do NOT include files that are not related to the requirement.
Always provide complete file contents, never just diffs or partial snippets.
Make sure the code compiles and follows the project's existing patterns and conventions.";

			// Append loaded skills
			if (_skillEntries.Count > 0)
			{
				prompt += $@"

Additionally, the user has defined the following coding standards / skills that you MUST follow:";
				foreach (var entry in _skillEntries)
				{
					if (!string.IsNullOrWhiteSpace(entry.Content))
					{
						prompt += $@"

--- {entry.Name} ---
{entry.Content}";
					}
				}
			}

			return prompt;
		}

		private class ParsedAiChanges
		{
			public List<ParsedFileChange> Files { get; } = new List<ParsedFileChange>();
		}

		private class ParsedFileChange
		{
			public string FilePath { get; set; }
			public string Content { get; set; }
			public bool IsDelete { get; set; }
		}

		private class AiFileChange
		{
			public string FilePath { get; set; }
			public string OldContent { get; set; }
			public string NewContent { get; set; }
			public bool IsNewFile { get; set; }
			public bool IsDelete { get; set; }
		}

		private static ParsedAiChanges ParseAiResponse(string response)
		{
			ParsedAiChanges changes = new ParsedAiChanges();
			string[] lines = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
			ParsedFileChange currentFile = null;
			bool inCodeBlock = false;
			List<string> codeLines = new List<string>();

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];

				if (line.TrimStart().StartsWith("===FILE:"))
				{
					// Save previous file
					if (currentFile != null)
					{
						if (inCodeBlock && codeLines.Count > 0)
						{
							currentFile.Content = string.Join("\n", codeLines);
						}
						changes.Files.Add(currentFile);
					}
					codeLines.Clear();
					inCodeBlock = false;

					string filePath = line.Substring(line.IndexOf(':') + 1).Trim().Trim('=').Trim();
					currentFile = new ParsedFileChange { FilePath = filePath };
					continue;
				}

				if (currentFile != null)
				{
					if (line.Trim().Equals("DELETE"))
					{
						currentFile.IsDelete = true;
						continue;
					}

					if (line.TrimStart().StartsWith("```"))
					{
						if (inCodeBlock)
						{
							// End of code block
							currentFile.Content = string.Join("\n", codeLines);
							inCodeBlock = false;
						}
						else
						{
							inCodeBlock = true;
							codeLines.Clear();
						}
						continue;
					}

					if (inCodeBlock)
					{
						codeLines.Add(line);
					}
				}
			}

			// Save last file
			if (currentFile != null)
			{
				if (inCodeBlock && codeLines.Count > 0)
				{
					currentFile.Content = string.Join("\n", codeLines);
				}
				changes.Files.Add(currentFile);
			}

			return changes;
		}

		private Dictionary<string, string> GetCurrentFileContents()
		{
			Dictionary<string, string> contents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (_gitModule?.Path == null)
			{
				return contents;
			}
			try
			{
				string workDir = _gitModule.Path;
				foreach (string file in Directory.EnumerateFiles(workDir, "*.*", SearchOption.AllDirectories)
					.Where(f => !f.Contains("\\.git\\") && !f.Contains("\\.git/"))
					.Take(100))
				{
					try
					{
						string relativePath = GetRelativePath(workDir, file);
						contents[relativePath] = File.ReadAllText(file);
					}
					catch
					{
						// Skip files that can't be read
					}
				}
			}
			catch
			{
				// Ignore errors
			}
			return contents;
		}

		private static string GetRelativePath(string basePath, string fullPath)
		{
			basePath = basePath.TrimEnd('\\', '/') + "\\";
			if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
			{
				return fullPath.Substring(basePath.Length);
			}
			return fullPath;
		}

		/// <summary>
		/// 获取 AI 允许修改的目录列表：
		/// - 当前仓库目录（始终允许）
		/// - 如果当前仓库是子模块：父仓目录 + 所有兄弟子模块目录
		/// - 如果当前仓库有子模块：所有子模块目录
		/// 所有路径均经 Path.GetFullPath 规范化，防止路径穿越。
		/// </summary>
		private List<string> GetAllowedDirectories()
		{
			List<string> allowed = new List<string>();
			string workDir = _gitModule?.Path;
			if (workDir == null)
			{
				return allowed;
			}

			// 1. 当前仓库目录（始终允许）
			allowed.Add(Path.GetFullPath(workDir));

			if (_gitModule.ParentRepoPath != null)
			{
				// 当前仓库是子模块：允许父仓目录
				string parentPath = Path.GetFullPath(_gitModule.ParentRepoPath);
				if (!allowed.Contains(parentPath, StringComparer.OrdinalIgnoreCase))
				{
					allowed.Add(parentPath);
				}

				// 也允许兄弟子模块目录（父仓下所有子模块）
				try
				{
					string parentGitModules = System.IO.Path.Combine(parentPath, ".gitmodules");
					GitCommandResult<Submodule[]> result = new GetSubmodulesGitCommand().Execute(parentGitModules);
					if (result.Succeeded)
					{
						foreach (Submodule sm in result.Result)
						{
							string siblingPath = Path.GetFullPath(System.IO.Path.Combine(parentPath, sm.Path));
							if (!allowed.Contains(siblingPath, StringComparer.OrdinalIgnoreCase))
							{
								allowed.Add(siblingPath);
							}
						}
					}
				}
				catch
				{
					// 无法读取子模块配置时，仅允许父仓
				}
			}
			else
			{
				// 当前仓库是普通仓库：如果有子模块，也允许子模块目录
				try
				{
					GitCommandResult<Submodule[]> result = new GetSubmodulesGitCommand().Execute(_gitModule);
					if (result.Succeeded)
					{
						foreach (Submodule sm in result.Result)
						{
							string smPath = Path.GetFullPath(System.IO.Path.Combine(workDir, sm.Path));
							if (!allowed.Contains(smPath, StringComparer.OrdinalIgnoreCase))
							{
								allowed.Add(smPath);
							}
						}
					}
				}
				catch
				{
					// 无法读取子模块配置时，仅允许仓库目录
				}
			}

			return allowed;
		}

		/// <summary>
		/// 检查文件路径是否在允许的目录范围内（防止路径穿越攻击）。
		/// </summary>
		private static bool IsPathInAllowedDirectories(string fullPath, List<string> allowedDirectories)
		{
			string normalizedPath = Path.GetFullPath(fullPath);
			foreach (string allowedDir in allowedDirectories)
			{
				string normalizedAllowedDir = Path.GetFullPath(allowedDir);
				// 确保路径以分隔符结尾，防止 /dir 匹配 /dir-other
				if (!normalizedAllowedDir.EndsWith("\\"))
				{
					normalizedAllowedDir += "\\";
				}
				if (normalizedPath.StartsWith(normalizedAllowedDir, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// 获取 git 索引中指定文件的內容（git show :path），用于与写入内容对比，
		/// 避免写入相同内容导致 git 误报文件被修改。
		/// </summary>
		private string GetIndexContent(string relativePath)
		{
			try
			{
				GitCommandResult<MemoryStream> result = new GetBlobGitCommand().Execute(_gitModule, new BlobTarget.Revision("", relativePath));
				if (result.Succeeded && result.Result != null)
				{
					using (StreamReader reader = new StreamReader(result.Result, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
					{
						return reader.ReadToEnd();
					}
				}
			}
			catch
			{
				// 文件可能不在索引中（新建文件）
			}
			return null;
		}

		/// <summary>
		/// 检测文件的换行符风格，返回该文件中使用的行结束符。
		/// </summary>
		private static string DetectLineEnding(string content)
		{
			if (content == null) return "\n";
			int crlfIdx = content.IndexOf("\r\n", StringComparison.Ordinal);
			if (crlfIdx >= 0) return "\r\n";
			int lfIdx = content.IndexOf('\n');
			if (lfIdx >= 0) return "\n";
			return Environment.NewLine;
		}

		/// <summary>
		/// 将内容转换为与原始内容相同的换行符风格。
		/// </summary>
		private static string NormalizeLineEndings(string content, string targetLineEnding)
		{
			if (string.IsNullOrEmpty(content)) return content;
			// 先把所有换行统一为 \n
			string normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
			// 再替换为目标换行符
			if (targetLineEnding == "\r\n")
			{
				return normalized.Replace("\n", "\r\n");
			}
			return normalized;
		}

		private List<AiFileChange> ApplyFileChanges(ParsedAiChanges parsedChanges, Dictionary<string, string> beforeContents)
		{
			List<AiFileChange> appliedChanges = new List<AiFileChange>();
			string workDir = _gitModule?.Path;
			if (workDir == null)
			{
				return appliedChanges;
			}

			// 路径安全：计算允许修改的目录列表
			List<string> allowedDirectories = GetAllowedDirectories();

			foreach (ParsedFileChange fileChange in parsedChanges.Files)
			{
				string fullPath = System.IO.Path.Combine(workDir, fileChange.FilePath);
				string resolvedPath = Path.GetFullPath(fullPath);

				// 安全检查：拒绝越界路径
				if (!IsPathInAllowedDirectories(resolvedPath, allowedDirectories))
				{
					base.Dispatcher.Async(delegate
					{
						AddStatusMessage(
							PreferencesLocalization.FormatCurrent("⚠️ 安全限制: 拒绝修改目录外的文件 {0}", fileChange.FilePath),
							Brushes.OrangeRed);
					});
					continue;
				}

				string dirPath = System.IO.Path.GetDirectoryName(resolvedPath);

				AiFileChange change = new AiFileChange
				{
					FilePath = fileChange.FilePath,
					IsDelete = fileChange.IsDelete,
					IsNewFile = false
				};

				if (fileChange.IsDelete)
				{
					if (File.Exists(resolvedPath))
					{
						change.OldContent = beforeContents.TryGetValue(fileChange.FilePath, out var oldContent) ? oldContent : File.ReadAllText(resolvedPath);
						change.NewContent = null;
						File.Delete(resolvedPath);
						appliedChanges.Add(change);
					}
					continue;
				}

				if (string.IsNullOrWhiteSpace(fileChange.Content))
				{
					continue;
				}

				// Remove trailing newlines for consistent comparison
				string newContent = fileChange.Content.TrimEnd('\r', '\n');
				bool fileExists = File.Exists(resolvedPath);

				if (!fileExists)
				{
					// New file
					if (!Directory.Exists(dirPath))
					{
						Directory.CreateDirectory(dirPath);
					}
					change.IsNewFile = true;
					change.OldContent = "";
					change.NewContent = newContent;
					File.WriteAllText(resolvedPath, newContent);
					appliedChanges.Add(change);
				}
				else
				{
					// Read current on-disk content
					string onDiskContent = File.ReadAllText(resolvedPath);
					string onDiskLineEnding = DetectLineEnding(onDiskContent);

					// Normalize the AI output to use the same line endings as the current file
					string normalizedNewContent = NormalizeLineEndings(newContent, onDiskLineEnding);

					// Compare after normalizing line endings (trim trailing newlines for consistency)
					string onDiskTrimmed = onDiskContent.TrimEnd('\r', '\n');
					string newTrimmed = normalizedNewContent.TrimEnd('\r', '\n');

					if (onDiskTrimmed != newTrimmed)
					{
						// Compare against git index as well to confirm the change is meaningful
						string indexContent = GetIndexContent(fileChange.FilePath);
						if (indexContent != null)
						{
							string indexTrimmed = indexContent.TrimEnd('\r', '\n');
							if (indexTrimmed == newTrimmed)
							{
								// AI output matches git index content - no real change needed
								// But the file on disk might differ from index; if so, restore from index
								if (onDiskTrimmed != indexTrimmed)
								{
									// Write the index content to disk to clear stale modification
									try
									{
										string indexLineEnding = DetectLineEnding(indexContent);
										string normalizedIndexContent = NormalizeLineEndings(indexContent, indexLineEnding);
										File.WriteAllText(resolvedPath, normalizedIndexContent, Encoding.UTF8);
									}
									catch { }
								}
								continue;
							}
						}

						change.OldContent = onDiskContent;
						change.NewContent = normalizedNewContent;
						File.WriteAllText(resolvedPath, normalizedNewContent, Encoding.UTF8);
						appliedChanges.Add(change);
					}
				}
			}

			return appliedChanges;
		}

		private void ShowDiffResults(List<AiFileChange> changes)
		{
			// Create a container for diff results
			Border diffContainer = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(10, 0, 0, 0)),
				CornerRadius = new CornerRadius(6),
				Padding = new Thickness(10, 8, 10, 8),
				Margin = new Thickness(0, 4, 0, 4),
				BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215)),
				BorderThickness = new Thickness(1)
			};

			StackPanel diffs = new StackPanel();

			// Header
			TextBlock diffHeader = new TextBlock
			{
				Text = PreferencesLocalization.FormatCurrent("📝 文件变更 ({0} 个文件)", changes.Count),
				FontSize = 13,
				FontWeight = FontWeights.SemiBold,
				Margin = new Thickness(0, 0, 0, 6)
			};
			diffs.Children.Add(diffHeader);

			foreach (AiFileChange change in changes)
			{
				// File header
				TextBlock headerBlock = new TextBlock
				{
					Text = change.IsNewFile
						? PreferencesLocalization.FormatCurrent("📄 新建: {0}", change.FilePath)
						: change.IsDelete
							? PreferencesLocalization.FormatCurrent("🗑️ 删除: {0}", change.FilePath)
							: PreferencesLocalization.FormatCurrent("✏️ 修改: {0}", change.FilePath),
					FontSize = 13,
					FontWeight = FontWeights.Medium,
					Margin = new Thickness(0, 6, 0, 2),
					Foreground = change.IsNewFile ? Brushes.Green : change.IsDelete ? Brushes.Red : Brushes.DodgerBlue
				};
				diffs.Children.Add(headerBlock);

				// Diff content
				if (!change.IsDelete && change.OldContent != change.NewContent)
				{
					Border diffBorder = new Border
					{
						Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)),
						BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)),
						BorderThickness = new Thickness(1),
						Margin = new Thickness(0, 0, 0, 8),
						MaxHeight = 300
					};

					ScrollViewer diffScroll = new ScrollViewer
					{
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
						HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
					};

					TextBlock diffTextBlock = new TextBlock
					{
						FontFamily = new FontFamily("Consolas"),
						FontSize = 12,
						Padding = new Thickness(8, 4, 8, 4),
						Text = GenerateUnifiedDiffText(change),
						TextWrapping = TextWrapping.NoWrap
					};

					diffScroll.Content = diffTextBlock;
					diffBorder.Child = diffScroll;
					diffs.Children.Add(diffBorder);
				}
				else if (change.IsNewFile)
				{
					Border newFileBorder = new Border
					{
						Background = new SolidColorBrush(Color.FromArgb(15, 0, 128, 0)),
						BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0, 128, 0)),
						BorderThickness = new Thickness(1),
						Margin = new Thickness(0, 0, 0, 8),
						MaxHeight = 300
					};

					ScrollViewer diffScroll = new ScrollViewer
					{
						VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
						HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
					};

					TextBlock diffTextBlock = new TextBlock
					{
						FontFamily = new FontFamily("Consolas"),
						FontSize = 12,
						Padding = new Thickness(8, 4, 8, 4),
						Text = change.NewContent,
						TextWrapping = TextWrapping.NoWrap
					};

					diffScroll.Content = diffTextBlock;
					newFileBorder.Child = diffScroll;
					diffs.Children.Add(newFileBorder);
				}
			}

			diffContainer.Child = diffs;
			MessagePanel.Children.Add(diffContainer);
			ScrollToEnd();
		}

		private static string GenerateUnifiedDiffText(AiFileChange change)
		{
			string[] oldLines = (change.OldContent ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
			string[] newLines = (change.NewContent ?? "").Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

			int maxLineNumDigits = Math.Max(
				(oldLines.Length + 1).ToString().Length,
				(newLines.Length + 1).ToString().Length
			);

			// Simple LCS-based diff generation
			List<string> result = new List<string>();

			int oldIdx = 0, newIdx = 0;
			while (oldIdx < oldLines.Length || newIdx < newLines.Length)
			{
				if (oldIdx < oldLines.Length && newIdx < newLines.Length && oldLines[oldIdx] == newLines[newIdx])
				{
					// Context line
					result.Add($"  {(oldIdx + 1).ToString().PadLeft(maxLineNumDigits)} {oldLines[oldIdx]}");
					oldIdx++;
					newIdx++;
				}
				else
				{
					// Find next common line or end
					bool found = false;
					for (int lookahead = 1; lookahead <= Math.Min(10, Math.Max(oldLines.Length - oldIdx, newLines.Length - newIdx)); lookahead++)
					{
						if (oldIdx + lookahead < oldLines.Length && newIdx < newLines.Length && oldLines[oldIdx + lookahead] == newLines[newIdx])
						{
							// Deleted lines
							for (int d = 0; d < lookahead; d++)
							{
								result.Add($"- {(oldIdx + d + 1).ToString().PadLeft(maxLineNumDigits)} {oldLines[oldIdx + d]}");
							}
							oldIdx += lookahead;
							found = true;
							break;
						}
						if (newIdx + lookahead < newLines.Length && oldIdx < oldLines.Length && oldLines[oldIdx] == newLines[newIdx + lookahead])
						{
							// Added lines
							for (int a = 0; a < lookahead; a++)
							{
								result.Add($"+ {(newIdx + a + 1).ToString().PadLeft(maxLineNumDigits)} {newLines[newIdx + a]}");
							}
							newIdx += lookahead;
							found = true;
							break;
						}
					}

					if (!found)
					{
						if (oldIdx < oldLines.Length)
						{
							result.Add($"- {(oldIdx + 1).ToString().PadLeft(maxLineNumDigits)} {oldLines[oldIdx]}");
							oldIdx++;
						}
						if (newIdx < newLines.Length)
						{
							result.Add($"+ {(newIdx + 1).ToString().PadLeft(maxLineNumDigits)} {newLines[newIdx]}");
							newIdx++;
						}
					}
				}
			}

			return string.Join("\n", result);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
