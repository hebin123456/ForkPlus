using System;
using Avalonia.Controls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	/// <summary>
	/// 自动更新下载进度面板：确定性进度条 + 状态文本（已下载/总量/速度）。
	/// 纯展示控件——与 UpdateCheckWindow 的"检测中"面板同款视觉（3px 细条 +
	/// AccentColorBrush + FontSize 13 状态文本）；启动/取消/失败展示等交互统一由
	/// 宿主窗口（UpdateAvailableWindow）经 ForkPlusDialogWindow 的 Footer 按钮
	/// 与 SetStatus 状态区承载，保持与现有弹窗组件风格一致。
	/// </summary>
	public partial class UpdateDownloadPanel : UserControl
	{
		/// <summary>下载速度平滑窗口（EMA：瞬时速度抖动大，UI 每次都跳）。</summary>
		private double _smoothedBytesPerSecond;

		private long _lastReceivedBytes;

		private DateTime _lastSampleAt;

		public UpdateDownloadPanel()
		{
			InitializeComponent();
		}

		/// <summary>重置到"开始下载"初态：进度归零、状态文本归位。</summary>
		public void Reset()
		{
			_smoothedBytesPerSecond = 0.0;
			_lastReceivedBytes = 0L;
			_lastSampleAt = DateTime.UtcNow;
			DownloadProgressBar.IsIndeterminate = false;
			DownloadProgressBar.Minimum = 0.0;
			DownloadProgressBar.Maximum = 100.0;
			DownloadProgressBar.Value = 0.0;
			StatusTextBlock.Text = PreferencesLocalization.Current("Downloading update...");
		}

		/// <summary>
		/// 更新下载进度。total &lt;= 0（服务器无 Content-Length）转不确定进度条，
		/// 只显示已下载量与速度。
		/// </summary>
		public void UpdateProgress(long receivedBytes, long totalBytes)
		{
			if (totalBytes > 0)
			{
				DownloadProgressBar.IsIndeterminate = false;
				double percent = Math.Min(100.0, (double)receivedBytes / (double)totalBytes * 100.0);
				DownloadProgressBar.Value = percent;
				StatusTextBlock.Text = PreferencesLocalization.FormatCurrent(
					"{0} / {1} ({2}/s)",
					FileSizeFormatter.Format(receivedBytes),
					FileSizeFormatter.Format(totalBytes),
					FileSizeFormatter.Format((long)Math.Max(1.0, _smoothedBytesPerSecond)));
			}
			else
			{
				DownloadProgressBar.IsIndeterminate = true;
				StatusTextBlock.Text = PreferencesLocalization.FormatCurrent(
					"{0} / {1} ({2}/s)",
					FileSizeFormatter.Format(receivedBytes),
					"?",
					FileSizeFormatter.Format((long)Math.Max(1.0, _smoothedBytesPerSecond)));
			}
		}

		/// <summary>
		/// 采样速度（EMA 平滑）。由宿主在收到 download 进度消息时同步调用——
		/// 单独于 UpdateProgress 以便宿主控制采样频率（每条管道消息一次即可）。
		/// </summary>
		public void SampleSpeed(long receivedBytes)
		{
			DateTime now = DateTime.UtcNow;
			double elapsedSeconds = (now - _lastSampleAt).TotalSeconds;
			if (elapsedSeconds < 0.15)
			{
				return;
			}
			double instant = (receivedBytes - _lastReceivedBytes) / elapsedSeconds;
			if (instant < 0.0)
			{
				instant = 0.0; // 服务器重定向/重试导致的回退，不当负速度
			}
			_smoothedBytesPerSecond = _smoothedBytesPerSecond == 0.0
				? instant
				: 0.4 * instant + 0.6 * _smoothedBytesPerSecond;
			_lastReceivedBytes = receivedBytes;
			_lastSampleAt = now;
		}

		/// <summary>切换阶段文案（解压/安装/重启阶段无百分比语义，转不确定进度条）。</summary>
		public void SetPhase(string phase)
		{
			StatusTextBlock.Text = PhaseText(phase);
			DownloadProgressBar.IsIndeterminate = true; // 阶段无百分比语义
		}

		/// <summary>阶段名 → 本地化文案。未知阶段显示安装中（安全兜底）。</summary>
		internal static string PhaseText(string phase)
		{
			switch (phase)
			{
				case "downloading":
					return PreferencesLocalization.Current("Downloading update...");
				case "extracting":
					return PreferencesLocalization.Current("Extracting update...");
				case "waiting-exit":
				case "replacing":
					return PreferencesLocalization.Current("Installing update...");
				case "restarting":
				case "done":
					return PreferencesLocalization.Current("Restarting ForkPlus...");
				default:
					return PreferencesLocalization.Current("Installing update...");
			}
		}
	}
}
