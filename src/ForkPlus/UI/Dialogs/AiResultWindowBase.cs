using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Utils.Http;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// v3.9.0：AI 结果窗口基类。封装 ModelComboBox 初始化/切换等共享逻辑。
	/// AiTextResultWindow / AiCodeReviewWindow / AiCommitComposerWindow 继承此类。
	/// CSS 读取和 Markdown→HTML 转换委托给 AiStreamingWebView 静态方法，与 AiDevelopmentWindow 共用。
	/// </summary>
	public abstract class AiResultWindowBase : CustomWindow
	{
		// ── 子类通过 XAML 生成的字段实现这些属性 ──

		protected abstract ComboBox AiModelComboBox { get; }
		protected abstract Button AiStopButton { get; }
		protected abstract Button AiRetryButton { get; }
		protected abstract TextBlock AiStatusTextBlock { get; }
		protected abstract ProgressBar AiStatusProgressBar { get; }

		// ── ModelComboBox 共享逻辑（v3.10.3 委托 AiModelComboBoxHelper，与 AiDevelopmentWindow 共用） ──

		/// <summary>初始化模型下拉框。先用当前选中模型占位，再后台拉取完整列表。</summary>
		protected void InitializeModelComboBox()
		{
			AiModelComboBoxHelper.Initialize(AiModelComboBox, Dispatcher);
		}

		/// <summary>切换模型时保存到设置。子类可 override OnModelChanged 做额外反馈。</summary>
		protected void ModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			AiModelComboBoxHelper.HandleSelectionChanged(AiModelComboBox, OnModelChanged);
		}

		/// <summary>模型切换后的回调，子类可 override 做额外反馈（如更新状态栏文字）。</summary>
		protected virtual void OnModelChanged(string selected)
		{
		}

		// ── CSS / Markdown 共享逻辑（委托给 AiStreamingWebView 静态方法，v3.9.0 统一收口） ──

		/// <summary>读取 md-ai-output.css 嵌入资源（委托 AiStreamingWebView.GetCss）。</summary>
		protected static string GetCss()
		{
			return AiStreamingWebView.GetCss();
		}

		/// <summary>Markdown → HTML（委托 AiStreamingWebView.ConvertMarkdownToHtml）。</summary>
		protected static GitCommandResult<string> ConvertMarkdownToHtml(string markdown)
		{
			return AiStreamingWebView.ConvertMarkdownToHtml(markdown);
		}
	}
}
