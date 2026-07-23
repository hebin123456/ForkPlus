using Avalonia.Controls.Selection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>AI Commit Composer (WIP拆分) 预览窗口。
	/// 调用方（CommitUserControl）传入 gitModule + 已暂存文件列表，本窗口：
	/// 1. 调 OpenAiService.GenerateWipCommitSplits 流式获取 AI 拆分方案（JSON）。
	/// 2. 用 OpenAiService.ParseWipCommitPlan 解析为 WipCommitPlan。
	/// 3. 左侧 ListBox 展示分组、中间展示文件、右侧可编辑 subject/body。
	/// 4. 用户点 Apply All 后用 ComposeWipCommitsGitCommand 按顺序 stage + commit。
	/// 5. 完成后关闭窗口，触发主界面 Refresh 让 commit 列表更新。</summary>
	public partial class AiCommitComposerWindow : CustomWindow, ILocalizableControl
	{
		private readonly GitModule _gitModule;
		private readonly bool _amend;
		private readonly ChangedFile[] _stagedFiles;

		private WipCommitPlan _plan;
		private bool _aiRunning;
		private bool _applying;
		private JobMonitor _currentMonitor;
		private bool _modelListLoaded;

		// 用户在 SubjectTextBox/BodyTextBox 中编辑时，回写到当前选中的 WipCommitGroup。
		// 切换分组时把新选中分组的 subject/body 写回 TextBox，期间用 _suppressTextBoxSync 抑制 TextChanged。
		private bool _suppressTextBoxSync;

		public AiCommitComposerWindow(GitModule gitModule, ChangedFile[] stagedFiles, bool amend)
		{
			InitializeComponent();
			_gitModule = gitModule;
			_amend = amend;
			_stagedFiles = stagedFiles ?? new ChangedFile[0];
			PreferencesLocalization.ApplyCurrent(this);
			Loaded += AiCommitComposerWindow_Loaded;
		}

		private void AiCommitComposerWindow_Loaded(object sender, RoutedEventArgs e)
		{
			InitializeModelComboBox();
			ApplyLocalizationToButtons();
			// 启动时立即触发一次 AI 请求
			StartAiRequest();
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.ApplyCurrent(this);
			ApplyLocalizationToButtons();
		}

		private void ApplyLocalizationToButtons()
		{
			TitleTextBlock.Text = PreferencesLocalization.Current("AI Commit Composer");
			GroupsHeaderLabel.Text = PreferencesLocalization.Current("Commit Groups");
			FilesHeaderLabel.Text = PreferencesLocalization.Current("Files in this group");
			MessageHeaderLabel.Text = PreferencesLocalization.Current("Commit Message");
			SubjectTextBox.Placeholder = PreferencesLocalization.Current("Subject");
			BodyTextBox.Placeholder = PreferencesLocalization.Current("Body (optional)");
			RetryButton.ToolTip = PreferencesLocalization.Current("Retry");
			StopButton.ToolTip = PreferencesLocalization.Current("Stop the current AI task");
			ModelComboBox.ToolTip = PreferencesLocalization.Current("Select AI model");
			ApplyAllButton.Content = PreferencesLocalization.Current("Apply All");
			CancelButton.Content = PreferencesLocalization.Current("Cancel");
		}

		/// <summary>初始化模型下拉框。先用当前选中模型占位，再后台拉取完整列表（参考 AiTextResultWindow）。</summary>
		private void InitializeModelComboBox()
		{
			string currentModel = ForkPlusSettings.Default.AiReviewSelectedModel;
			if (!string.IsNullOrWhiteSpace(currentModel))
			{
				ModelComboBox.Items.Add(currentModel);
				ModelComboBox.SelectedIndex = 0;
			}
			else
			{
				ModelComboBox.Items.Add(PreferencesLocalization.Current("Select model..."));
				ModelComboBox.SelectedIndex = 0;
			}

			System.Threading.ThreadPool.QueueUserWorkItem(delegate(object state)
			{
				List<string> models = null;
				try
				{
					if (OpenAiService.IsAiReviewConfigured())
					{
						OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
						ServiceResult<string[]> result = aiService.ListModels();
						if (result.Succeeded && result.Result != null)
						{
							models = new List<string>(result.Result);
						}
					}
				}
				catch (Exception ex)
				{
					Log.Warn("AiCommitComposerWindow failed to load AI model list: " + ex.Message);
				}

				if (models == null || models.Count == 0)
				{
					return;
				}

				Dispatcher.Async(delegate
				{
					try
					{
						if (_modelListLoaded)
						{
							return;
						}
						_modelListLoaded = true;
						string selected = ForkPlusSettings.Default.AiReviewSelectedModel;
						ModelComboBox.Items.Clear();
						foreach (string m in models)
						{
							if (!string.IsNullOrWhiteSpace(m))
							{
								ModelComboBox.Items.Add(m);
							}
						}
						int idx = -1;
						for (int i = 0; i < ModelComboBox.Items.Count; i++)
						{
							if (string.Equals((string)ModelComboBox.Items[i], selected, StringComparison.OrdinalIgnoreCase))
							{
								idx = i;
								break;
							}
						}
						if (idx >= 0)
						{
							ModelComboBox.SelectedIndex = idx;
						}
						else if (!string.IsNullOrWhiteSpace(selected))
						{
							ModelComboBox.Items.Insert(0, selected);
							ModelComboBox.SelectedIndex = 0;
						}
						else if (ModelComboBox.Items.Count > 0)
						{
							ModelComboBox.SelectedIndex = 0;
						}
					}
					catch (Exception ex)
					{
						Log.Warn("AiCommitComposerWindow failed to populate model combo box: " + ex.Message);
					}
				});
			});
		}

		private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ModelComboBox.SelectedItem == null)
			{
				return;
			}
			string selected = (string)ModelComboBox.SelectedItem;
			if (string.IsNullOrWhiteSpace(selected) || selected == PreferencesLocalization.Current("Select model..."))
			{
				return;
			}
			if (string.Equals(selected, ForkPlusSettings.Default.AiReviewSelectedModel, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewSelectedModel = selected;
			ForkPlusSettings.Default.Save();
			StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Model switched to: {0}", selected);
		}

		/// <summary>启动 AI 请求：拉取 staged diff → 调 GenerateWipCommitSplits 流式 → 解析为 WipCommitPlan → 填充 UI。</summary>
		private void StartAiRequest()
		{
			if (_aiRunning || _applying)
			{
				return;
			}
			if (!OpenAiService.IsAiReviewConfigured())
			{
				MessageBox.Show(
					PreferencesLocalization.Current("AI is not configured. Please configure AI review settings in Preferences first."),
					PreferencesLocalization.Current("AI Commit Composer"),
					MessageBoxButton.OK,
					MessageBoxImage.Warning);
				return;
			}
			if (_stagedFiles.Length == 0)
			{
				MessageBox.Show(
					PreferencesLocalization.Current("No staged files to compose. Stage some files first."),
					PreferencesLocalization.Current("AI Commit Composer"),
					MessageBoxButton.OK,
					MessageBoxImage.Information);
				return;
			}

			_aiRunning = true;
			RetryButton.IsEnabled = false;
			ApplyAllButton.IsEnabled = false;
			StopButton.Visibility = Visibility.Visible;
			StatusProgressBar.Visibility = Visibility.Visible;
			StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Composing with {0}...", ForkPlusSettings.Default.AiReviewSelectedModel ?? "AI");
			GroupsListBox.Items.Clear();
			FilesListBox.Items.Clear();
			ReasonTextBlock.Text = "";
			SubjectTextBox.Text = "";
			BodyTextBox.Text = "";
			UnassignedFilesHint.Text = "";

			StringBuilder liveOutput = new StringBuilder();
			RepositoryUserControl activeRepo = MainWindow.ActiveRepositoryUserControl;
			if (activeRepo == null)
			{
				_aiRunning = false;
				RetryButton.IsEnabled = true;
				StopButton.Visibility = Visibility.Collapsed;
				StatusProgressBar.Visibility = Visibility.Collapsed;
				StatusTextBlock.Text = PreferencesLocalization.Current("No active repository.");
				return;
			}
			activeRepo.JobQueue.Add(PreferencesLocalization.Current("AI Commit Composer"), delegate(JobMonitor monitor)
			{
				_currentMonitor = monitor;
				try
				{
					// 1. 获取 staged diff
					GitCommandResult<string> patchResult = new GetWorkingDirectoryFileChangesGitCommand().GetStagedPatch(_gitModule, _amend);
					if (!patchResult.Succeeded)
					{
						Dispatcher.Async(delegate
						{
							ShowError(patchResult.Error.FriendlyDescription);
						});
						return;
					}
					if (monitor.IsCanceled)
					{
						return;
					}

					// 2. AI 流式请求
					string[] filePaths = _stagedFiles.Select(f => f.Path).ToArray();
					OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<OpenAiResponse> response = openAiService.GenerateWipCommitSplits(
						patchResult.Result, filePaths, _gitModule, monitor,
						delegate(string chunk)
						{
							if (string.IsNullOrEmpty(chunk))
							{
								return;
							}
							liveOutput.Append(chunk);
							int chars = liveOutput.Length;
							Dispatcher.Async(delegate
							{
								StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Generating... ({0} chars)", chars);
							});
						});

					if (monitor.IsCanceled)
					{
						return;
					}
					if (!response.Succeeded)
					{
						Dispatcher.Async(delegate
						{
							ShowError(response.Error.FriendlyMessage);
						});
						return;
					}

					// 3. 解析为 WipCommitPlan
					WipCommitPlan plan = OpenAiService.ParseWipCommitPlan(response.Result.Message, _stagedFiles);
					Dispatcher.Async(delegate
					{
						if (monitor.IsCanceled)
						{
							return;
						}
						if (plan == null || plan.Groups.Count == 0)
						{
							ShowError(PreferencesLocalization.Current("AI returned an invalid plan. Please retry, or compose commits manually."));
							return;
						}
						_plan = plan;
						PopulateGroupsListBox();
						int unassigned = plan.GetUnassignedFiles().Count;
						if (unassigned > 0)
						{
							UnassignedFilesHint.Text = PreferencesLocalization.FormatCurrent("{0} staged file(s) not assigned to any group.", unassigned);
						}
						else
						{
							UnassignedFilesHint.Text = "";
						}
						StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Composed {0} groups", plan.Groups.Count);
						ApplyAllButton.IsEnabled = plan.Groups.Count > 0;
					});
				}
				finally
				{
					Dispatcher.Async(delegate
					{
						_aiRunning = false;
						_currentMonitor = null;
						RetryButton.IsEnabled = true;
						StopButton.Visibility = Visibility.Collapsed;
						StatusProgressBar.Visibility = Visibility.Collapsed;
					});
				}
			}, JobFlags.SaveToLog);
		}

		private void ShowError(string message)
		{
			StatusTextBlock.Text = PreferencesLocalization.Current("Failed");
			new ErrorWindow(message).ShowDialog();
		}

		/// <summary>把分组的 subject + 文件数填到左侧 ListBox。每个 ListBoxItem.Tag = WipCommitGroup。</summary>
		private void PopulateGroupsListBox()
		{
			GroupsListBox.Items.Clear();
			for (int i = 0; i < _plan.Groups.Count; i++)
			{
				WipCommitGroup group = _plan.Groups[i];
				ListBoxItem item = new ListBoxItem
				{
					Tag = group,
					Content = PreferencesLocalization.FormatCurrent("{0}. {1}  ({2} files)", i + 1, group.Subject, group.MatchedFileCount)
				};
				GroupsListBox.Items.Add(item);
			}
			if (GroupsListBox.Items.Count > 0)
			{
				GroupsListBox.SelectedIndex = 0;
			}
		}

		private void GroupsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (GroupsListBox.SelectedItem is ListBoxItem item && item.Tag is WipCommitGroup group)
			{
				DisplayGroupDetails(group);
			}
			else
			{
				FilesListBox.Items.Clear();
				ReasonTextBlock.Text = "";
				SubjectTextBox.Text = "";
				BodyTextBox.Text = "";
			}
		}

		/// <summary>把选中分组的文件 / reason / subject / body 写到中间和右侧控件。</summary>
		private void DisplayGroupDetails(WipCommitGroup group)
		{
			_suppressTextBoxSync = true;
			try
			{
				FilesListBox.Items.Clear();
				foreach (ChangedFile matched in group.MatchedFiles)
				{
					FilesListBox.Items.Add(matched.Path);
				}
				// 显示 AI 给出但未匹配到 staged 文件的路径（让用户知道 AI 写错了路径）
				foreach (string unmatched in group.UnmatchedFiles)
				{
					ListBoxItem warn = new ListBoxItem
					{
						Content = PreferencesLocalization.FormatCurrent("{0}  (not staged)", unmatched),
						Foreground = System.Windows.Media.Brushes.OrangeRed
					};
					FilesListBox.Items.Add(warn);
				}
				ReasonTextBlock.Text = string.IsNullOrEmpty(group.Reason) ? "" : PreferencesLocalization.FormatCurrent("Reason: {0}", group.Reason);
				SubjectTextBox.Text = group.Subject ?? "";
				BodyTextBox.Text = group.Body ?? "";
			}
			finally
			{
				_suppressTextBoxSync = false;
			}
		}

		private void SubjectTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_suppressTextBoxSync)
			{
				return;
			}
			if (GroupsListBox.SelectedItem is ListBoxItem item && item.Tag is WipCommitGroup group)
			{
				group.Subject = SubjectTextBox.Text;
				// 同步刷新左侧 ListBox 文案
				int idx = GroupsListBox.SelectedIndex;
				if (idx >= 0)
				{
					item.Content = PreferencesLocalization.FormatCurrent("{0}. {1}  ({2} files)", idx + 1, group.Subject, group.MatchedFileCount);
				}
			}
		}

		private void BodyTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_suppressTextBoxSync)
			{
				return;
			}
			if (GroupsListBox.SelectedItem is ListBoxItem item && item.Tag is WipCommitGroup group)
			{
				group.Body = BodyTextBox.Text;
			}
		}

		private void RetryButton_Click(object sender, RoutedEventArgs e)
		{
			StartAiRequest();
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			_currentMonitor?.Cancel();
		}

		private void CancelButton_Click(object sender, RoutedEventArgs e)
		{
			if (_applying)
			{
				_currentMonitor?.Cancel();
				return;
			}
			Close();
		}

		/// <summary>Apply All：把所有分组（含用户编辑后的 subject/body）交给 ComposeWipCommitsGitCommand 执行。
		/// 用 AddUndoable 包裹，让 v3.0.0 引入的 Undo/Redo 栈能撤销整批 commit。</summary>
		private void ApplyAllButton_Click(object sender, RoutedEventArgs e)
		{
			if (_plan == null || _plan.Groups.Count == 0)
			{
				return;
			}
			if (_applying)
			{
				return;
			}
			// 校验每个分组的 subject 非空（git commit 不允许空 message）
			for (int i = 0; i < _plan.Groups.Count; i++)
			{
				WipCommitGroup g = _plan.Groups[i];
				if (g.MatchedFileCount == 0)
				{
					continue;
				}
				if (string.IsNullOrWhiteSpace(g.Subject))
				{
					MessageBox.Show(
						PreferencesLocalization.FormatCurrent("Group {0} has an empty subject. Please fill in a commit subject or skip the group.", i + 1),
						PreferencesLocalization.Current("AI Commit Composer"),
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					return;
				}
			}

			_applying = true;
			ApplyAllButton.IsEnabled = false;
			RetryButton.IsEnabled = false;
			StopButton.Visibility = Visibility.Visible;
			StatusProgressBar.Visibility = Visibility.Visible;
			StatusTextBlock.Text = PreferencesLocalization.Current("Applying...");

			RepositoryUserControl activeRepo = MainWindow.ActiveRepositoryUserControl;
			if (activeRepo == null)
			{
				_applying = false;
				ApplyAllButton.IsEnabled = true;
				RetryButton.IsEnabled = true;
				StopButton.Visibility = Visibility.Collapsed;
				StatusProgressBar.Visibility = Visibility.Collapsed;
				StatusTextBlock.Text = PreferencesLocalization.Current("No active repository.");
				return;
			}
			activeRepo.AddUndoable(PreferencesLocalization.Current("Compose WIP commits"), delegate(JobMonitor monitor)
			{
				_currentMonitor = monitor;
				GitCommandResult<string[]> result = new ComposeWipCommitsGitCommand().Execute(_gitModule, _plan, commitAndPush: false, monitor: monitor);
				Dispatcher.Async(delegate
				{
					_applying = false;
					_currentMonitor = null;
					StopButton.Visibility = Visibility.Collapsed;
					StatusProgressBar.Visibility = Visibility.Collapsed;
					if (result.Succeeded)
					{
						StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Composed {0} commits", result.Result.Length);
						// 刷新仓库视图让 commit 列表和 staged 区更新
						try
						{
							activeRepo.InvalidateAndRefresh(SubDomain.All);
						}
						catch (Exception ex)
						{
							Log.Warn("AiCommitComposerWindow: failed to refresh after apply: " + ex.Message);
						}
						Close();
					}
					else
					{
						StatusTextBlock.Text = PreferencesLocalization.Current("Failed");
						new ErrorWindow(result.Error.FriendlyDescription).ShowDialog();
						ApplyAllButton.IsEnabled = true;
						RetryButton.IsEnabled = true;
					}
				});
				return result.Succeeded ? GitCommandResult.Success() : GitCommandResult.Failure(result.Error);
			}, JobFlags.SaveToLog);
		}
	}
}
