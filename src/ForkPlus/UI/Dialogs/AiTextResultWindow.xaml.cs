using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Biturbo;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Microsoft.Web.WebView2.Core;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>通用 AI 文本结果流式显示窗口。功能1（AI 解释 commit）和功能3（AI 生成 PR 描述）共用。
	/// 调用方通过 StartStreaming(title, requestAction) 传入一个"启动 AI 请求并把 chunk 回写到本窗口"的委托。</summary>
	public partial class AiTextResultWindow : CustomWindow, ILocalizableControl
	{
		// 流式渲染相关字段（模式参考 AiCodeReviewWindow，精简版）
		private StringBuilder _streamingMarkdown;
		private readonly object _streamingLock = new object();
		private DateTime _lastStreamingRenderUtc = DateTime.MinValue;
		private const int StreamingRenderIntervalMs = 400;
		private bool _streamingActive;
		private bool _pendingStreamingScrollToEnd;
		private bool _streamingUserAtBottom = true;

		// 用户传入的"重试"委托：每次点 Retry 都重新执行一次 AI 请求
		private Action<AiTextResultWindow, JobMonitor> _requestAction;
		private JobMonitor _currentMonitor;
		private static string _cachedCss;

		// v3.0.1：模型下拉框是否已完成后台加载
		private bool _modelListLoaded;

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

		/// <summary>v3.0.1：初始化模型下拉框。先用当前选中模型占位，再后台拉取完整列表。</summary>
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
					Log.Warn("AiTextResultWindow failed to load AI model list: " + ex.Message);
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
			if (string.Equals(selected, ForkPlusSettings.Default.AiReviewSelectedModel, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
			ForkPlusSettings.Default.AiReviewSelectedModel = selected;
			ForkPlusSettings.Default.Save();
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
					if (!e.IsSuccess || !_pendingStreamingScrollToEnd)
					{
						return;
					}
					_pendingStreamingScrollToEnd = false;
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
			// 重置流式状态
			_streamingMarkdown = new StringBuilder();
			_lastStreamingRenderUtc = DateTime.MinValue;
			_streamingActive = true;
			_streamingUserAtBottom = true;
			_pendingStreamingScrollToEnd = false;

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
				Dispatcher.Async(delegate { StopStreamingRender(); });
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

		/// <summary>流式 chunk 回调：追加到缓冲，节流渲染到 WebView。由调用方在 AI 请求的 onChunk 中调用。</summary>
		public void OnChunk(string chunk)
		{
			if (string.IsNullOrEmpty(chunk) || !_streamingActive)
			{
				return;
			}
			lock (_streamingLock)
			{
				_streamingMarkdown?.Append(chunk);
			}
			int lengthSoFar;
			lock (_streamingLock)
			{
				lengthSoFar = _streamingMarkdown?.Length ?? 0;
			}
			Dispatcher.Async(delegate { TryRenderStreamingPreview(lengthSoFar); });
		}

		/// <summary>请求成功完成时调用：渲染最终内容并切到完成态。</summary>
		public void OnSuccess(string finalMarkdown = null)
		{
			Dispatcher.Async(delegate
			{
				_streamingActive = false;
				StopButton.Visibility = Visibility.Collapsed;
				RetryButton.IsEnabled = true;
				StatusProgressBar.Visibility = Visibility.Collapsed;
				BusyIndicator.Collapse();
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
			_streamingActive = false;
			StopButton.Visibility = Visibility.Collapsed;
			RetryButton.IsEnabled = true;
			StatusProgressBar.Visibility = Visibility.Collapsed;
			BusyIndicator.Collapse();
			StatusTextBlock.Text = PreferencesLocalization.Current("Failed");
			string escaped = WebUtility.HtmlEncode(message ?? "");
			string html = "<!DOCTYPE html><html><head><meta charset='utf-8'><style>" + GetCss() + "</style></head><body><p style='color:#d33'>" + escaped + "</p></body></html>";
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

		private void StopStreamingRender()
		{
			_streamingActive = false;
			StopButton.Visibility = Visibility.Collapsed;
			RetryButton.IsEnabled = true;
			StatusProgressBar.Visibility = Visibility.Collapsed;
			BusyIndicator.Collapse();
			StatusTextBlock.Text = PreferencesLocalization.Current("Canceled");
		}

		private void TryRenderStreamingPreview(int lengthSoFar)
		{
			if (!_streamingActive || AiResponseWebView?.CoreWebView2 == null)
			{
				return;
			}
			DateTime now = DateTime.UtcNow;
			if (now - _lastStreamingRenderUtc < TimeSpan.FromMilliseconds(StreamingRenderIntervalMs))
			{
				return;
			}
			_lastStreamingRenderUtc = now;
			StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Generating... ({0} chars)", lengthSoFar);
			string md;
			lock (_streamingLock)
			{
				md = _streamingMarkdown?.ToString() ?? "";
			}
			if (string.IsNullOrEmpty(md))
			{
				return;
			}
			RenderMarkdown(md, scrollToEnd: _streamingUserAtBottom);
		}

		private void RenderMarkdown(string markdown, bool scrollToEnd)
		{
			string body;
			try
			{
				GitCommandResult<string> htmlResult = ConvertMarkdownToHtml(markdown);
				body = htmlResult.Succeeded ? htmlResult.Result : WebUtility.HtmlEncode(markdown);
			}
			catch (Exception ex)
			{
				Log.Warn("AiTextResultWindow markdown render failed: " + ex.Message);
				body = WebUtility.HtmlEncode(markdown);
			}
			string scrollScript = "<script>(function(){function s(){var st=document.documentElement.scrollTop||document.body.scrollTop;var sh=document.documentElement.scrollHeight||document.body.scrollHeight;var ch=document.documentElement.clientHeight;var at=ch<=0||(st+ch>=sh-80);window.chrome.webview.postMessage('scroll-at-bottom:'+(at?'1':'0'));}window.addEventListener('scroll',s,{passive:true});window.addEventListener('load',s);if(document.readyState==='complete'||document.readyState==='interactive'){s();}})();</script>";
			if (scrollToEnd)
			{
				_pendingStreamingScrollToEnd = true;
			}
			string html = "<!DOCTYPE html>\n<html>\n<head><meta charset='utf-8'><style>" + GetCss() + "\n</style></head>\n<body>" + body + "\n" + scrollScript + "\n</body>\n</html>";
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
			if (message != null && message.StartsWith("scroll-at-bottom:", StringComparison.Ordinal))
			{
				string value = message.Substring("scroll-at-bottom:".Length);
				_streamingUserAtBottom = value == "1";
			}
		}

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
			string md;
			lock (_streamingLock)
			{
				md = _streamingMarkdown?.ToString() ?? "";
			}
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
