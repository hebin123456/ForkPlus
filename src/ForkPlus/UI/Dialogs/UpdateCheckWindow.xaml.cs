using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;
using ForkPlus;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 手动检查更新对话框：打开即开始检测，检测期间显示进度，
	/// 关闭窗口会通过 CancellationToken 立即中止 HTTP 请求。
	/// 检测完成后：有更新→显示 Release Notes + 下载/稍后；无更新→提示已是最新；失败→显示错误。
	/// </summary>
	public partial class UpdateCheckWindow : ForkPlusDialogWindow
	{
		private readonly UpdateChecker _checker = new UpdateChecker();
		private readonly CancellationTokenSource _cts = new CancellationTokenSource();
		private UpdateInfo _result;

		public UpdateCheckWindow()
		{
			InitializeComponent();
			DialogTitle = PreferencesLocalization.Current("Check for Updates");
			DialogDescription = PreferencesLocalization.Current("Checking for updates...");
			SubmitButtonTitle = PreferencesLocalization.Current("Download");
			CancelButtonTitle = PreferencesLocalization.Current("Close");
			// 检测完成前隐藏 Submit（下载）按钮
			ShowSubmitButton = false;
			Loaded += UpdateCheckWindow_Loaded;
		}

		private void UpdateCheckWindow_Loaded(object sender, RoutedEventArgs e)
		{
			StartCheck();
		}

		private void StartCheck()
		{
			CancellationToken token = _cts.Token;
			Task.Run(delegate
			{
				UpdateInfo info = null;
				try
				{
					info = _checker.CheckLatestRelease(token);
					if (token.IsCancellationRequested)
					{
						return;
					}
					UpdateChecker.MarkChecked();
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (Exception ex)
				{
					info = new UpdateInfo { ErrorMessage = ex.Message };
					Log.Warn("Update check outer exception: " + ex.Message);
				}
				if (token.IsCancellationRequested)
				{
					return;
				}
				Dispatcher.Invoke(new Action(() => OnCheckCompleted(info)));
			}, token);
		}

		private void OnCheckCompleted(UpdateInfo info)
		{
			_result = info;
			CheckingPanel.Visibility = Visibility.Collapsed;
			ResultPanel.Visibility = Visibility.Visible;
			if (info == null || (!string.IsNullOrEmpty(info.ErrorMessage) && info.ErrorMessage != "Cancelled"))
			{
				// 检测失败
				string err = info?.ErrorMessage ?? "Unknown error";
				VersionInfoTextBlock.Text = PreferencesLocalization.FormatCurrent("Update check failed: {0}", err);
				ReleaseNotesLabel.Visibility = Visibility.Collapsed;
				ReleaseNotesTextBox.Visibility = Visibility.Collapsed;
				SkipVersionCheckBox.Visibility = Visibility.Collapsed;
				ShowSubmitButton = false;
				StatusTextBlock.Text = "";
				return;
			}
			if (info.HasUpdate)
			{
				// 有更新
				VersionInfoTextBlock.Text = PreferencesLocalization.FormatCurrent(
					"A new version {0} is available (current: {1}).",
					info.LatestVersion, info.CurrentVersion);
				ReleaseNotesTextBox.Text = string.IsNullOrEmpty(info.ReleaseNotes)
					? info.ReleaseName
					: info.ReleaseNotes;
				ShowSubmitButton = true;
			}
			else
			{
				// 已是最新（附当前版本号）
			VersionInfoTextBlock.Text = PreferencesLocalization.FormatCurrent(
				"You are using the latest version (v{0}).", info.CurrentVersion);
				ReleaseNotesLabel.Visibility = Visibility.Collapsed;
				ReleaseNotesTextBox.Visibility = Visibility.Collapsed;
				SkipVersionCheckBox.Visibility = Visibility.Collapsed;
				ShowSubmitButton = false;
			}
		}

		protected override void OnSubmit()
		{
			try
			{
				if (_result != null && !string.IsNullOrEmpty(_result.DownloadUrl))
				{
					new Uri(_result.DownloadUrl).OpenInBrowser();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open download url", ex);
			}
			if (SkipVersionCheckBox.IsChecked == true && _result != null)
			{
				UpdateChecker.SkipVersion(_result.LatestVersion);
			}
			base.OnSubmit();
		}

		protected override void OnCancel()
		{
			// 关闭窗口：立即取消正在进行的检测
			try
			{
				_cts.Cancel();
			}
			catch
			{
			}
			if (SkipVersionCheckBox.IsChecked == true && _result != null && _result.HasUpdate)
			{
				UpdateChecker.SkipVersion(_result.LatestVersion);
			}
			base.OnCancel();
		}

		protected override void OnClosed(EventArgs e)
		{
			try
			{
				_cts.Cancel();
				_cts.Dispose();
			}
			catch
			{
			}
			base.OnClosed(e);
		}
	}
}
