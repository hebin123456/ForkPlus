// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Threading → using Avalonia.Threading
// - DispatcherTimer → Avalonia.Threading.DispatcherTimer（API 兼容：Interval/Tick/Start）
// - instance.Dispatcher.Invoke(Action) 保持（Avalonia.Threading.Dispatcher.Invoke 兼容，参考 QuickPullCommand/SaveFileCommand）
using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI
{
	/// <summary>
	/// 后台定时检测更新：启动后延迟 30 秒首次自检，之后每小时 tick 一次，
	/// tick 时依据 <see cref="UpdateChecker.ShouldAutoCheck"/> 决定是否实际发起请求。
	/// 自动检测失败静默；手动检测失败给出提示。
	/// </summary>
	internal class UpdateCheckManager
	{
		private static readonly TimeSpan StartCheckInterval = TimeSpan.FromSeconds(30.0);

		private static readonly TimeSpan RecurringCheckInterval = TimeSpan.FromHours(1.0);

		private readonly DispatcherTimer _timer = new DispatcherTimer();

		private readonly UpdateChecker _checker = new UpdateChecker();

		private bool _firstCheckDone;

		public UpdateCheckManager()
		{
			_timer.Interval = StartCheckInterval;
			_timer.Tick += Timer_Tick;
		}

		public void Start()
		{
			_timer.Start();
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			// 首次 tick 后切换到周期间隔
			if (!_firstCheckDone)
			{
				_firstCheckDone = true;
				_timer.Interval = RecurringCheckInterval;
			}
			if (!UpdateChecker.ShouldAutoCheck())
			{
				return;
			}
			CheckAsync();
		}

		/// <summary>手动触发检测：立即弹出检查窗口，窗内执行检测，关窗即停止检测。</summary>
		public void CheckNow()
		{
			MainWindow instance = MainWindow.Instance;
			if (instance == null)
			{
				return;
			}
			try
			{
				instance.Dispatcher.Invoke(delegate
				{
					new UpdateCheckWindow().ShowDialog();
				});
			}
			catch (Exception ex)
			{
				Log.Warn("CheckNow failed: " + ex.Message);
			}
		}

		private void CheckAsync()
		{
			Task.Run(delegate
			{
				UpdateInfo info = null;
				try
				{
					info = _checker.CheckLatestRelease();
					UpdateChecker.MarkChecked();
				}
				catch (Exception ex)
				{
					info = new UpdateInfo { ErrorMessage = ex.Message };
					Log.Warn("Update check outer exception: " + ex.Message);
				}
				// 自动检测：仅在有更新且未被跳过时提示，失败静默
				if (info != null && info.HasUpdate && !UpdateChecker.IsVersionSkipped(info.LatestVersion))
				{
					ShowUpdateAvailable(info);
				}
			});
		}

		private static void ShowUpdateAvailable(UpdateInfo info)
		{
			MainWindow instance = MainWindow.Instance;
			if (instance == null)
			{
				return;
			}
			try
			{
				instance.Dispatcher.Invoke(delegate
				{
					new UpdateAvailableWindow(info).ShowDialog();
				});
			}
			catch (Exception ex)
			{
				Log.Warn("ShowUpdateAvailable failed: " + ex.Message);
			}
		}
	}
}
