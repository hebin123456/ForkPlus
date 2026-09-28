// 生产路径复现（2026-09-28，"自动检查更新弹窗稍后按钮无效"）：UpdateAvailableWindow 由
// UpdateCheckManager.ShowUpdateAvailable 在后台线程经 Dispatcher.Invoke → WindowDialogCompat
// .ShowDialog shim（PushFrame 模态）打开。既有 UpdateAvailableWindowTests 全部走非模态
// window.Show()，从未覆盖模态 shim 路径——本测试按生产时序复现：后台线程 Invoke 弹模态
// 窗 → 模态 frame 内点击 Footer Cancel（"稍后"）→ 断言窗口关闭且模态调用返回。
// 注意：不能在模态存活期间用 Run/RunJobs 泵队列（会把 PushFrame 泵进当前调用栈造成
// 测试自身死锁——首次实现实证），点击动作经 Post 投递进模态 frame 内执行。
using System;
using System.Linq;
using System.Threading;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class UpdateAvailableWindowModalCloseTests
	{
		[Fact]
		public void CancelButton_OnModalUpdateAvailableWindow_ClosesWindowAndReturnsFromShowDialog()
		{
			HeadlessAppBootstrap.EnsureStarted();
			var info = new UpdateInfo
			{
				LatestVersion = "9.9.9",
				CurrentVersion = "4.1.0",
				HasUpdate = true,
				ReleaseName = "v9.9.9",
				ReleaseNotes = "release notes for modal close test",
				DownloadUrl = "https://example.com/ForkPlus-9.9.9-test.zip"
			};
			UpdateAvailableWindow window = null;
			var opened = new ManualResetEventSlim();
			var clicked = new ManualResetEventSlim();
			var modalReturned = new ManualResetEventSlim();
			Exception modalError = null;
			Exception clickError = null;
			bool visibleAfterClick = true;
			bool cancelVisibleBeforeClick = false;
			bool cancelEnabledBeforeClick = false;

			// 生产时序：后台线程 Dispatcher.Invoke → UI 线程执行 shim ShowDialog（PushFrame 模态）
			var t = new Thread(delegate()
			{
				try
				{
					Dispatcher.UIThread.Invoke(delegate
					{
						window = new UpdateAvailableWindow(info);
						window.Opened += delegate { opened.Set(); };
						window.ShowDialog();
					});
				}
				catch (Exception e)
				{
					modalError = e;
				}
				finally
				{
					modalReturned.Set();
				}
			});
			t.IsBackground = true;
			t.Start();

			try
			{
				Assert.True(opened.Wait(15000), "模态弹窗未显示");

				// 点击动作投进模态 frame（PushFrame 泵 Default 优先级队列）
				Dispatcher.UIThread.Post(delegate
				{
					try
					{
						ForkPlusDialogFooter footer = window.GetVisualDescendants()
							.OfType<ForkPlusDialogFooter>().FirstOrDefault();
						Assert.NotNull(footer);
						cancelVisibleBeforeClick = footer.CancelButton.IsVisible;
						cancelEnabledBeforeClick = footer.CancelButton.IsEnabled;
						footer.CancelButton.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
						visibleAfterClick = window.IsVisible;
					}
					catch (Exception e)
					{
						clickError = e;
					}
					finally
					{
						clicked.Set();
					}
				});
				Assert.True(clicked.Wait(15000), "点击操作未在模态 frame 内执行");
				Assert.Null(clickError);
				Assert.True(cancelVisibleBeforeClick, "稍后按钮不可见");
				Assert.True(cancelEnabledBeforeClick, "稍后按钮被禁用");

				// 窗口应关闭，且模态 ShowDialog shim 应随窗口关闭返回
				Assert.False(visibleAfterClick, "点击稍后后窗口未关闭（模态路径 OnCancel 失效）");
				Assert.True(modalReturned.Wait(15000), "模态 ShowDialog shim 未随窗口关闭返回");
				Assert.Null(modalError);
			}
			finally
			{
				Dispatcher.UIThread.Post(delegate
				{
					if (window != null && window.IsVisible)
					{
						window.Close();
					}
				});
				modalReturned.Wait(5000);
			}
		}
	}
}
