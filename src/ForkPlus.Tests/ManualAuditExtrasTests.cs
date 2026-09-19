// 用户手册截图测试(审计补写, 2026-09-19):为已存在但手册未覆盖的 5 个功能补写中文界面截图。
//   1) 二分查找 Bisect(ch-11):含已知首坏提交的仓库 → 通知条 Good/Bad/Skip/Abort 交互。
//   2) git mm 子仓标签拖拽重排(ch-18):TestRepoFactory.CreateGitMmWorkspace + 真实拖拽手势。
//   3) 快速启动窗口 QuickLaunch(ch-02 文档):主窗口打开 QuickLaunchWindow 显示 命令/仓库/分支导航。
//   4) 文件树导出与外部树比对(ch-04):多选两个修订右键 → "Compare File Trees in {工具}"。
//   5) git mm Upload 弹窗"允许空提交"(--honor-no-changes)(ch-18)。
// 复用 ManualScreenshotHelper.Snap 与 HeadlessAppBootstrap/E2eMainWindowHarness/UiClick/TestRepoFactory
// 既有基建;截图落盘 docs/manual/screenshots/<模块目录>/。
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.QuickLaunch;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ManualAuditExtrasTests
	{
		// ============================ 共享助手 ============================

		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
		}

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static string RunGit(string args, string cwd)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = cwd
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception("git " + args + " 失败: " + err);
			}
			return p.StandardOutput.ReadToEnd();
		}

		/// <summary>通知条 0.7s 展开动画依赖 headless 手动渲染时钟,每轮轮询补帧驱动收敛。</summary>
		private static bool WaitForHeightAnimated(NotificationBarUserControl bar, double target, int timeoutMs)
		{
			return UiClick.WaitFor(delegate
			{
				AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
				return bar.RootBorder.Height == target;
			}, timeoutMs);
		}

		/// <summary>等仓库 JobQueue 静默(上一命令及其触发的刷新全部落定),消并发窗口。</summary>
		private static void WaitForRepositoryPipelineIdle(RepositoryUserControl repoControl, string what)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				if (!repoControl.JobQueue.IsIdle)
				{
					return false;
				}
				Dispatcher.UIThread.RunJobs();
				return repoControl.JobQueue.IsIdle;
			}, 30000), what + " 前仓库任务队列应静默(JobQueue.IsIdle)");
		}

		// ============================ 1) 二分查找 Bisect (ch-11) ============================

		private static string CreateBisectRepoWithKnownBadCommit()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpmanualbisect_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			RunGit("init -q -b main", root);
			RunGit("config user.email test@example.com", root);
			RunGit("config user.name Test", root);
			File.WriteAllText(Path.Combine(root, ".gitignore"), "*.tmp\n");
			RunGit("add .gitignore", root);
			RunGit("commit -q -m c0", root);
			for (int i = 1; i <= 8; i++)
			{
				File.WriteAllText(Path.Combine(root, "f.txt"), (i >= 5 ? "BUG\n" : "") + "content v" + i + "\n");
				RunGit("add f.txt", root);
				RunGit("commit -q -m c" + i, root);
			}
			return root;
		}

		[Fact]
		public void Ch11_BisectNotificationBar()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repoRoot = CreateBisectRepoWithKnownBadCommit();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repoRoot, out var window);
					try
					{
						var bar = window.GetVisualDescendants().OfType<NotificationBarUserControl>().First();
						Assert.True(bar != null, "仓库视图中应存在通知条控件");
						WaitForRepositoryPipelineIdle(repoControl, "bisect start");
						RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Start);
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryStatus?.RepositoryState is RepositoryState.BisectInProgress;
						}, 30000), "bisect start 后仓库状态应刷新为 BisectInProgress");
						Assert.True(WaitForHeightAnimated(bar, 28.0, 30000), "bisect start 后通知条应展开(高度 28)");
						Assert.True(bar.NotificationTextBlock.IsVisible, "通知条文本应可见");
						Assert.True(bar.Button1.IsVisible && bar.Button2.IsVisible && bar.Button3.IsVisible
							&& bar.AbortButton.IsVisible, "Good/Bad/Skip/Abort 四键应可见");
						ManualScreenshotHelper.Snap(window, "09-bisect-notification-bar", "11-history-rewrite");

						// 收敛收尾,防止遗留 BISECT_START 污染后续测试
						WaitForRepositoryPipelineIdle(repoControl, "bisect reset");
						RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Reset);
						Assert.True(UiClick.WaitFor(delegate
						{
							return !File.Exists(Path.Combine(repoRoot, ".git", "BISECT_START"));
						}, 30000), "bisect reset 应清除 BISECT_START");
						Assert.True(WaitForHeightAnimated(bar, 0.0, 30000), "reset 后通知条应收起");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repoRoot);
					}
				});
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}

		// ============================ 2) git mm 子仓标签拖拽重排 (ch-18) ============================

		private sealed class DragGesture
		{
			private readonly Pointer _pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
			private readonly ulong _startTimestamp = (ulong)Environment.TickCount64;
			private int _step;

			internal void Press(InputElement source, Window window, Point position)
			{
				source.RaiseEvent(new PointerPressedEventArgs(
					source, _pointer, window, position, _startTimestamp + (ulong)(uint)_step++,
					new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
					KeyModifiers.None));
			}

			internal void Move(InputElement source, Window window, Point position)
			{
				source.RaiseEvent(new PointerEventArgs(
					InputElement.PointerMovedEvent, source, _pointer, window, position,
					_startTimestamp + (ulong)(uint)_step++,
					new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
					KeyModifiers.None));
			}

			internal void Release(InputElement source, Window window, Point position)
			{
				source.RaiseEvent(new PointerReleasedEventArgs(
					source, _pointer, window, position, _startTimestamp + (ulong)(uint)_step++,
					new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
					KeyModifiers.None, MouseButton.Left));
			}
		}

		private static void PerformDrag(InputElement source, Window window, Point pressPosition, Point dropPosition)
		{
			Point midPosition = new Point(
				pressPosition.X + (dropPosition.X - pressPosition.X) / 3.0,
				pressPosition.Y + (dropPosition.Y - pressPosition.Y) / 3.0);
			var gesture = new DragGesture();
			gesture.Press(source, window, pressPosition);
			gesture.Move(source, window, midPosition);
			gesture.Move(source, window, dropPosition);
			gesture.Release(source, window, dropPosition);
			Dispatcher.UIThread.RunJobs();
		}

		private static Point? PointIn(Visual visual, Window window, double ratioX, double ratioY)
		{
			if (visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)
			{
				return null;
			}
			return visual.TranslatePoint(new Point(visual.Bounds.Width * ratioX, visual.Bounds.Height * ratioY), window);
		}

		[Fact]
		public void Ch18_GitMmSubrepoTabDragReorder()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string ws = TestRepoFactory.CreateGitMmWorkspace();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// 沙箱无 git-mm CLI:打开工作区会弹 missing 警告(看门狗自动关闭并记录)
					HeadlessAppBootstrap.ExpectErrorDialogs();
					E2eMainWindowHarness.OpenTab(ws, out var window);
					try
					{
						GitMmUserControl gitMm = window.GetVisualDescendants().OfType<GitMmUserControl>().FirstOrDefault();
						Assert.True(gitMm != null, "应创建 GitMmUserControl(GitMm 模式 tab)");

						TabControl subrepoTabs = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							subrepoTabs = gitMm.GetVisualDescendants().OfType<TabControl>()
								.FirstOrDefault(tc => tc.Name == "SubreposTabControl");
							return subrepoTabs != null && subrepoTabs.Items.OfType<TabItem>().Count() == 2;
						}), "子仓 tab 应装配为 2 个(后台扫描路径)");
						TabItem tabA = subrepoTabs.Items.OfType<TabItem>().ElementAt(0);
						TabItem tabB = subrepoTabs.Items.OfType<TabItem>().ElementAt(1);
						Assert.Equal("repoA", (tabA.Tag as GitMmSubrepoItem)?.Name);
						Assert.Equal("repoB", (tabB.Tag as GitMmSubrepoItem)?.Name);

						Assert.True(UiClick.WaitFor(delegate
						{
							return tabA.Bounds.Width > 0 && tabB.Bounds.Width > 0;
						}), "子仓 tab 应完成布局");
						System.Threading.Tasks.Task.Delay(400).GetAwaiter().GetResult();
						Dispatcher.UIThread.RunJobs();
						ManualScreenshotHelper.Snap(window, "07-gitmm-subrepo-tabs", "18-gitmm");

						Point? pressAt = PointIn(tabA, window, 0.5, 0.5);
						Point? dropAt = PointIn(tabB, window, 0.75, 0.5);
						Assert.True(pressAt.HasValue && dropAt.HasValue, "子仓 tab 布局坐标应可换算");
						PerformDrag(tabA, window, pressAt.Value, dropAt.Value);

						string[] order = subrepoTabs.Items.OfType<TabItem>()
							.Select(t => (t.Tag as GitMmSubrepoItem)?.Name)
							.ToArray();
						Assert.True(order.Length == 2 && order[0] == "repoB" && order[1] == "repoA",
							"拖拽后子仓 tab 应换位为 [repoB, repoA](实际:[" + string.Join(", ", order) + "])");
						Assert.True(tabA.IsSelected, "拖拽完成后被拖子仓 tab 应被选中");
						ManualScreenshotHelper.Snap(window, "08-gitmm-subrepo-tabs-reordered", "18-gitmm");

						HeadlessAppBootstrap.TakeCapturedErrorDialogs();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, ws);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(ws);
			}
		}

		// ============================ 3) 快速启动窗口 QuickLaunch (ch-02) ============================

		[Fact]
		public void Ch02_QuickLaunchWindow()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						Assert.True(UiClick.WaitFor(delegate { return repoControl.RepositoryData != null; }),
							"RepositoryData 未装配");
						QuickLaunchWindow quickLaunch = new QuickLaunchWindow();
						quickLaunch.Show();
						Dispatcher.UIThread.RunJobs(); // Loaded → RefreshCommandList

						ListBox listBox = UiClick.Find<ListBox>(quickLaunch, "RepositoriesListBox");
						Assert.True(UiClick.WaitFor(delegate
						{
							return listBox.ItemsSource is CommandProviderItem[] { Length: > 0 };
						}), "快速启动窗口应装配命令/仓库/分支导航列表");
						ManualScreenshotHelper.Snap(quickLaunch, "04-quick-launch", "02-main-window");
						quickLaunch.Close();
					}
					finally
					{
						try
						{
							QuickLaunchWindow leftover = WpfApp.Windows.OfType<QuickLaunchWindow>().FirstOrDefault();
							leftover?.Close();
							Dispatcher.UIThread.RunJobs();
						}
						catch { }
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 4) 文件树导出与外部树比对 (ch-04) ============================

		[Fact]
		public void Ch04_FileTreeCompareContextMenu()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			ForkPlus.Settings.ExternalTool[] prevDiff = ForkPlusSettings.Default.ExternalDiffTools;
			string repo = TestRepoFactory.CreateBranches();
			try
			{
				// 配置一个本机可探测的比对工具,让双选右键菜单出现 "Compare File Trees in {工具}"。
				// 用自定义 Custom 工具(临时配置),跑完恢复,避免依赖系统预置工具。
				ForkPlusSettings.Default.ExternalDiffTools = new ForkPlus.Settings.ExternalTool[]
				{
					new ForkPlus.Settings.ExternalTool(ToolType.Custom, "DiffX", "/usr/bin/diff", "$REMOTE $LOCAL", false, true)
				};
				ForkPlusSettings.Default.Save();

				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var revList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.Count >= 2;
						}), "修订列表未加载出数据");
						DragAndDropListView listView = UiClick.Find<DragAndDropListView>(revList, "RevisionListView");

						// 多选两个修订(顶部两行,普通提交)
						var revisions = revList.RevisionsDataSource.OfType<DecoratedRevision>().ToArray();
						var first = revisions[0];
						var second = revisions[1];
						listView.SelectedItems.Clear();
						listView.SelectedItems.Add(first);
						listView.SelectedItems.Add(second);
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(2, listView.SelectedItems.Count);

						ListBoxItem container = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							container = (ListBoxItem)listView.ContainerFromItem(second);
							return container != null;
						}), "第二个选中的行应已生成 ListBoxItem 容器");
						Dispatcher.UIThread.RunJobs();

						// 右键(第二行已选中)→ RevisionListView_ContextMenuPointerPressed → 打开多选右键菜单
						var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
						container.RaiseEvent(new PointerPressedEventArgs(
							container, pointer, window, new Point(5.0, 5.0),
							(ulong)Environment.TickCount64,
							new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.RightButtonPressed),
							KeyModifiers.None));
						Dispatcher.UIThread.RunJobs();

						bool menuOpen = listView.ContextMenu.IsOpen;
						bool menuHasTreeCompare = listView.ContextMenu.Items.OfType<object>()
							.Any(x => x is MenuItem m && (m.Header?.ToString()?.Contains("完整文件树") == true
								|| m.Header?.ToString()?.Contains("Compare File Trees") == true));
						Assert.True(menuOpen, "多选两个修订后右键应打开右键菜单");
						Assert.True(menuHasTreeCompare,
							"双选右键菜单应包含 '用 {工具} 比较完整文件树' 项(Compare File Trees in {工具})");
						ManualScreenshotHelper.Snap(window, "05-filetree-compare-context-menu", "04-revision-details");

						// 关闭右键菜单,防止自动 dismiss 处理器残留
						listView.ContextMenu?.Close();
						Dispatcher.UIThread.RunJobs();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				try
				{
					ForkPlusSettings.Default.ExternalDiffTools = prevDiff;
					ForkPlusSettings.Default.Save();
				}
				catch { }
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 5) git mm Upload 允许空提交 (ch-18) ============================

		[Fact]
		public void Ch18_GitMmUploadHonorNoChanges()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			HeadlessAppBootstrap.Run(delegate
			{
				var upload = new GitMmUploadWindow("/tmp/fpmanual_gitmm_ws");
				upload.Show();
				RunJobs();
				// 展开 "高级选项(Advanced Options)" 折叠区,让 "Allow empty commit" 复选框在截图中可见
				Expander? advanced = upload.GetVisualDescendants().OfType<Expander>()
					.FirstOrDefault(e =>
						e.Header?.ToString()?.Contains("高级选项") == true
						|| e.Header?.ToString()?.Contains("Advanced Options") == true);
				Assert.True(advanced != null, "上传弹窗应包含 '高级选项' 折叠区(Advanced Options)");
				advanced.IsExpanded = true;
				RunJobs();
				// 勾选 "Allow empty commit"(--honor-no-changes)
				upload.HonorNoChangesCheckBox.IsChecked = true;
				RunJobs();
				Assert.True(upload.HonorNoChangesCheckBox.IsChecked == true, "允许空提交复选框应被勾选");
				Assert.True(upload.HonorNoChangesCheckBox.IsVisible, "展开后允许空提交复选框应可见");
				Assert.True(upload.GetVisualDescendants().OfType<TextBlock>()
					.Any(t => t.Text != null && t.Text.Contains("--honor-no-changes")), "命令预览应包含 --honor-no-changes");
				ManualScreenshotHelper.Snap(upload, "06-gitmm-upload-honor-no-changes", "18-gitmm");
				upload.Close();
			});
		}
	}
}