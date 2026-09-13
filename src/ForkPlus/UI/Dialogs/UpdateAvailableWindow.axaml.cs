using System;
using ForkPlus;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 启动期"有新版本"弹窗。v4.1.0 起 Download 按钮不再开浏览器：解析当前平台的
	/// zip 直链后经 AutoUpdater 子进程自动下载（进度条 + 随时取消）→ 解压 → 关闭
	/// 本应用 → 替换安装目录 → 自动重启。zip 直链解析不出（资产缺失）或本地缺少
	/// AutoUpdater helper 时回退旧的浏览器打开。
	/// 交互与视觉完全复用现有弹窗组件框架（ForkPlusDialogWindow）：下载期间 Footer
	/// 的 Cancel 按钮语义切为"取消下载"（不关窗、恢复结果区可重试），失败经 Footer
	/// 状态区 SetStatus(Error) 展示，内容区进度面板与 UpdateCheckWindow 的
	/// "检测中"面板同款（3px 细条 + AccentColorBrush + 13 号状态文本）。
	/// </summary>
	public partial class UpdateAvailableWindow : ForkPlusDialogWindow
	{
		private readonly UpdateInfo _updateInfo;

		private AutoUpdateRunner _updateRunner;

		/// <summary>updater 已发出 waiting-exit（此后 Exited 不再恢复 UI，等应用退出）。</summary>
		private bool _waitingForRestart;

		private bool _updateFailed;

		/// <summary>
		/// E2E 测试注入：替换默认 AutoUpdateRunner 工厂（注入临时安装目录/假 wait-pid/
		/// no-restart），生产为 null（用真实安装目录与本进程 pid）。
		/// </summary>
		internal Func<UpdateInfo, AutoUpdateRunner> RunnerFactoryForTests;

		/// <summary>E2E 测试注入：收到 waiting-exit 时的替代动作（默认真正关闭应用）。</summary>
		internal Action WaitingExitActionForTests;

		public UpdateAvailableWindow(UpdateInfo updateInfo)
		{
			InitializeComponent();
			_updateInfo = updateInfo;
			DialogTitle = PreferencesLocalization.Current("Update Available");
			DialogDescription = PreferencesLocalization.FormatCurrent(
				"A new version {0} is available (current: {1}).",
				updateInfo.LatestVersion, updateInfo.CurrentVersion);
			SubmitButtonTitle = PreferencesLocalization.Current("Download");
			CancelButtonTitle = PreferencesLocalization.Current("Later");
			ReleaseNotesTextBox.Text = string.IsNullOrEmpty(updateInfo.ReleaseNotes)
				? updateInfo.ReleaseName
				: updateInfo.ReleaseNotes;
		}

		protected override void OnSubmit()
		{
			if (SkipVersionCheckBox.IsChecked == true)
			{
				UpdateChecker.SkipVersion(_updateInfo.LatestVersion);
			}
			string zipUrl = AutoUpdatePackageUrl.Resolve(_updateInfo);
			if (zipUrl != null && StartAutoUpdate(zipUrl))
			{
				return;
			}
			// 回退：无法自动更新（无 zip 直链或本地缺少 AutoUpdater helper）
			OpenDownloadInBrowser();
			base.OnSubmit();
		}

		protected override void OnCancel()
		{
			// 下载中：Footer Cancel = 取消下载（Kill updater，进程退出后经 Exited 恢复
			// 结果区可重试），不关窗——与现有弹窗"操作进行中 Cancel 中断操作而非关窗"
			// 的语义一致。非下载阶段（解压/替换中）Cancel 按钮已收起，此分支不可达。
			if (_updateRunner != null && _updateRunner.IsRunning)
			{
				_updateRunner.Cancel();
				return;
			}
			if (SkipVersionCheckBox.IsChecked == true)
			{
				UpdateChecker.SkipVersion(_updateInfo.LatestVersion);
			}
			base.OnCancel();
		}

		/// <summary>启动自动更新流：切换到进度面板并启动 updater 子进程。</summary>
		private bool StartAutoUpdate(string zipUrl)
		{
			try
			{
				_updateRunner = RunnerFactoryForTests?.Invoke(_updateInfo) ?? new AutoUpdateRunner();
				_updateRunner.Progress += OnUpdateProgress;
				_updateRunner.Exited += OnUpdateRunnerExited;
				DownloadPanel.Reset();
				ContentPanel.IsVisible = false;
				DownloadPanel.IsVisible = true;
				// 下载期间 Footer 语义：Submit 收起，Cancel 复用为"取消下载"
				//（沿用现有 Footer 按钮组件，不引入内容区内联按钮，保持全弹窗风格一致）
				ShowSubmitButton = false;
				CancelButtonTitle = PreferencesLocalization.Current("Cancel download");
				bool started = _updateRunner.Start(zipUrl);
				if (!started)
				{
					// Start 内部已判定 helper 缺失——恢复 UI 交回退路径
					RestoreContentUi();
					DisposeRunner();
				}
				return started;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to start auto update", ex);
				RestoreContentUi();
				DisposeRunner();
				return false;
			}
		}

		/// <summary>进度消息（UI 线程回调）：下载字节/阶段推进/失败/waiting-exit。</summary>
		private void OnUpdateProgress(AutoUpdateProgress progress)
		{
			if (_waitingForRestart)
			{
				return; // 已进入关闭流程，后续 replacing/restarting 静默忽略
			}
			if (progress.IsError)
			{
				_updateFailed = true;
				// 失败：恢复结果区（可重试 Download/关闭 Later），错误文案走 Footer 状态区
				// SetStatus(Error)——现有弹窗操作失败的标准展示位
				RestoreContentUi();
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
				// 解压及之后阶段不可安全中断（替换进行中/应用即将重启）：收起 Footer
				// Cancel，避免出现点了没反应的"取消下载"按钮
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
		/// updater 进程退出：用户取消/意外早退时恢复结果区（可重试）；失败路径已在
		/// OnUpdateProgress 恢复过，这里只做清理；waiting-exit 后应用即将关闭不动 UI。
		/// </summary>
		private void OnUpdateRunnerExited()
		{
			if (_waitingForRestart)
			{
				return; // 应用即将关闭（updater 已接管）
			}
			if (!_updateFailed)
			{
				// 用户取消（或 updater 意外早退）：恢复结果区允许重试或关闭
				RestoreContentUi();
			}
			DisposeRunner();
		}

		/// <summary>恢复"结果区 + footer"初始形态（取消/失败/早退后）。</summary>
		private void RestoreContentUi()
		{
			DownloadPanel.IsVisible = false;
			ContentPanel.IsVisible = true;
			ShowSubmitButton = true;
			ShowCancelButton = true;
			CancelButtonTitle = PreferencesLocalization.Current("Later");
			ClearStatus();
		}

		private void OpenDownloadInBrowser()
		{
			try
			{
				if (!string.IsNullOrEmpty(_updateInfo.DownloadUrl))
				{
					new Uri(_updateInfo.DownloadUrl).OpenInBrowser();
				}
			}
			catch (Exception ex)
			{
				Log.Error("Failed to open download url", ex);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			// 关窗即取消：下载中直接 Kill；已过下载阶段（不可安全取消）不动作
			DisposeRunner();
			base.OnClosed(e);
		}

		private void DisposeRunner()
		{
			if (_updateRunner != null)
			{
				_updateRunner.Progress -= OnUpdateProgress;
				_updateRunner.Exited -= OnUpdateRunnerExited;
				_updateRunner.Dispose();
				_updateRunner = null;
			}
		}
	}
}
