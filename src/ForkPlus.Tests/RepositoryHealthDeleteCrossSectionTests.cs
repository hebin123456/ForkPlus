// 生产路径回归（2026-09-30，"健康窗口一个分区删掉了分支，另一个分区还在"）：
// DeleteSelectedBranches 删除成功后原先只从当前分区集合移除 VM；陈旧区含全部本地分支，
// 与已合并/分叉天然重叠，同名分支删掉后其他分区不联动。修复后 RefreshAfterBranchDeletion
// 按 FullReference 从三区集合同步移除并刷新关注项计数/空态/删除按钮可见性。
// 本测试按生产时序复现：真仓库建 feature 分支（与 main 同提交 → 已合并区可见 + 陈旧区含
// 全部分支）→ 勾选 → 点已合并区"删除选中分支"→ 模态 PushFrame 内自重投轮询找到确认窗
// 点 Footer Submit（无受保护分支，ProtectedBranchConfirmWindow.Confirm 直接放行）→
// AddUndoable 后台 job 真删（git branch -D feature）→ 断言：git 层分支确已删除，且
// 已合并区与陈旧区的列表都不再含 feature（跨分区联动）。
// 注意：不能在模态存活期间用 Run/RunJobs 泵队列（PushFrame 泵进当前调用栈会死锁）；
// 模态返回后才用 UiClick.WaitFor 时间循环等 JobQueue 后台 job 与回投刷新落地。
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
	public class RepositoryHealthDeleteCrossSectionTests
	{
		[Fact]
		public void DeleteSelectedBranches_RemovedFromAllSections()
		{
			string repo = TestRepoFactory.CreateClean();
			try
			{
				// feature 与 main 同提交：已合并区（merged into HEAD、非当前分支）必含；
				// 陈旧区含全部本地分支。两区重叠即用户报告的场景。
				TestRepoFactory.GitOutput(repo, "branch feature");
				HeadlessAppBootstrap.EnsureStarted();
				RepositoryHealthWindow healthDialog = null;
				MainWindow mainWindow = null;
				var clicked = new ManualResetEventSlim();
				var deleteReturned = new ManualResetEventSlim();
				Exception modalError = null;
				Exception clickError = null;
				bool boxSeen = false;
				bool goneFromMerged = false;
				bool goneFromStale = false;

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
							var merged = health.MergedBranchList.ItemsSource as ObservableCollection<RepositoryHealthUserControl.BranchHealthItemViewModel>;
							var stale = health.StaleBranchList.ItemsSource as ObservableCollection<RepositoryHealthUserControl.BranchHealthItemViewModel>;
							Assert.NotNull(merged);
							Assert.NotNull(stale);
							var mergedFeature = merged.FirstOrDefault((RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "feature");
							Assert.NotNull(mergedFeature);
							Assert.True(stale.Any((RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "feature"),
								"陈旧区含全部本地分支，应与已合并区重叠");
							mergedFeature.IsChecked = true;

							// 确认窗就绪后点 Footer Submit（真正删除）；无受保护分支不再有后续弹窗
							int attempts = 0;
							Action confirmWhenReady = null;
							confirmWhenReady = delegate
							{
								MessageBoxWindow box = WpfApp.Windows.OfType<MessageBoxWindow>().FirstOrDefault(w => w.IsVisible);
								ForkPlusDialogFooter footer = box?.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
								if (footer == null)
								{
									if (attempts++ < 500)
									{
										Dispatcher.UIThread.Post(confirmWhenReady);
										return;
									}
									clickError = new InvalidOperationException("轮询 500 次未等到删除确认窗+Footer");
									clicked.Set();
									return;
								}
								try
								{
									boxSeen = true;
									footer.SubmitButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
							Dispatcher.UIThread.Post(confirmWhenReady);

							// 生产路径：点已合并区"删除选中分支" → 确认 → AddUndoable 后台真删 → 回投三区联动刷新
							health.MergedDeleteButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

							// 模态已返回（job 在 JobQueue 后台跑），时间循环等回投刷新落地
							goneFromMerged = UiClick.WaitFor(delegate
							{
								return !merged.Any((RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "feature");
							}, 45000);
							goneFromStale = UiClick.WaitFor(delegate
							{
								return !stale.Any((RepositoryHealthUserControl.BranchHealthItemViewModel x) => x.Name == "feature");
							}, 45000);
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
					Assert.True(boxSeen, "点删除时应弹出二次确认窗");
					Assert.True(deleteReturned.Wait(120000), "删除+联动刷新流程未在超时内返回");
					Assert.Null(modalError);
					Assert.True(goneFromMerged, "删除成功后已合并区应移除该分支");
					Assert.True(goneFromStale, "删除成功后陈旧区应联动移除该分支（跨分区不再残留）");
					// git 层实证：分支确实被删除（排除"仅 UI 移除、实际没删"的假刷新）
					Assert.Equal("", TestRepoFactory.GitOutput(repo, "branch --list feature").Trim());
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
						if (mainWindow != null && mainWindow.IsVisible)
						{
							mainWindow.Close();
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
