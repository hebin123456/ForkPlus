using ForkPlus;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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

		// 撤销支持：记录上一次 AI 修改前的文件内容
		private Dictionary<string, string> _lastBeforeContents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		private readonly DispatcherTimer _statusTimer;

		private List<AiSkillEntry> _skillEntries;

		// Queue for pending requests when one is in progress
		private readonly Queue<string> _pendingRequests = new Queue<string>();

		private bool _isProcessing;

		// 多轮对话记忆：按顺序存储历史 user/assistant 消息（不含 system prompt）
		private readonly List<JObject> _conversationHistory = new List<JObject>();

		// 单轮对话最大保留条数（防止 token 超限），超出时触发自动压缩
		private const int MaxHistoryMessages = 20;

		// 上下文压缩：估算 token 上限（超过则压缩早期对话），保留最近的消息条数
		private const int MaxContextTokenEstimate = 6000;
		private const int KeepRecentMessagesOnCompress = 6;
		private bool _isCompressingContext;
		private bool _modelListLoaded;

		// 流式输出的实时 Markdown 缓冲（边收边渲染到 WebView）
		private StringBuilder _streamingMarkdown;

		// 保护 _streamingMarkdown 的并发追加（chunk 来自后台 job 线程，渲染来自 UI 线程）
		private readonly object _streamingLock = new object();

		// 流式预览渲染节流：避免每个 chunk 都触发一次 markdown→html→NavigateToString 造成卡顿
		private DateTime _lastStreamingRenderUtc = DateTime.MinValue;
		private const int StreamingRenderIntervalMs = 400;

		// 当前流式响应的 WebView2（onChunk 追加到 _streamingMarkdown 后节流渲染到这里）
		private WebView2 _streamingWebView;

		// CSS 缓存（从嵌入资源 md-ai-output.css 读取，与 AiCodeReviewWindow 共用样式）
		private static string _cachedCss;

		public AiDevelopmentWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			InitializeComponent();
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			base.Title = PreferencesLocalization.Current("AI-Assisted Development");
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
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
			// 初始化模型下拉：先显示当前选中模型，再后台拉取完整模型列表
			InitializeModelComboBox();
			// 显示欢迎信息
			ShowWelcomeMessage();
		}

		/// <summary>
		/// 初始化右上角模型下拉框：
		/// 1. 先用当前选中模型作为唯一项，避免下拉为空；
		/// 2. 后台异步调用 /v1/models 拉取完整列表，替换填充。
		/// </summary>
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

			// 后台拉取模型列表（不阻塞 UI 线程）
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
					Log.Warn("Failed to load AI model list: " + ex.Message);
				}

				if (models == null || models.Count == 0)
				{
					return;
				}

				// 回到 UI 线程更新下拉框
				base.Dispatcher.Async(delegate
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
						// 选中当前模型；若列表中不包含，插入到首位并选中
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
						Log.Warn("Failed to populate model combo box: " + ex.Message);
					}
				});
			});
		}

		/// <summary>切换模型时保存到设置，并提示用户。</summary>
		private void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			// 首次初始化时也会触发，此时 _modelListLoaded 可能尚未完成；仅在有有效选中项时保存
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
			AddStatusMessage(
				PreferencesLocalization.FormatCurrent("Model switched to: {0}", selected),
				Brushes.Gray);
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
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
			UpdateHintText();
			InputTextBox.Focus();
		}

		/// <summary>更新底部操作提示。</summary>
		private void UpdateHintText()
		{
			bool sendOnEnter = ForkPlusSettings.Default.AiDevSendMode == "Enter";
			HintTextBlock.Text = sendOnEnter
				? PreferencesLocalization.Current("Press Enter to send, Shift+Enter for new line. The AI remembers previous conversation in this session.")
				: PreferencesLocalization.Current("Press Ctrl+Enter to send, Enter for new line. The AI remembers previous conversation in this session.");
		}

		/// <summary>显示欢迎信息（空对话状态）。</summary>
		private void ShowWelcomeMessage()
		{
			Border welcomeBorder = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(10, 0, 120, 215)),
				CornerRadius = new CornerRadius(8),
				Padding = new Thickness(16, 12, 16, 12),
				Margin = new Thickness(0, 4, 0, 8),
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			StackPanel panel = new StackPanel();
			TextBlock title = new TextBlock
			{
				Text = "✦ " + PreferencesLocalization.Current("AI-Assisted Development"),
				FontSize = 15,
				FontWeight = FontWeights.SemiBold,
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
				Margin = new Thickness(0, 0, 0, 6)
			};
			TextBlock desc = new TextBlock
			{
				Text = PreferencesLocalization.Current("Describe your development requirement below. The AI will analyze your codebase and generate file changes. You can have a continuous conversation - the AI remembers previous context in this session."),
				FontSize = 12,
				TextWrapping = TextWrapping.Wrap,
				Foreground = (Brush)FindResource("SecondaryLabelBrush"),
				Margin = new Thickness(0, 0, 0, 4)
			};
			panel.Children.Add(title);
			panel.Children.Add(desc);
			welcomeBorder.Child = panel;
			MessagePanel.Children.Add(welcomeBorder);
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
			SendButton.Content = PreferencesLocalization.Current(isEnter ? "Send (Enter)" : "Send (Ctrl+Enter)");
			UpdateHintText();
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
				// 任务2：当前有请求在处理——将新请求入队，用户可继续输入下一个需求，
				// 无需等待 AI 回复。队列会在当前请求完成后自动按顺序处理。
				_pendingRequests.Enqueue(requirement);
				UpdateQueueIndicator();
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
			// 任务2：不再禁用输入框和发送按钮——用户可以在 AI 处理期间继续输入并排队新需求，
			// 无需等待当前请求回复完成。SendButton 的启用状态仅由输入框文本决定（见 UpdateSendButton）。
			ProgressBar.Visibility = Visibility.Visible;
			StopButton.Visibility = Visibility.Visible;
			UpdateQueueIndicator();
			AddStatusMessage(PreferencesLocalization.Current("排队中..."), Brushes.Gray);

			// Start timer to track job status (Pending → Running)
			_statusTimer.Start();

			// Save current file state for diff later and undo support
			Dictionary<string, string> beforeContents = GetCurrentFileContents();

			// Create streaming response bubble (will be populated chunk by chunk)
			WebView2 streamingWebView = null;
			base.Dispatcher.Async(delegate
			{
				streamingWebView = CreateStreamingResponseBubble();
			});

			_activeJob = _repositoryUserControl.JobQueue.Add(
				PreferencesLocalization.Current("AI 开发"),
				delegate (JobMonitor monitor)
				{
					try
				{
					OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
					string systemPrompt = BuildSystemPrompt();
					// 任务4：发送前若上下文超长，自动压缩早期对话为摘要，避免 token 超限。
					CompressHistoryIfNeeded(monitor);
					// 多轮对话：携带历史上下文 + 当前需求
					List<JObject> historySnapshot = new List<JObject>(_conversationHistory);
					base.Dispatcher.Async(delegate
					{
						AddStatusMessage(PreferencesLocalization.FormatCurrent("正在请求 AI ({0})...", ForkPlusSettings.Default.AiReviewSelectedModel), Brushes.Gray);
					});

					// 流式输出：onChunk 回调实时追加到 _streamingMarkdown，节流渲染到 WebView2
					ServiceResult<OpenAiResponse> result = aiService.OpenAiRequestStreamingWithRetry(historySnapshot, systemPrompt, requirement, monitor, delegate(string delta)
						{
							if (string.IsNullOrEmpty(delta))
							{
								return;
							}
							lock (_streamingLock)
							{
								if (_streamingMarkdown == null)
								{
									_streamingMarkdown = new StringBuilder();
								}
								_streamingMarkdown.Append(delta);
							}
							base.Dispatcher.Async(delegate
							{
								TryRenderStreamingPreview();
							});
						});
						if (monitor.IsCanceled)
					{
						// 取消可能由 Stop 按钮触发，此时位于后台线程；
						// FinishRequest 会操作 UI 元素，需切回 UI 线程执行。
						base.Dispatcher.Async(delegate { FinishRequest(); });
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

					// 记录本轮对话到历史（user 需求 + assistant 响应），实现多轮记忆
					JObject histUser = new JObject();
					histUser["role"] = "user";
					histUser["content"] = requirement;
					JObject histAssistant = new JObject();
					histAssistant["role"] = "assistant";
					histAssistant["content"] = aiResponse;
					_conversationHistory.Add(histUser);
					_conversationHistory.Add(histAssistant);
					// 超出条数上限时丢弃最早的消息（保留最近 MaxHistoryMessages 条）。
					// 注意：token 级别的压缩由 CompressHistoryIfNeeded 在下次发送前处理，
					// 这里只做条数兜底，防止单条消息极多时列表无限增长。
					while (_conversationHistory.Count > MaxHistoryMessages)
					{
						_conversationHistory.RemoveAt(0);
					}

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
									// 有文件变更：移除流式气泡，显示 diff 结果（含撤销按钮）
									RemoveStreamingResponseBubble(streamingWebView);
									_fileChanges.Clear();
									_fileChanges.AddRange(appliedChanges);
									_lastBeforeContents = beforeContents;
									ShowDiffResults(appliedChanges);
									AddStatusMessage(
										PreferencesLocalization.FormatCurrent("AI modified {0} files", appliedChanges.Count),
										Brushes.Green);
								}
								else
								{
									// 无文件变更：流式气泡即为最终响应，保留
									FinalizeStreamingResponseBubble(streamingWebView);
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
				UpdateQueueIndicator();
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
				StopButton.Visibility = Visibility.Collapsed;
				UpdateQueueIndicator();
				UpdateSendButton();
				InputTextBox.Focus();
			}
		}

		/// <summary>
		/// 停止当前 AI 任务及其后台 HTTP 请求，并清空待处理队列。
		/// 通过 JobMonitor.Cancel() 触发已注册的取消回调（CancellationTokenSource.Cancel），
		/// 中断正在进行的流式 HTTP 请求；OpenAiRequestStreamingWithRetry 的重试循环检测到
		/// IsCanceled 后立即返回 Cancelled 错误，ProcessRequest 随后调用 FinishRequest 收尾。
		/// </summary>
		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			// 先清空待处理队列，避免取消当前后队列中的下一个又被自动启动
			int cleared = _pendingRequests.Count;
			_pendingRequests.Clear();

			Job activeJob = _activeJob;
			if (activeJob != null && activeJob.Monitor != null && !activeJob.Monitor.IsCanceled)
			{
				activeJob.Monitor.Cancel();
				AddStatusMessage(
					PreferencesLocalization.FormatCurrent("⏹ 已停止当前任务" + (cleared > 0 ? "（同时清除 {0} 个排队请求）" : ""), cleared),
					Brushes.OrangeRed);
			}
			else if (cleared > 0)
			{
				AddStatusMessage(
					PreferencesLocalization.FormatCurrent("⏹ 已清除 {0} 个排队请求", cleared),
					Brushes.OrangeRed);
				_isProcessing = false;
				StopButton.Visibility = Visibility.Collapsed;
				ProgressBar.Visibility = Visibility.Collapsed;
				_statusTimer.Stop();
				_activeJob = null;
				UpdateQueueIndicator();
				UpdateSendButton();
			}
		}

		/// <summary>
		/// 任务2：更新队列指示器。当有请求正在处理或在队列中等待时，
		/// 在发送按钮上显示待处理数量，让用户知道新输入的请求已入队。
		/// </summary>
		private void UpdateQueueIndicator()
		{
			int pending = _pendingRequests.Count;
			if (_isProcessing && pending > 0)
			{
				SendButton.Content = PreferencesLocalization.FormatCurrent("Send (queued: {0})", pending);
			}
			else
			{
				bool isEnter = ForkPlusSettings.Default.AiDevSendMode == "Enter";
				SendButton.Content = PreferencesLocalization.Current(isEnter ? "Send (Enter)" : "Send (Ctrl+Enter)");
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

		/// <summary>读取嵌入资源 md-ai-output.css（与 AiCodeReviewWindow 共用样式），带缓存。</summary>
		private static string GetCss()
		{
			if (_cachedCss != null)
			{
				return _cachedCss;
			}
			try
			{
				Assembly executingAssembly = Assembly.GetExecutingAssembly();
				string name = "ForkPlus.Assets.md-ai-output.css";
				using Stream stream = executingAssembly.GetManifestResourceStream(name);
				using StreamReader streamReader = new StreamReader(stream);
				_cachedCss = streamReader.ReadToEnd();
				return _cachedCss;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to read CSS resource", ex);
				return string.Empty;
			}
		}

		/// <summary>调 native Biturbo 库把 Markdown 转为 HTML（与 AiCodeReviewWindow 共用底层 bt_md_to_html）。</summary>
		private static GitCommandResult<string> ConvertMarkdownToHtml(string markdown)
		{
			return BtRequest.Run(() => default(BtMdToHtmlResult), delegate(ref BtMdToHtmlResult x)
			{
				return Bt.bt_md_to_html(markdown, ref x);
			}, delegate(ref BtMdToHtmlResult x)
			{
				return GitCommandResult<string>.Success(x.html.GetUtf8String());
			}, delegate(ref BtMdToHtmlResult x)
			{
				Bt.bt_release_md_to_html(ref x);
			});
		}

		/// <summary>把 Markdown 文本转为完整 HTML 文档（含 CSS），用于 NavigateToString。</summary>
		private static string BuildHtmlDocument(string bodyHtml)
		{
			string css = GetCss();
			return "<!DOCTYPE html>\n<html>\n<head><meta charset='utf-8'><style>" + css + "\n</style></head>\n<body>" + bodyHtml + "\n</body>\n</html>";
		}

		/// <summary>把 Markdown 渲染到 WebView2（转 HTML + NavigateToString），失败时回退为 HTML 转义纯文本。</summary>
		private void RenderMarkdownToWebView(WebView2 webView, string markdown)
		{
			if (webView?.CoreWebView2 == null || string.IsNullOrEmpty(markdown))
			{
				return;
			}
			string body;
			try
			{
				GitCommandResult<string> htmlResult = ConvertMarkdownToHtml(markdown);
				body = htmlResult.Succeeded ? htmlResult.Result : WebUtility.HtmlEncode(markdown);
			}
			catch (Exception ex)
			{
				Log.Warn("AI message markdown render failed: " + ex.Message);
				body = WebUtility.HtmlEncode(markdown);
			}
			try
			{
				webView.NavigateToString(BuildHtmlDocument(body));
			}
			catch (Exception ex)
			{
				Log.Warn("AI message WebView navigate failed: " + ex.Message);
			}
		}

		/// <summary>节流后的实时预览渲染：把当前已收到的 Markdown 转为 HTML 并写入流式 WebView。</summary>
		private void TryRenderStreamingPreview()
		{
			if (_streamingWebView?.CoreWebView2 == null)
			{
				return;
			}
			DateTime now = DateTime.UtcNow;
			if (now - _lastStreamingRenderUtc < TimeSpan.FromMilliseconds(StreamingRenderIntervalMs))
			{
				return;
			}
			_lastStreamingRenderUtc = now;
			string md;
			lock (_streamingLock)
			{
				md = _streamingMarkdown?.ToString() ?? "";
			}
			if (string.IsNullOrEmpty(md))
			{
				return;
			}
			RenderMarkdownToWebView(_streamingWebView, md);
			ScrollToEnd();
		}

		/// <summary>异步初始化 WebView2：创建环境、禁用右键菜单、导航完成后自动测量内容高度并调整控件高度。</summary>
		private async Task InitializeAiMessageWebViewAsync(WebView2 webView)
		{
			try
			{
				await webView.EnsureCoreWebView2Async(await WebView2EnvironmentHelper.GetEnvironmentAsync());
				webView.CoreWebView2.Profile.PreferredColorScheme =
					ForkPlusSettings.Default.Theme != ThemeType.Dark
						? CoreWebView2PreferredColorScheme.Light
						: CoreWebView2PreferredColorScheme.Dark;
				webView.CoreWebView2.ContextMenuRequested += delegate(object s, CoreWebView2ContextMenuRequestedEventArgs e)
				{
					e.Handled = true;
				};
				// 自动高度：导航完成后用 JS 测量内容高度，调整 WebView2 的 Height 使其完整显示
				webView.CoreWebView2.NavigationCompleted += delegate(object s, CoreWebView2NavigationCompletedEventArgs e)
				{
					if (!e.IsSuccess)
					{
						return;
					}
					webView.CoreWebView2.ExecuteScriptAsync("document.documentElement.scrollHeight").ContinueWith(delegate(Task<string> t)
					{
						try
						{
							string result = t.Result;
							if (double.TryParse(result, out double h))
							{
								base.Dispatcher.Async(delegate
								{
									webView.Height = Math.Max(h, 20);
									ScrollToEnd();
								});
							}
						}
						catch
						{
						}
					});
				};
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to init AI message WebView2: " + ex.Message);
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
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
				Foreground = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
				Margin = new Thickness(0, 0, 0, 2)
			};

			TextBox content = new TextBox
			{
				Text = message,
				FontSize = 13,
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
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

		/// <summary>
		/// 创建流式响应气泡，AI 生成内容逐 chunk 追加到 _streamingMarkdown，
		/// 节流渲染到 WebView2（Markdown→HTML），支持代码块/列表/表格/emoji 彩色显示。
		/// </summary>
		private WebView2 CreateStreamingResponseBubble()
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
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
				Foreground = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
				Margin = new Thickness(0, 0, 0, 4)
			};

			WebView2 webView = new WebView2
			{
				MinHeight = 20,
				DefaultBackgroundColor = Colors.Transparent
			};

			StackPanel innerPanel = new StackPanel();
			innerPanel.Children.Add(header);
			innerPanel.Children.Add(webView);
			aiBorder.Child = innerPanel;

			MessagePanel.Children.Add(aiBorder);
			ScrollToEnd();

			// 重置流式状态
			lock (_streamingLock)
			{
				_streamingMarkdown = null;
			}
			_lastStreamingRenderUtc = DateTime.MinValue;
			_streamingWebView = webView;

			// 异步初始化 WebView2（环境/主题/右键菜单/自动高度），fire-and-forget
			_ = InitializeAiMessageWebViewAsync(webView);

			return webView;
		}

		/// <summary>有文件变更时移除流式气泡（改用 diff 展示）。</summary>
		private void RemoveStreamingResponseBubble(WebView2 streamingWebView)
		{
			if (streamingWebView?.Parent is StackPanel panel && panel.Parent is Border border)
			{
				MessagePanel.Children.Remove(border);
			}
			if (_streamingWebView == streamingWebView)
			{
				_streamingWebView = null;
			}
		}

		/// <summary>无文件变更时保留流式气泡作为最终响应，做一次最终渲染确保完整内容显示。</summary>
		private void FinalizeStreamingResponseBubble(WebView2 streamingWebView)
		{
			// 最终渲染（无节流），确保流式结束后完整 Markdown 已渲染
			string md;
			lock (_streamingLock)
			{
				md = _streamingMarkdown?.ToString() ?? "";
			}
			if (!string.IsNullOrEmpty(md) && streamingWebView?.CoreWebView2 != null)
			{
				RenderMarkdownToWebView(streamingWebView, md);
				ScrollToEnd();
			}
			if (_streamingWebView == streamingWebView)
			{
				_streamingWebView = null;
			}
		}

		/// <summary>
		/// 撤销上一次 AI 修改：用 _lastBeforeContents / _fileChanges 回写文件原内容。
		/// </summary>
		private void UndoAiChanges()
		{
			if (_fileChanges == null || _fileChanges.Count == 0)
			{
				AddStatusMessage(PreferencesLocalization.Current("No AI changes to revert"), Brushes.Gray);
				return;
			}
			try
			{
				List<string> allowedDirectories = GetAllowedDirectories();
				foreach (AiFileChange change in _fileChanges)
				{
					string fullPath = System.IO.Path.Combine(_gitModule.Path, change.FilePath);
					string resolvedPath = Path.GetFullPath(fullPath);
					if (!IsPathInAllowedDirectories(resolvedPath, allowedDirectories))
					{
						continue;
					}
					if (change.IsDelete)
					{
						// 恢复被删除的文件
						if (!File.Exists(resolvedPath) && change.OldContent != null)
						{
							string dir = System.IO.Path.GetDirectoryName(resolvedPath);
							if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
							File.WriteAllText(resolvedPath, change.OldContent, Encoding.UTF8);
						}
					}
					else if (change.IsNewFile)
					{
						// 删除新建的文件
						if (File.Exists(resolvedPath)) File.Delete(resolvedPath);
					}
					else
					{
						// 恢复修改前的内容
						if (File.Exists(resolvedPath) && change.OldContent != null)
						{
							File.WriteAllText(resolvedPath, change.OldContent, Encoding.UTF8);
						}
					}
				}
				AddStatusMessage(PreferencesLocalization.Current("AI changes reverted"), Brushes.Green);
				_fileChanges.Clear();
				_lastBeforeContents.Clear();
				RefreshRepositoryStatus();
			}
			catch (Exception ex)
			{
				AddStatusMessage(PreferencesLocalization.FormatCurrent("AI 请求出错: {0}", ex.Message), Brushes.Red);
			}
		}

		private void UndoButton_Click(object sender, RoutedEventArgs e)
		{
			UndoAiChanges();
		}

		/// <summary>清空对话历史和界面消息，重新开始。</summary>
		private void ClearConversation()
		{
			_conversationHistory.Clear();
			_fileChanges.Clear();
			_lastBeforeContents.Clear();
			_streamingWebView = null;
			lock (_streamingLock)
			{
				_streamingMarkdown = null;
			}
			MessagePanel.Children.Clear();
			ShowWelcomeMessage();
			AddStatusMessage(PreferencesLocalization.Current("Conversation cleared."), Brushes.Gray);
		}

		private void ClearConversationButton_Click(object sender, RoutedEventArgs e)
		{
			ClearConversation();
		}

		/// <summary>
		/// 任务4：估算当前对话历史的 token 数（粗略：每 4 个字符约 1 个 token）。
		/// </summary>
		private int EstimateHistoryTokens()
		{
			int totalChars = 0;
			foreach (JObject msg in _conversationHistory)
			{
				string content = msg["content"]?.Value<string>() ?? "";
				totalChars += content.Length;
				// role 字段也占少量 token
				totalChars += (msg["role"]?.Value<string>() ?? "").Length + 4;
			}
			return totalChars / 4;
		}

		/// <summary>
		/// 任务4：若上下文超长，自动压缩早期对话为摘要。
		/// 策略：当估算 token 数超过 MaxContextTokenEstimate 时，保留最近 KeepRecentMessagesOnCompress 条消息，
		/// 将更早的消息通过 AI 生成摘要，替换为单条 system 摘要消息。
		/// 摘要失败时退回到简单截断（丢弃早期消息）。
		/// 此方法在后台线程（Job 内）调用，调用方已持有 monitor。
		/// </summary>
		private void CompressHistoryIfNeeded(JobMonitor monitor)
		{
			if (_isCompressingContext)
			{
				return;
			}
			int estimatedTokens = EstimateHistoryTokens();
			if (estimatedTokens <= MaxContextTokenEstimate)
			{
				return;
			}
			// 历史条数过少时不压缩（避免无意义摘要把仅有的几条消息也吞掉）
			if (_conversationHistory.Count <= KeepRecentMessagesOnCompress + 2)
			{
				return;
			}

			_isCompressingContext = true;
			try
			{
				int splitIndex = _conversationHistory.Count - KeepRecentMessagesOnCompress;
				List<JObject> toSummarize = new List<JObject>();
				for (int i = 0; i < splitIndex; i++)
				{
					toSummarize.Add(_conversationHistory[i]);
				}
				List<JObject> toKeep = new List<JObject>();
				for (int i = splitIndex; i < _conversationHistory.Count; i++)
				{
					toKeep.Add(_conversationHistory[i]);
				}

				// 构造待摘要的对话文本
				StringBuilder convoText = new StringBuilder();
				foreach (JObject msg in toSummarize)
				{
					string role = msg["role"]?.Value<string>() ?? "user";
					string content = msg["content"]?.Value<string>() ?? "";
					// 限制单条长度，避免摘要请求本身过长
					if (content.Length > 2000)
					{
						content = content.Substring(0, 2000) + "...[truncated]";
					}
					convoText.AppendLine("[" + role + "]: " + content);
					convoText.AppendLine("---");
				}

				string summaryPrompt = "Summarize the following conversation between a user and an AI coding assistant in under 300 tokens. "
					+ "Preserve: key file paths mentioned, code changes made (new/modified/deleted files), the user's main requirements, and any important decisions or constraints. "
					+ "Be concise and factual. Do not include code snippets.\n\nConversation:\n" + convoText.ToString();

				base.Dispatcher.Async(delegate
				{
					AddStatusMessage(
						PreferencesLocalization.FormatCurrent("📦 上下文较长 ({0} tokens)，正在自动压缩早期对话...", estimatedTokens),
						Brushes.Gray);
				});

				OpenAiService aiService = OpenAiService.CreateFromAiReviewSettings();
				// 复用流式+重试请求：享受排队/重试机制；onChunk 不需要（我们只取最终结果）
				ServiceResult<OpenAiResponse> summaryResult = aiService.OpenAiRequestStreamingWithRetry(summaryPrompt, monitor, null);

				_conversationHistory.Clear();
				if (summaryResult.Succeeded && !string.IsNullOrWhiteSpace(summaryResult.Result?.Message))
				{
					string summary = summaryResult.Result.Message;
					JObject summaryMsg = new JObject();
					summaryMsg["role"] = "system";
					summaryMsg["content"] = "[Previous conversation summary]: " + summary;
					_conversationHistory.Add(summaryMsg);
					foreach (JObject msg in toKeep)
					{
						_conversationHistory.Add(msg);
					}
					base.Dispatcher.Async(delegate
					{
						AddStatusMessage(
							PreferencesLocalization.FormatCurrent("✅ 上下文已压缩（{0} 条早期对话 → 摘要 + 最近 {1} 条）", toSummarize.Count, toKeep.Count),
							Brushes.Gray);
					});
				}
				else
				{
					// 摘要失败：退回到简单截断，保留最近的消息
					foreach (JObject msg in toKeep)
					{
						_conversationHistory.Add(msg);
					}
					base.Dispatcher.Async(delegate
					{
						AddStatusMessage(
							PreferencesLocalization.Current("⚠️ 上下文过长，已截断早期对话（摘要生成失败）"),
							Brushes.OrangeRed);
					});
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to compress conversation history: " + ex.Message);
			}
			finally
			{
				_isCompressingContext = false;
			}
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
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
				Foreground = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
				Margin = new Thickness(0, 0, 0, 4)
			};

			WebView2 webView = new WebView2
			{
				MinHeight = 20,
				DefaultBackgroundColor = Colors.Transparent
			};

			StackPanel innerPanel = new StackPanel();
			innerPanel.Children.Add(header);
			innerPanel.Children.Add(webView);
			aiBorder.Child = innerPanel;

			MessagePanel.Children.Add(aiBorder);
			ScrollToEnd();

			// 初始化 WebView2 后渲染 Markdown（非流式一次性渲染）
			base.Dispatcher.Async(async delegate
			{
				await InitializeAiMessageWebViewAsync(webView);
				RenderMarkdownToWebView(webView, response);
			});
		}

		private void AddStatusMessage(string message, Brush foreground)
		{
			TextBlock statusBlock = new TextBlock
			{
				Text = message,
				FontSize = 12,
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
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

		/// <summary>
		/// 构建系统提示（固定指令部分，不含用户需求）。
		/// 多轮对话中 system 消息只发一次，用户需求作为独立的 user 消息发送。
		/// </summary>
		private string BuildSystemPrompt()
		{
			string repoPath = _gitModule?.Path ?? "";
			string prompt = $@"You are an AI coding assistant integrated into ForkPlus, a Git client.
Current repository path: {repoPath}

Analyze the user's requirement and generate necessary code changes.
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
Make sure the code compiles and follows the project's existing patterns and conventions.

You have memory of the previous conversation in this session. When the user refers to previous changes or asks follow-up questions, use the conversation context to provide relevant responses.";

			// Append loaded skills
			if (_skillEntries.Count > 0)
			{
				prompt += @"

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
							PreferencesLocalization.FormatCurrent("Security limit: refused to modify file outside directory: {0}", fileChange.FilePath),
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

			// Header row: title + undo button
			DockPanel headerRow = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
			Button undoButton = new Button
			{
				Content = PreferencesLocalization.Current("Undo AI Changes"),
				FontSize = 12,
				Padding = new Thickness(8, 2, 8, 2),
				Margin = new Thickness(8, 0, 0, 0),
				VerticalAlignment = VerticalAlignment.Center
			};
			DockPanel.SetDock(undoButton, Dock.Right);
			undoButton.Click += UndoButton_Click;
			headerRow.Children.Add(undoButton);
			TextBlock diffHeader = new TextBlock
			{
				Text = PreferencesLocalization.FormatCurrent("📝 文件变更 ({0} 个文件)", changes.Count),
				FontSize = 13,
				FontWeight = FontWeights.SemiBold,
				FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
				VerticalAlignment = VerticalAlignment.Center
			};
			headerRow.Children.Add(diffHeader);
			diffs.Children.Add(headerRow);

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
					FontFamily = new FontFamily("Segoe UI, Segoe UI Emoji"),
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
