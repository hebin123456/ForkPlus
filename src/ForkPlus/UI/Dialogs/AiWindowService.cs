using System;
using Avalonia.Controls;
using ForkPlus.Accounts.AiServices;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.WpfCompat;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// AI 窗口弹窗行为统一入口（v3.10.3 收口）。此前各 AI 功能各自 new 窗口 + 各自设 owner/任务栏/未配置提示/尺寸，
	/// 行为不一致（有的 SetOwnerCompat(主窗口)、有的不设 owner、有的用 TopLevel、只有 AI 检视出现在任务栏、
	/// 尺寸从 820×640 到 1080×720 各自为政、未配置提示只有 AI 辅助开发带"打开偏好设置"深链）。统一规则：
	/// 1. <see cref="Show"/>：AI 结果窗口一律非模态、owner=主窗口、统一尺寸 1080×720（最小 720×480）、
	///    居中到主窗口所在屏幕（多显示器不乱跳）、主窗口最大化时跟随最大化、显示在任务栏
	///    （非模态窗口被主窗口遮住时可从任务栏找回）。
	/// 2. <see cref="EnsureAiConfigured"/>：所有 AI 功能入口统一未配置拦截——模态提示框带
	///    "打开偏好设置"按钮，点击直达偏好设置的 AI Enhancement 标签。
	/// 覆盖：AI 解释 commit / AI PR 描述 / AI 检视 / AI 辅助开发 / AI 提交编排。
	/// （AI 检视内的建议预览窗口保持模态并跟随检视窗口，不走本入口；其尺寸在 XAML 中与本常量同规格，
	///  以保留 ForkPlusDialogWindow 的 LayoutScaling 缩放机制。）
	/// </summary>
	public static class AiWindowService
	{
		/// <summary>AI 窗口统一默认宽度（DIP）。AiSuggestionPreviewWindow.axaml 中的声明值需与此保持一致。</summary>
		public const double DefaultWidth = 1080.0;

		/// <summary>AI 窗口统一默认高度（DIP）。AiSuggestionPreviewWindow.axaml 中的声明值需与此保持一致。</summary>
		public const double DefaultHeight = 720.0;

		/// <summary>AI 窗口统一最小宽度（DIP）。</summary>
		public const double MinimumWidth = 720.0;

		/// <summary>AI 窗口统一最小高度（DIP）。</summary>
		public const double MinimumHeight = 480.0;

		/// <summary>统一打开 AI 结果窗口：非模态 + 统一尺寸 + owner=主窗口 + 跟随主窗口所在屏幕居中
		/// + 主窗口最大化时跟随最大化 + 显示在任务栏。</summary>
		public static void Show(Window window)
		{
			if (window == null)
			{
				return;
			}
			ApplyWindowMetrics(window);
			window.ShowInTaskbar = true;
			window.SetOwnerCompat(WpfApp.MainWindow);
			// 主窗口最大化时统一跟随最大化（v3.10.3：原仅 AI 辅助开发有此行为，现所有 AI 结果窗口一致）
			if (WpfApp.MainWindow != null
				&& WpfApp.MainWindow.WindowState == global::Avalonia.Controls.WindowState.Maximized)
			{
				window.WindowState = global::Avalonia.Controls.WindowState.Maximized;
			}
			window.ShowAtOwnerScreen();
		}

		/// <summary>应用 AI 窗口统一尺寸：默认 1080×720，最小 720×480。</summary>
		public static void ApplyWindowMetrics(Window window)
		{
			if (window == null)
			{
				return;
			}
			window.MinWidth = MinimumWidth;
			window.MinHeight = MinimumHeight;
			window.Width = DefaultWidth;
			window.Height = DefaultHeight;
		}

		/// <summary>统一的 AI 未配置拦截。已配置返回 true；未配置弹统一提示框（可一键打开偏好设置 AI 页），返回 false。</summary>
		public static bool EnsureAiConfigured()
		{
			if (OpenAiService.IsAiReviewConfigured())
			{
				return true;
			}
			bool openPreferences = new MessageBoxWindow(
				"AI is not configured.",
				"AI is not configured. Please configure AI review settings in Preferences first.",
				"Open Preferences",
				"Close",
				showCancelButton: true,
				width: 560.0,
				showWarningIcon: false).ShowDialog().GetValueOrDefault();
			if (openPreferences)
			{
				OpenAiPreferences();
			}
			return false;
		}

		/// <summary>打开偏好设置窗口并定位到 AI Enhancement 标签。</summary>
		public static void OpenAiPreferences()
		{
			PreferencesWindow preferencesWindow = new PreferencesWindow();
			// 程序集内可访问 internal 字段，直接定位到 AI Enhancement 标签
			try
			{
				preferencesWindow.PreferencesTabControl.SelectedItem = preferencesWindow.AiReviewTabItem;
			}
			catch
			{
			}
			preferencesWindow.ShowDialog();
		}
	}
}
