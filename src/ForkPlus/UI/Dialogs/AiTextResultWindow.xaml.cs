using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Jobs;
using ForkPlus.UI.UserControls.Preferences;
using Microsoft.Web.WebView2.Core;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>通用 AI 文本结果流式显示窗口。功能1（AI 解释 commit）和功能3（AI 生成 PR 描述）共用。
	/// 调用方通过 StartStreaming(title, requestAction) 传入一个"启动 AI 请求并把 chunk 回写到本窗口"的委托。
	/// 流式渲染状态/协议/Markdown转换/CSS 由 AiTextResultWindowViewModel（继承 AiStreamingMarkdownViewModel）承载。
	/// 本类仅负责 WebView2 实例操作 + Dispatcher 调度 + UI 状态切换 + ComboBox 填充。</summary>
	public partial class AiTextResultWindow : CustomWindow, ILocalizableControl
	{
		private readonly AiTextResultWindowViewModel _viewModel = new AiTextResultWindowViewModel();

		// 用户传入的"重试"委托：每次点 Retry 都重新执行一次 AI 请求
		private Action<AiTextResultWindow, JobMonitor> _requestAction;
		private JobMonitor _currentMonitor;

		public AiTextResultWindow()
		{
			InitializeComponent();
			PreferencesLocalization.ApplyCurrent(this);
			Loaded += AiTextResultWindow_Loaded;
		}

		private async void AiTextResultWindow_Loaded(object sender, RoutedEventArgs e)
		{
			InitializeModelComboBox();
			ApplyLocalizationToButtons();
			await InitializeWebView();
			// 首次加载触发一次请求（如果调用方已设置 _requestAction）
			if (_requestAction != null)
			{
				RunRequest();
			}
		}

		/// <summary>v3.0.1：初始化模型下拉框。先用当前选中模型占位，再后台拉取完整列表。
		/// 模型拉取逻辑由 AiModelListLoader 承载（零 WPF），本方法仅负责 ComboBox 填充 + Dispatcher 调度。</summary>
		private void InitializeModelComboBox()
		{
			string currentModel = AiModelListLoader.CurrentModel;
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
				List<string> models = AiModelListLoader.LoadModels();
				if (models == null)
				{
					return;
				}
				Dispatcher.Async(delegate
				{
					try
					{
						if (_viewModel.ModelListLoaded)
						{
							return;
						}
						_viewModel.ModelListLoaded = true;
						string selected = AiModelListLoader.CurrentModel;
						ModelComboBox.Items.Clear();
						foreach (string m in models)
						{
							ModelComboBox.Items.Add(m);
						}
						(int idx, bool shouldInsertCurrent) = AiModelListLoader.FindSelectedIndex(models, selected);
						if (idx >= 0)
						{
							ModelComboBox.SelectedIndex = idx;
						}
						else if (shouldInsertCurrent)
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
						Log.Warn("AiTextResultWindow failed to populate model combo box: " + ex.Message);
					}
				});
			});
		}

		/// <summary>v3.0.1：切换模型时保存到设置。</summary>
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
			AiModelListLoader.CurrentModel = selected;
			StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Model switched to: {0}", selected);
		}

		/// <summary>v3.0.1：应用按钮 ToolTip / Content 的本地化文案。</summary>
		private void ApplyLocalizationToButtons()
		{
			RetryButton.ToolTip = PreferencesLocalization.Current("Retry");
			StopButton.ToolTip = PreferencesLocalization.Current("Stop the current AI task");
			CopyButton.ToolTip = PreferencesLocalization.Current("Copy result to clipboard");
			ModelComboBox.ToolTip = PreferencesLocalization.Current("Select AI model");
		}

		private async Task InitializeWebView()
		{
			try
			{
				await AiResponseWebView.EnsureCoreWebView2Async(await WebView2EnvironmentHelper.GetEnvironmentAsync());
				AiResponseWebView.CoreWebView2.ContextMenuRequested += delegate(object s, CoreWebView2ContextMenuRequestedEventArgs e)
				{
					e.Handled = true;
				};
				AiResponseWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
				AiResponseWebView.CoreWebView2.NavigationCompleted += delegate(object s, CoreWebView2NavigationCompletedEventArgs e)
				{
					if (!e.IsSuccess || !_viewModel.ConsumeScrollToEndRequest())
					{
						return;
					}
					try
					{
						AiResponseWebView.CoreWebView2.ExecuteScriptAsync("window.scrollTo(0, document.documentElement.scrollHeight || document.body.scrollHeight)");
					}
					catch (Exception ex)
					{
						Log.Warn("Streaming scroll-to-end failed: " + ex.Message);
					}
				};
			}
			catch (Exception ex)
			{
				Log.Error("AiTextResultWindow WebView2 init failed", ex);
				ShowError(ex.Message);
			}
		}

		/// <summary>启动一次 AI 请求。调用方在 requestAction 内调用 OnChunk(chunk) 把流式数据写回。</summary>
		public void StartStreaming(string title, Action<AiTextResultWindow, JobMonitor> requestAction)
		{
			TitleTextBlock.Text = title;
			base.Title = title;
			_requestAction = requestAction;
			// 如果窗口已加载，立即启动；否则 Loaded 事件会触发 RunRequest
			if (AiResponseWebView.CoreWebView2 != null)
			{
				RunRequest();
			}
		}

		private void RunRequest()
		{
			if (_requestAction == null)
			{
				return;
			}
			// 重置流式状态（VM 承载）
			_viewModel.ResetForNewRequest();

			StatusTextBlock.Text = PreferencesLocalization.Current("Queued...");
			StatusProgressBar.Visibility = Visibility.Visible;
			BusyIndicator.Show();
			AiResponseWebView.Show();
			AiResponseFallback.Collapse();
			StopButton.Visibility = Visibility.Visible;
			RetryButton.IsEnabled = false;

			_currentMonitor = new JobMonitor();
			_currentMonitor.SetCancellationAction(delegate
			{
				Dispatcher.Async(delegate { _viewModel.StopStreaming(); UpdateStreamingStoppedUi(); });
			});
			// 后台线程执行 AI 请求
			Task.Run(delegate
			{
				try
				{
					_requestAction(this, _currentMonitor);
				}
				catch (Exception ex)
				{
					Log.Error("AiTextResultWindow request action failed", ex);
					Dispatcher.Async(delegate { ShowError(ex.Message); });
				}
			});
		}

		/// <summary>流式 chunk 回调：追加到 VM 缓冲，节流渲染到 WebView。由调用方在 AI 请求的 onChunk 中调用。</summary>
		public void OnChunk(string chunk)
		{
			(bool shouldRender, int lengthSoFar) = _viewModel.OnChunk(chunk);
			if (!shouldRender)
			{
				return;
			}
			Dispatcher.Async(delegate { TryRenderStreamingPreview(lengthSoFar); });
		}

		/// <summary>请求成功完成时调用：渲染最终内容并切到完成态。</summary>
		public void OnSuccess(string finalMarkdown = null)
		{
			Dispatcher.Async(delegate
			{
				_viewModel.StopStreaming();
				UpdateStreamingStoppedUi();
				StatusTextBlock.Text = PreferencesLocalization.Current("Done");
				// 如果调用方给了最终 markdown，覆盖渲染（可能经过修正）
				if (!string.IsNullOrEmpty(finalMarkdown))
				{
					RenderMarkdown(finalMarkdown, scrollToEnd: true);
				}
			});
		}

		/// <summary>请求失败时调用：显示错误。</summary>
		public void OnError(string errorMessage)
		{
			Dispatcher.Async(delegate { ShowError(errorMessage); });
		}

		private void ShowError(string message)
		{
			_viewModel.StopStreaming();
			UpdateStreamingStoppedUi();
			StatusTextBlock.Text = PreferencesLocalization.Current("Failed");
			string html = AiStreamingMarkdownViewModel.BuildErrorHtmlDocument(message);
			try
			{
				AiResponseWebView.NavigateToString(html);
				AiResponseWebView.Show();
			}
			catch (Exception ex)
			{
				Log.Warn("AiTextResultWindow ShowError navigate failed: " + ex.Message);
			}
		}

		/// <summary>流式停止后统一切换 UI 控件（完成/取消/出错共用）。</summary>
		private void UpdateStreamingStoppedUi()
		{
			StopButton.Visibility = Visibility.Collapsed;
			RetryButton.IsEnabled = true;
			StatusProgressBar.Visibility = Visibility.Collapsed;
			BusyIndicator.Collapse();
		}

		private void TryRenderStreamingPreview(int lengthSoFar)
		{
			if (!_viewModel.ShouldRenderNow())
			{
				return;
			}
			if (AiResponseWebView?.CoreWebView2 == null)
			{
				return;
			}
			StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Generating... ({0} chars)", lengthSoFar);
			string md = _viewModel.GetMarkdownSnapshot();
			if (string.IsNullOrEmpty(md))
			{
				return;
			}
			RenderMarkdown(md, scrollToEnd: _viewModel.StreamingUserAtBottom);
		}

		private void RenderMarkdown(string markdown, bool scrollToEnd)
		{
			string html = AiStreamingMarkdownViewModel.RenderMarkdownToHtmlDocumentWithScrollScript(markdown);
			if (scrollToEnd)
			{
				_viewModel.RequestScrollToEndIfNeeded();
			}
			try
			{
				AiResponseWebView.NavigateToString(html);
				AiResponseWebView.Show();
				BusyIndicator.Collapse();
			}
			catch (Exception ex)
			{
				Log.Warn("AiTextResultWindow navigate failed: " + ex.Message);
			}
		}

		private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
		{
			string message = e.TryGetWebMessageAsString();
			bool? atBottom = AiStreamingMarkdownViewModel.TryParseScrollMessage(message);
			if (atBottom.HasValue)
			{
				_viewModel.SetUserAtBottom(atBottom.Value);
			}
		}

		private void RetryButton_Click(object sender, RoutedEventArgs e)
		{
			RunRequest();
		}

		private void StopButton_Click(object sender, RoutedEventArgs e)
		{
			_currentMonitor?.Cancel();
		}

		private void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			string md = _viewModel.GetFinalMarkdown();
			if (string.IsNullOrEmpty(md))
			{
				return;
			}
			try
			{
				Clipboard.SetText(md);
				StatusTextBlock.Text = PreferencesLocalization.Current("Copied to clipboard");
			}
			catch (Exception ex)
			{
				Log.Warn("Copy to clipboard failed: " + ex.Message);
			}
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.ApplyCurrent(this);
			ApplyLocalizationToButtons();
		}
	}
}
