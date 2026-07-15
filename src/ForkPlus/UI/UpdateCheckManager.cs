using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;

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
			CheckAsync(manual: false);
		}

		/// <summary>手动触发检测（不受节流限制）。</summary>
		public void CheckNow()
		{
			CheckAsync(manual: true);
		}

		private void CheckAsync(bool manual)
		{
			Task.Run(delegate
			{
				UpdateInfo info = _checker.CheckLatestRelease();
				UpdateChecker.MarkChecked();
				if (info.HasUpdate && !UpdateChecker.IsVersionSkipped(info.LatestVersion))
				{
					ShowUpdateAvailable(info);
				}
				else if (manual)
				{
					if (string.IsNullOrEmpty(info.LatestVersion))
					{
						ShowCheckFailed();
					}
					else
					{
						ShowLatestVersion();
					}
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
			instance.Dispatcher.Async(delegate
			{
				new UpdateAvailableWindow(info).ShowDialog();
			});
		}

		private static void ShowLatestVersion()
		{
			MainWindow instance = MainWindow.Instance;
			if (instance == null)
			{
				return;
			}
			instance.Dispatcher.Async(delegate
			{
				new MessageBoxWindow(
					PreferencesLocalization.Current("Update Available"),
					PreferencesLocalization.Current("You are using the latest version."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false).ShowDialog();
			});
		}

		private static void ShowCheckFailed()
		{
			MainWindow instance = MainWindow.Instance;
			if (instance == null)
			{
				return;
			}
			instance.Dispatcher.Async(delegate
			{
				new MessageBoxWindow(
					PreferencesLocalization.Current("Update Available"),
					PreferencesLocalization.Current("Update check failed. Please check your network connection."),
					PreferencesLocalization.Current("OK"),
					showCancelButton: false).ShowDialog();
			});
		}
	}
}
