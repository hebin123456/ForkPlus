using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>通用 AI 文本结果流式显示窗口。功能1（AI 解释 commit）和功能3（AI 生成 PR 描述）共用。
	/// 调用方通过 StartStreaming(title, requestAction) 传入一个"启动 AI 请求并把 chunk 回写到本窗口"的委托。
	/// v3.9.0：继承 AiResultWindowBase，流式渲染委托给 AiStreamingWebView 控件。</summary>
	public partial class AiTextResultWindow : AiResultWindowBase, ILocalizableControl
	{
		// 用户传入的"重试"委托：每次点 Retry 都重新执行一次 AI 请求
		private Action<AiTextResultWindow, JobMonitor> _requestAction;
		private JobMonitor _currentMonitor;

		// v3.9.0：基类 AiResultWindowBase 要求的 UI 元素（XAML 生成的字段）
		protected override ComboBox AiModelComboBox => ModelComboBox;
		protected override Button AiStopButton => StopButton;
		protected override Button AiRetryButton => RetryButton;
		protected override TextBlock AiStatusTextBlock => StatusTextBlock;
		protected override ProgressBar AiStatusProgressBar => StatusProgressBar;

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
			// v3.9.0：流式渲染进度回调 → 更新状态栏字数
			AiStreamingView.StreamingProgress += delegate(int chars)
			{
				StatusTextBlock.Text = PreferencesLocalization.FormatCurrent("Generating... ({0} chars)", chars);
			};
			await AiStreamingView.InitializeAsync();
			// 首次加载触发一次请求（如果调用方已设置 _requestAction）
			if (_requestAction != null)
			{
				RunRequest();
			}
		}

		/// <summary>v3.9.0：模型切换后更新状态栏文字。</summary>
		protected override void OnModelChanged(string selected)
		{
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

		/// <summary>启动一次 AI 请求。调用方在 requestAction 内调用 OnChunk(chunk) 把流式数据写回。</summary>
		public void StartStreaming(string title, Action<AiTextResultWindow, JobMonitor> requestAction)
		{
			TitleTextBlock.Text = title;
			base.Title = title;
			_requestAction = requestAction;
			// 如果窗口已加载，立即启动；否则 Loaded 事件会触发 RunRequest
			if (AiStreamingView.WebView.CoreWebView2 != null)
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
			// v3.9.0：流式状态重置委托给 AiStreamingWebView
			AiStreamingView.StartStreaming();

			StatusTextBlock.Text = PreferencesLocalization.Current("Queued...");
			StatusProgressBar.Visibility = Visibility.Visible;
			AiStreamingView.ShowBusy();
			AiStreamingView.ShowContent();
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

		/// <summary>流式 chunk 回调：委托给 AiStreamingWebView.AppendChunk。由调用方在 AI 请求的 onChunk 中调用。</summary>
		public void OnChunk(string chunk)
		{
			AiStreamingView.AppendChunk(chunk);
		}

		/// <summary>请求成功完成时调用：渲染最终内容并切到完成态。</summary>
		public void OnSuccess(string finalMarkdown = null)
		{
			Dispatcher.Async(delegate
			{
				AiStreamingView.RenderFinal(finalMarkdown);
				StopButton.Visibility = Visibility.Collapsed;
				RetryButton.IsEnabled = true;
				StatusProgressBar.Visibility = Visibility.Collapsed;
				AiStreamingView.HideBusy();
				StatusTextBlock.Text = PreferencesLocalization.Current("Done");
			});
		}

		/// <summary>请求失败时调用：显示错误。</summary>
		public void OnError(string errorMessage)
		{
			Dispatcher.Async(delegate { ShowError(errorMessage); });
		}

		private void ShowError(string message)
		{
			AiStreamingView.StopStreaming();
			AiStreamingView.ShowError(message);
			StopButton.Visibility = Visibility.Collapsed;
			RetryButton.IsEnabled = true;
			StatusProgressBar.Visibility = Visibility.Collapsed;
			AiStreamingView.HideBusy();
			StatusTextBlock.Text = PreferencesLocalization.Current("Failed");
		}

		private void StopStreamingRender()
		{
			AiStreamingView.StopStreaming();
			StopButton.Visibility = Visibility.Collapsed;
			RetryButton.IsEnabled = true;
			StatusProgressBar.Visibility = Visibility.Collapsed;
			AiStreamingView.HideBusy();
			StatusTextBlock.Text = PreferencesLocalization.Current("Canceled");
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
			string md = AiStreamingView.GetMarkdown();
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
