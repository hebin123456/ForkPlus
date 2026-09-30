// 生产路径复现（2026-09-30，"跳转到 Reflog 条目弹窗取消按钮无效/把窗口压到下面"）：
// ReflogWindow 是非模态独立窗口（ToolbarUserControl.ShowReflogWindow 经 Show() 打开），
// JumpToSelected 经 WindowDialogCompat.ShowDialog shim（PushFrame 模态）弹 MessageBoxWindow
// 确认窗。ForkPlusDialogWindow 构造自动 SetOwnerCompat(MainWindow.Instance)，修复前
// 确认窗未显式改挂 ReflogWindow：模态链错挂主窗口，关闭时 Avalonia 重新激活 owner，
// 主窗口跳到最前把 Reflog 窗口压到下面（用户视角"取消按钮把窗口弄到下面去"）。
// 修复后 JumpToSelected 显式 confirmDialog.SetOwnerCompat(this)。本测试按生产时序复现：
// 真仓库 → 真 MainWindow → 非模态 ReflogWindow（ShowAtOwnerScreen）→ 选中条目 →
// 反射调用 JumpToSelected（UI 线程 PushFrame 模态）→ 模态 frame 内点击 Footer Cancel →
// 断言 owner 挂在 ReflogWindow、确认窗关闭、模态调用返回。
// 注意：不能在模态存活期间用 Run/RunJobs 泵队列（会把 PushFrame 泵进当前调用栈造成
// 测试自身死锁——UpdateAvailableWindowModalCloseTests 首次实现实证）；Footer chrome 经
// Dispatcher.Post 延迟装配，点击委托用自重投轮询等 footer 就绪（全程在模态 frame 内执行）。
// 看门狗对 MessageBoxWindow 有 3s 关闭宽限期（HeadlessAppBootstrap），点击须在宽限期内完成。
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ReflogJumpDialogModalCloseTests
	{
		[Fact]
		public void CancelButton_OnJumpToReflogEntryDialog_ClosesDialogAndReturns()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.EnsureStarted();
				ReflogWindow reflogWindow = null;
				MainWindow mainWindow = null;
				var clicked = new ManualResetEventSlim();
				var jumpReturned = new ManualResetEventSlim();
				Exception modalError = null;
				Exception clickError = null;
				bool boxVisibleAfterClick = true;
				bool ownerIsReflogWindow = false;
				string boxDiagnostics = "";

				var t = new Thread(delegate()
				{
					try
					{
						Dispatcher.UIThread.Invoke(delegate
						{
							RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out mainWindow);
							// 生产入口（ToolbarUserControl.ShowReflogWindow）：非模态 + 主窗口 owner
							reflogWindow = new ReflogWindow(repoControl);
							reflogWindow.SetOwnerCompat(mainWindow);
							reflogWindow.ShowAtOwnerScreen();
							Dispatcher.UIThread.RunJobs();
							// 真仓库必有 reflog（初始提交），断言列表装配完成并选中首条
							Assert.NotEmpty(reflogWindow.ReflogListView.ItemsSource as IEnumerable);
							reflogWindow.ReflogListView.SelectedIndex = 0;
							Dispatcher.UIThread.RunJobs();

						// 点击动作投进模态 frame：Footer chrome 延迟装配，自重投轮询直到
						// 确认窗 + Footer 就绪再点（重投上限防死循环；重投路径不置 clicked，
						// 只有真正点击/失败才放行测试线程）
						int attempts = 0;
						Action clickWhenReady = null;
						clickWhenReady = delegate
						{
							MessageBoxWindow box = WpfApp.Windows.OfType<MessageBoxWindow>().FirstOrDefault(w => w.IsVisible);
							ForkPlusDialogFooter footer = box?.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
							if (footer == null)
							{
								if (attempts++ < 500)
								{
									Dispatcher.UIThread.Post(clickWhenReady);
									return;
								}
								clickError = new InvalidOperationException("轮询 500 次未等到确认窗+Footer（box=" + (box == null ? "null" : box.Title) + "）");
								clicked.Set();
								return;
							}
							try
							{
								// 诊断：捕获确认窗标题与当前 owner（断言消息回显，定位错配来源）
								Window ownerWindow = WindowOwnerCompat.TryGetOwner(box);
								boxDiagnostics = "box.Title=" + box.Title
									+ ", box.GetHashCode=" + box.GetHashCode()
									+ ", reflogWindow.GetHashCode=" + reflogWindow.GetHashCode()
									+ ", owner=" + (ownerWindow == null ? "<null>" : ownerWindow.GetType().Name + "#" + ownerWindow.GetHashCode());
								Assert.True(footer.CancelButton.IsVisible, "取消按钮不可见");
								// 修复断言：确认窗模态 owner 应为 ReflogWindow 而非主窗口
								ownerIsReflogWindow = ownerWindow == reflogWindow;
								footer.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
								boxVisibleAfterClick = box.IsVisible;
							}
							catch (Exception e)
							{
								clickError = e;
							}
							finally
							{
								clicked.Set();
							}
						};
							Dispatcher.UIThread.Post(clickWhenReady);

							// 生产路径：双击/按钮 → JumpToSelected（private，反射）→ PushFrame 模态阻塞
							typeof(ReflogWindow).GetMethod("JumpToSelected", BindingFlags.NonPublic | BindingFlags.Instance)
								.Invoke(reflogWindow, null);
						});
					}
					catch (Exception e)
					{
						modalError = e;
					}
					finally
					{
						jumpReturned.Set();
					}
				});
				t.IsBackground = true;
				t.Start();

				try
				{
					Assert.True(clicked.Wait(30000), "点击操作未在模态 frame 内执行" + (modalError != null ? "；modalError=" + modalError : ""));
					Assert.Null(clickError);
					Assert.True(ownerIsReflogWindow, "确认弹窗 owner 应为 ReflogWindow（修复前错误默认主窗口）；诊断: " + boxDiagnostics);
					Assert.False(boxVisibleAfterClick, "点击取消后确认窗未关闭（模态路径 OnCancel 失效）");
					Assert.True(jumpReturned.Wait(30000), "JumpToSelected 模态未随确认窗关闭返回");
					Assert.Null(modalError);
				}
				finally
				{
					Dispatcher.UIThread.Post(delegate
					{
						if (reflogWindow != null && reflogWindow.IsVisible)
						{
							reflogWindow.Close();
						}
						foreach (MessageBoxWindow box in WpfApp.Windows.OfType<MessageBoxWindow>().ToArray())
						{
							if (box.IsVisible)
							{
								box.Close();
							}
						}
						if (mainWindow != null && mainWindow.IsVisible)
						{
							mainWindow.Close();
						}
					});
					jumpReturned.Wait(5000);
				}
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
