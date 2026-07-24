using Avalonia.Controls.Selection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdown.Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ForkPlus.Jobs;
using ForkPlus.Services;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>通用 AI 文本结果流式显示窗口。功能1（AI 解释 commit）和功能3（AI 生成 PR 描述）共用。
	/// 调用方通过 StartStreaming(title, requestAction) 传入一个"启动 AI 请求并把 chunk 回写回本窗口"的委托。
	/// 流式渲染状态/节流协议由 AiTextResultWindowViewModel（继承 AiStreamingMarkdownViewModel）承载。
	/// 本类仅负责 MarkdownScrollViewer 实例操作 + Dispatcher 调度 + UI 状态切换 + ComboBox 填充。
	/// 阶段 4.7-c-3：WebView2 + scroll-at-bottom JS 互操作 → MarkdownScrollViewer + 原生 ScrollViewer.ScrollChanged 事件。</summary>
	public partial class AiTextResultWindow : CustomWindow, ILocalizableControl
	{
		private readonly AiTextResultWindowViewModel _viewModel = new AiTextResultWindowViewModel();

		// MarkdownScrollViewer 内部的 ScrollViewer（用于滚动位置跟踪 + 滚到底部）。
		// 在 Loaded 后通过视觉树查找；MarkdownScrollViewer 内置一个 ScrollViewer。
		private ScrollViewer _innerScrollViewer;

		// 用户传入的"重试"委托：每次点 Retry 都重新执行一次 AI 请求
		private Action<AiTextResultWindow, JobMonitor> _requestAction;
		private JobMonitor _currentMonitor;

		public AiTextResultWindow()
		{
			InitializeComponent();
			PreferencesLocalization.ApplyCurrent(this);
			Loaded += AiTextResultWindow_Loaded;
		}

		private void AiTextResultWindow_Loaded(object sender, RoutedEventArgs e)
		{
			InitializeModelComboBox();
			ApplyLocalizationToButtons();
			AttachScrollTracker();
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

		/// <summary>查找 MarkdownScrollViewer 内部的 ScrollViewer 并订阅 ScrollChanged 事件，
		/// 用于跟踪用户滚动位置（是否在底部）+ 滚到底部操作。
		/// 原 WebView2 通过 JS postMessage 上报 scroll-at-bottom；MarkdownScrollViewer 是原生 Avalonia 控件，
		/// 直接用 ScrollViewer.ScrollChanged 事件即可。MarkdownScrollViewer 内置一个 ScrollViewer，
		/// 通过 GetVisualDescendants 查找（控件模板应用后才有视觉树，故在 Loaded 中调用）。</summary>
		private void AttachScrollTracker()
		{
			try
			{
				_innerScrollViewer = AiResponseWebView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
				if (_innerScrollViewer != null)
				{
					_innerScrollViewer.ScrollChanged += InnerScrollViewer_ScrollChanged;
				}
			}
			catch (Exception ex)
			{
				Log.Warn("AiTextResultWindow scroll tracker attach failed: " + ex.Message);
			}
		}

		/// <summary>ScrollChanged 事件处理：计算用户是否在底部，更新 VM 状态。
		/// 原 WebView2 通过 JS postMessage('scroll-at-bottom:1/0') 上报；这里直接用 Avalonia 原生事件。
		/// 判定：Offset.Y + Viewport.Height >= Extent.Height - 80（容差，与原 JS 脚本一致）。</summary>
		private void InnerScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
		{
			if (_innerScrollViewer == null)
			{
				return;
			}
			double offset = _innerScrollViewer.Offset.Y;
			double viewport = _innerScrollViewer.Viewport.Height;
			double extent = _innerScrollViewer.Extent.Height;
			bool atBottom = viewport <= 0 || (offset + viewport >= extent - 80);
			_viewModel.SetUserAtBottom(atBottom);
		}

		/// <summary>滚动 MarkdownScrollViewer 内部 ScrollViewer 到底。
		/// 原 WebView2 用 ExecuteScriptAsync("window.scrollTo(...)")；这里用原生 ScrollViewer.ScrollToEnd()。</summary>
		private void ScrollInnerViewerToEnd()
		{
			if (_innerScrollViewer != null)
			{
				_innerScrollViewer.ScrollToEnd();
			}
		}

		/// <summary>启动一次 AI 请求。调用方在 requestAction 内调用 OnChunk(chunk) 把流式数据写回。</summary>
		public void StartStreaming(string title, Action<AiTextResultWindow, JobMonitor> requestAction)
		{
			TitleTextBlock.Text = title;
			base.Title = title;
			_requestAction = requestAction;
			// 如果窗口已加载（_innerScrollViewer 已挂载说明 Loaded 已执行），立即启动；否则 Loaded 事件会触发 RunRequest
			if (_innerScrollViewer != null)
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
			// 原 WebView2 用 BuildErrorHtmlDocument 生成红色 HTML；MarkdownScrollViewer 用 Markdown 引用块显示错误
			string escaped = (message ?? "").Replace("\n", "\n> ");
			string errorMarkdown = "> ⚠️ " + escaped;
			try
			{
				AiResponseWebView.Markdown = errorMarkdown;
				AiResponseWebView.Show();
			}
			catch (Exception ex)
			{
				Log.Warn("AiTextResultWindow ShowError render failed: " + ex.Message);
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
			if (AiResponseWebView == null)
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

		/// <summary>把 Markdown 渲染到 MarkdownScrollViewer（直接设置 Markdown 属性）。
		/// 原 WebView2 用 RenderMarkdownToHtmlDocumentWithScrollScript 生成 HTML（含 JS 滚动上报脚本）+ NavigateToString；
		/// 这里直接 Markdown = markdown，滚动位置跟踪由 InnerScrollViewer_ScrollChanged 处理。
		/// scrollToEnd=true 时，Markdown 渲染后延迟一轮布局再 ScrollToEnd（渲染是同步的，但内容高度需布局后才能测得）。</summary>
		private void RenderMarkdown(string markdown, bool scrollToEnd)
		{
			if (scrollToEnd)
			{
				_viewModel.RequestScrollToEndIfNeeded();
			}
			try
			{
				AiResponseWebView.Markdown = markdown;
				AiResponseWebView.Show();
				BusyIndicator.Collapse();
				if (scrollToEnd)
				{
					// Markdown 渲染后需要一轮布局才能测得正确高度，延迟滚动到底
					Dispatcher.Post(ScrollInnerViewerToEnd);
				}
			}
			catch (Exception ex)
			{
				Log.Warn("AiTextResultWindow markdown render failed: " + ex.Message);
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
				// WPF Clipboard.SetText → ServiceLocator.Clipboard.SetText（跨平台剪贴板服务）
				ServiceLocator.Clipboard.SetText(md);
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
