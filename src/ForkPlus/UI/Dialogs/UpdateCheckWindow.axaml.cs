using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using ForkPlus;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 手动检查更新对话框：打开即开始检测，检测期间显示进度，
	/// 关闭窗口会通过 CancellationToken 立即中止 HTTP 请求。
	/// 检测完成后：有更新→显示 Release Notes + 下载/稍后；无更新→提示已是最新；失败→显示错误。
	/// v4.1.1 起增加"重置此版本"入口（版本损坏时的就地修复）：点击后先确认重置、
	/// 再二次确认是否重置设置，随后按当前版本构造 GitHub 规则直链，复用
	/// AutoUpdater 子进程完成 下载 → 解压 → 等待退出 → 备份替换 → 重启 全链路
	///（交互与 UpdateAvailableWindow 的自动更新流同款：进度面板 + Footer Cancel
	/// 复用为"取消下载"）；确认重置设置时由 updater 在文件替换成功后删除
	/// settings.json（更新失败路径设置原样保留）。
	/// </summary>
	public partial class UpdateCheckWindow : ForkPlusDialogWindow
	{
		/// <summary>检查器（非 readonly：E2E 测试经 CheckerForTests 注入桩，避免真实出网）。</summary>
		private UpdateChecker _checker = new UpdateChecker();

		/// <summary>检查取消令牌（重置中止原检查后会换新实例重新检测，故非 readonly）。</summary>
		private CancellationTokenSource _cts = new CancellationTokenSource();

		private UpdateInfo _result;

		/// <summary>重置流 runner（null = 未在重置）。</summary>
		private AutoUpdateRunner _resetRunner;

		/// <summary>updater 已发出 waiting-exit（此后 Exited 不再恢复 UI，等应用退出）。</summary>
		private bool _waitingForRestart;

		private bool _resetFailed;

		/// <summary>重置流进行中（内容区让位给进度面板，检查结果作废不再回填）。</summary>
		private bool _resetInProgress;

		/// <summary>E2E 测试注入：替换默认 UpdateChecker（避免真实出网与 MarkChecked 落盘）。</summary>
		internal UpdateChecker CheckerForTests
		{
			get => _checker;
			set => _checker = value ?? throw new ArgumentNullException(nameof(value));
		}

		/// <summary>E2E 测试注入：第一次确认（是否重置当前版本）的替代动作。</summary>
		internal Func<bool> ConfirmResetDialogForTests;

		/// <summary>E2E 测试注入：二次确认（是否重置设置）的替代动作。</summary>
		internal Func<bool> ConfirmResetSettingsDialogForTests;

		/// <summary>
		/// E2E 测试注入：重置 runner 工厂（参数 = 是否重置设置；注入临时安装目录/
		/// 假 wait-pid/no-restart），生产为 null（CreateForVersionReset：真实安装目录
		/// 与本进程 pid、自动重启）。
		/// </summary>
		internal Func<bool, AutoUpdateRunner> ResetRunnerFactoryForTests;

		/// <summary>E2E 测试注入：重置包直链解析的替代（生产 = 当前版本+平台的 GitHub 规则地址）。</summary>
		internal Func<string> ResetPackageUrlForTests;

		/// <summary>E2E 测试注入：收到 waiting-exit 时的替代动作（默认真正关闭应用）。</summary>
		internal Action WaitingExitActionForTests;

		public UpdateCheckWindow()
		{
			InitializeComponent();
			DialogTitle = PreferencesLocalization.Current("Check for Updates");
			DialogDescription = PreferencesLocalization.Current("Checking for updates...");
			SubmitButtonTitle = PreferencesLocalization.Current("Download");
			CancelButtonTitle = PreferencesLocalization.Current("Close");
			// 检测完成前隐藏 Submit（下载）按钮
			ShowSubmitButton = false;
			ResetVersionButtonTextBlock.Text = PreferencesLocalization.Current("Reset this version");
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
			if (_resetInProgress)
			{
				return; // 重置流已接管内容区，检查结果作废
			}
			_result = info;
			CheckingPanel.IsVisible = false;
			ResultPanel.IsVisible = true;
			if (info == null || (!string.IsNullOrEmpty(info.ErrorMessage) && info.ErrorMessage != "Cancelled"))
			{
				// 检测失败
				string err = info?.ErrorMessage ?? "Unknown error";
				VersionInfoTextBlock.Text = PreferencesLocalization.FormatCurrent("Update check failed: {0}", err);
				ReleaseNotesLabel.IsVisible = false;
				ReleaseNotesTextBox.IsVisible = false;
				SkipVersionCheckBox.IsVisible = false;
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
			ReleaseNotesLabel.IsVisible = false;
			ReleaseNotesTextBox.IsVisible = false;
			SkipVersionCheckBox.IsVisible = false;
			ShowSubmitButton = false;
		}
	}

		// ===== "重置此版本"流（v4.1.1） =====

		/// <summary>
		/// 重置入口：① 确认重置当前版本（破坏性：重新下载并替换本地安装）→
		/// ② 二次确认是否同时重置设置 → ③ 启动重置流。任一步取消即中止。
		/// </summary>
		private void ResetVersionButton_Click(object sender, RoutedEventArgs e)
		{
			if (_resetInProgress)
			{
				return; // 防重入
			}
			if (!ConfirmResetVersion())
			{
				return;
			}
			bool resetSettings = ConfirmResetSettings();
			StartVersionReset(resetSettings);
		}

		/// <summary>第一次确认：重置当前版本（重新下载替换本地安装）。</summary>
		private bool ConfirmResetVersion()
		{
			if (ConfirmResetDialogForTests != null)
			{
				return ConfirmResetDialogForTests();
			}
			// 显式把自己设为 owner（SetOwnerAndCenter 登记，ShowDialog 优先用真实 owner
			// 而非活动窗口兜底）：确保确认弹窗是"检查更新"窗口的模态子弹窗、居中于其上，
			// 避免行为异常地挂到其他活动窗口/落在别的屏幕。
			return new MessageBoxWindow(
				"Reset this version",
				PreferencesLocalization.FormatCurrent(
					"The current version {0} will be re-downloaded from GitHub and will replace your local installation.",
					App.Version),
				"Reset",
				"Cancel",
				showCancelButton: true, 550.0, showWarningIcon: true)
				.SetOwnerAndCenter(this)
				.ShowDialog().GetValueOrDefault();
		}

		/// <summary>二次确认：是否同时重置设置（重置后恢复默认，不可撤销）。</summary>
		private bool ConfirmResetSettings()
		{
			if (ConfirmResetSettingsDialogForTests != null)
			{
				return ConfirmResetSettingsDialogForTests();
			}
			return new MessageBoxWindow(
				"Reset settings?",
				"Resetting your settings will restore all preferences (repositories, appearance, language, etc.) to their defaults. This cannot be undone.",
				"Reset settings",
				"Keep settings",
				showCancelButton: true, 550.0, showWarningIcon: true)
				.SetOwnerAndCenter(this)
				.ShowDialog().GetValueOrDefault();
		}

		/// <summary>启动重置流：中止在途检查、切换进度面板并启动 updater 子进程。</summary>
		private bool StartVersionReset(bool resetSettings)
		{
			try
			{
				// 当前版本 GitHub 规则直链（releases/download/v{版本}/ForkPlus-{版本}-{平台}.zip）
				string zipUrl = ResetPackageUrlForTests?.Invoke()
					?? AutoUpdatePackageUrl.Resolve(null, App.Version, UpdateChecker.GetCurrentPlatformId());
				if (zipUrl == null)
				{
					SetStatus(ForkPlusDialogStatus.Error, PreferencesLocalization.FormatCurrent(
						"Update failed: {0}", "current version or platform could not be resolved"));
					return false;
				}
				_resetRunner = ResetRunnerFactoryForTests?.Invoke(resetSettings)
					?? AutoUpdateRunner.CreateForVersionReset(resetSettings);
				_resetRunner.Progress += OnResetProgress;
				_resetRunner.Exited += OnResetRunnerExited;
				// 中止在途检查：结果作废，内容区让位给重置进度面板
				CancelPendingCheck();
				_resetInProgress = true;
				_resetFailed = false;
				DownloadPanel.Reset();
				CheckingPanel.IsVisible = false;
				ResultPanel.IsVisible = false;
				ResetVersionButton.IsVisible = false;
				DownloadPanel.IsVisible = true;
				// Footer 语义与 UpdateAvailableWindow 下载期一致：Submit 收起，
				// Cancel 复用为"取消下载"（沿用现有 Footer 按钮，风格统一）
				ShowSubmitButton = false;
				CancelButtonTitle = PreferencesLocalization.Current("Cancel download");
				bool started = _resetRunner.Start(zipUrl);
				if (!started)
				{
					// Start 内部已判定 helper 缺失——恢复 UI（回退提示）
					RestoreCheckUi();
					DisposeResetRunner();
				}
				return started;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to start version reset", ex);
				RestoreCheckUi();
				DisposeResetRunner();
				return false;
			}
		}

		/// <summary>重置进度消息（UI 线程回调）：下载字节/阶段推进/失败/waiting-exit。</summary>
		private void OnResetProgress(AutoUpdateProgress progress)
		{
			if (_waitingForRestart)
			{
				return; // 已进入关闭流程，后续 replacing/restarting 静默忽略
			}
			if (progress.IsError)
			{
				_resetFailed = true;
				// 失败：恢复检查 UI（可重试重置/关闭），错误文案走 Footer 状态区
				RestoreCheckUi();
				SetStatus(ForkPlusDialogStatus.Error, PreferencesLocalization.FormatCurrent(
					"Update failed: {0}", progress.ErrorMessage ?? ""));
				return;
			}
			if (progress.IsWaitingExit)
			{
				_waitingForRestart = true;
				DownloadPanel.SetPhase("waiting-exit");
				// updater 已解压完毕、在等本进程退出——立刻关闭应用交给它替换+重启
				if (WaitingExitActionForTests != null)
				{
					WaitingExitActionForTests();
				}
				else
				{
					(global::Avalonia.Application.Current?.ApplicationLifetime as global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Shutdown();
				}
				return;
			}
			if (progress.Phase != null)
			{
				DownloadPanel.SetPhase(progress.Phase);
				// 解压及之后阶段不可安全中断：收起 Footer Cancel，避免点了没反应
				if (progress.Phase != "downloading")
				{
					ShowCancelButton = false;
				}
				return;
			}
			DownloadPanel.SampleSpeed(progress.ReceivedBytes);
			DownloadPanel.UpdateProgress(progress.ReceivedBytes, progress.TotalBytes);
		}

		/// <summary>
		/// updater 进程退出：用户取消/意外早退时恢复检查 UI（可重试）；失败路径已在
		/// OnResetProgress 恢复过，这里只做清理；waiting-exit 后应用即将关闭不动 UI。
		/// </summary>
		private void OnResetRunnerExited()
		{
			if (_waitingForRestart)
			{
				return; // 应用即将关闭（updater 已接管）
			}
			if (!_resetFailed)
			{
				// 用户取消（或 updater 意外早退）：恢复检查 UI 允许重试或关闭
				RestoreCheckUi();
			}
			DisposeResetRunner();
		}

		/// <summary>
		/// 恢复窗口常态（重置取消/失败/早退后）：检查已完成过则回到结果区，
		/// 否则（检查被重置中止）重新发起检查——窗口回到自己的主职责。
		/// </summary>
		private void RestoreCheckUi()
		{
			_resetInProgress = false;
			DownloadPanel.IsVisible = false;
			ResetVersionButton.IsVisible = true;
			ClearStatus();
			if (_result != null)
			{
				// 检查已完成：恢复结果区（Submit 依检查结果，失败/无更新时收起）
				ResultPanel.IsVisible = true;
				ShowSubmitButton = _result.HasUpdate && string.IsNullOrEmpty(_result.ErrorMessage);
			}
			else
			{
				RestartCheck();
			}
			ShowCancelButton = true;
			CancelButtonTitle = PreferencesLocalization.Current("Close");
		}

		/// <summary>重新发起检查（原 CTS 已 Cancel 不可复用，换新实例）。</summary>
		private void RestartCheck()
		{
			try
			{
				_cts.Cancel();
				_cts.Dispose();
			}
			catch
			{
			}
			_cts = new CancellationTokenSource();
			CheckingPanel.IsVisible = true;
			ResultPanel.IsVisible = false;
			StatusTextBlock.Text = PreferencesLocalization.Current("Checking for updates...");
			ShowSubmitButton = false;
			StartCheck();
		}

		/// <summary>中止在途检查（关窗或重置接管内容区时）。</summary>
		private void CancelPendingCheck()
		{
			try
			{
				_cts.Cancel();
			}
			catch
			{
			}
		}

		private void DisposeResetRunner()
		{
			if (_resetRunner != null)
			{
				_resetRunner.Progress -= OnResetProgress;
				_resetRunner.Exited -= OnResetRunnerExited;
				_resetRunner.Dispose();
				_resetRunner = null;
				_resetInProgress = false;
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
			// 重置下载中：Footer Cancel = 取消下载（Kill updater，恢复 UI 可重试），不关窗
			if (_resetRunner != null && _resetRunner.IsRunning)
			{
				_resetRunner.Cancel();
				return;
			}
			// 关闭窗口：立即取消正在进行的检测
			CancelPendingCheck();
			if (SkipVersionCheckBox.IsChecked == true && _result != null && _result.HasUpdate)
			{
				UpdateChecker.SkipVersion(_result.LatestVersion);
			}
			base.OnCancel();
		}

		protected override void OnClosed(EventArgs e)
		{
			// 关窗即取消：检测中止；重置下载中直接 Kill（已过下载阶段不动作，幂等）
			CancelPendingCheck();
			try
			{
				_cts.Dispose();
			}
			catch
			{
			}
			DisposeResetRunner();
			base.OnClosed(e);
		}
	}
}
