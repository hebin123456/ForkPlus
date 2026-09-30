// 生产路径回归（2026-09-30，"健康窗口删除选中分支无二次确认太危险"）：
// RepositoryHealthUserControl.DeleteSelectedBranches 原先勾选后点"删除选中分支"直接走
// AddUndoable 批量 branch -D，无任何确认。修复后先弹 MessageBoxWindow 确认窗（列出将删除
// 的分支名），确认才继续（其后仍走受保护分支二次确认）。
// 本测试按生产时序复现：真仓库 → 健康窗口装配 → 勾选一个分支 → 反射调用按钮点击 →
// 模态 PushFrame 内自重投轮询找到确认窗并点击 Footer Cancel → 断言：确认窗关闭、模态
// 返回、分支未被删除（列表不变）。确认路径（点删除后真正删除）由 git 命令层测试覆盖，
// 此处只验证取消路径的防误删语义。
// 注意：不能在模态存活期间用 Run/RunJobs 泵队列（PushFrame 泵进当前调用栈会死锁，
// ReflogJumpDialogModalCloseTests 首次实现实证）；Footer chrome 延迟装配，点击委托自重投
// 轮询等就绪（重投路径不置信号，真正点击/失败才放行测试线程）。
using System;
using System.Collections.ObjectModel;
using System.Linq;
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
	public class RepositoryHealthDeleteConfirmTests
	{
		[Fact]
		public void DeleteSelectedBranches_ConfirmDialogCancel_KeepsBranches()
		{
			string repo = TestRepoFactory.CreateClean();
			try
			{
				HeadlessAppBootstrap.EnsureStarted();
				RepositoryHealthWindow healthDialog = null;
				MainWindow mainWindow = null;
				var clicked = new ManualResetEventSlim();
				var deleteReturned = new ManualResetEventSlim();
				Exception modalError = null;
				Exception clickError = null;
				bool boxVisibleAfterClick = true;
				bool boxSeen = false;
				int branchCountAfter = -1;

				var t = new Thread(delegate()
				{
					try
					{
						Dispatcher.UIThread.Invoke(delegate
						{
							RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out mainWindow);
							healthDialog = new RepositoryHealthWindow(repoControl);
							healthDialog.Show();
							Assert.True(UiClick.WaitFor(delegate
							{
								return healthDialog.GetVisualDescendants().OfType<RepositoryHealthUserControl>()
									.FirstOrDefault()?.ContentContainer.IsVisible == true;
							}, 45000), "健康仪表盘应完成装配（45s 超时——共享环境线程池可能被真实仓库刷新积压）");
							RepositoryHealthUserControl health = healthDialog.GetVisualDescendants().OfType<RepositoryHealthUserControl>().First();
							var stale = health.StaleBranchList.ItemsSource as ObservableCollection<RepositoryHealthUserControl.BranchHealthItemViewModel>;
							Assert.NotNull(stale);
							Assert.True(stale.Count >= 1, "CreateClean 应至少有 main 一个本地分支");
							// 勾选第一个分支（main）——生产里用户勾选后点"删除选中分支"
							stale[0].IsChecked = true;

							// 点击动作投进模态 frame：Footer chrome 延迟装配，自重投轮询直到
							// 确认窗 + Footer 就绪再点 Cancel（重投上限防死循环）
							int attempts = 0;
							Action cancelWhenReady = null;
							cancelWhenReady = delegate
							{
								MessageBoxWindow box = WpfApp.Windows.OfType<MessageBoxWindow>().FirstOrDefault(w => w.IsVisible);
								ForkPlusDialogFooter footer = box?.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
								if (footer == null)
								{
									if (attempts++ < 500)
									{
										Dispatcher.UIThread.Post(cancelWhenReady);
										return;
									}
									clickError = new InvalidOperationException("轮询 500 次未等到删除确认窗+Footer");
									clicked.Set();
									return;
								}
								try
								{
									boxSeen = true;
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
							Dispatcher.UIThread.Post(cancelWhenReady);

							// 生产路径：点击"删除选中分支"→ DeleteSelectedBranches → 确认弹窗（PushFrame 模态）
							health.StaleDeleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
							// 模态返回后：取消路径不应删除任何分支
							branchCountAfter = stale.Count;
						});
					}
					catch (Exception e)
					{
						modalError = e;
					}
					finally
					{
						deleteReturned.Set();
					}
				});
				t.IsBackground = true;
				t.Start();

				try
				{
					Assert.True(clicked.Wait(60000), "点击操作未在模态 frame 内执行" + (modalError != null ? "；modalError=" + modalError : ""));
					Assert.Null(clickError);
					Assert.True(boxSeen, "点删除时应弹出二次确认窗（修复前直接删除）");
					Assert.False(boxVisibleAfterClick, "点击取消后确认窗未关闭");
					Assert.True(deleteReturned.Wait(60000), "删除流程未随确认窗关闭返回");
					Assert.Null(modalError);
					Assert.True(branchCountAfter >= 1, "取消路径不应删除分支（列表应保持不变）");
				}
				finally
				{
					Dispatcher.UIThread.Post(delegate
					{
						if (healthDialog != null && healthDialog.IsVisible)
						{
							healthDialog.Close();
						}
						foreach (MessageBoxWindow box in WpfApp.Windows.OfType<MessageBoxWindow>().ToArray())
						{
							if (box.IsVisible)
							{
								box.Close();
							}
						}
						if (mainWindow != null)
						{
							// 铁律（E2eMainWindowHarness）：绝不能 Close() 主窗口——构造内订阅了
							// Closed → lifetime.Shutdown()，会把 headless App 整个关停，殃及同片
							// 后续所有用例（v4.2.1 core-a 实证：此处 Close 后 68 个用例连锁
							// TaskCanceledException）。收尾只关仓库 tab 并摘除窗口。
							E2eMainWindowHarness.CloseRepositoryTab(mainWindow, repo);
						}
					});
					deleteReturned.Wait(5000);
				}
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
