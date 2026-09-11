// 回归测试（2026-09-11，"仓库→二分查找后标签下方的通知条不出现"）：
// 根因：WPF 原版由 NotificationBarBorder 样式的 DataTrigger + Storyboard 把通知条
// Border 的 Height 0↔28 动画（0.7s）；迁移到 Avalonia 时该样式被注释成空壳
// （Border.axaml），NotificationBarUserControl.axaml 的 Border.Height 硬编码 0，
// IsControlVisible 属性无人消费——merge/rebase/cherry-pick/revert/bisect/gitignore
// 建议等所有通知条全部不可见（bisect 的 Good/Bad/Skip 按钮只在此通知条上，
// 因此二分查找完全不可用）。修复：IsControlVisible setter 直接驱动 RootBorder.Height，
// axaml 用 DoubleTransition(0.7s) 对齐 WPF 展开动画。
// 用例1：属性→高度联动（单元级）；用例2：菜单栏"仓库→二分查找"同款命令全链路
// （命令→git bisect start→状态刷新→通知条展开→Good/Bad/Skip→reset 收起）。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class NotificationBarBisectTests
	{
		/// <summary>等通知条 Height 动画到达终值。headless 渲染时钟是手动的
		/// （HeadlessRenderTimer 需显式 tick），DoubleTransition 的 0.7s 展开动画依赖渲染
		/// tick 驱动——首跑实证：状态管线已到达、但动画在 UI 队列空闲时冻结（60s 仅推进
		/// ~0.3s 动画量）。每轮轮询主动补 3 帧，动画按真实 0.7s 时长收敛（实证 ~750ms）。</summary>
		private static bool WaitForHeightAnimated(NotificationBarUserControl bar, double target, int timeoutMs)
		{
			return UiClick.WaitFor(delegate
			{
				Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
				return bar.RootBorder.Height == target;
			}, timeoutMs);
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

		/// <summary>5 个线性提交 + 已提交的 .gitignore（避免收起断言被 gitignore 建议干扰）。</summary>
		private static string CreateBisectRepo()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpbisect_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			RunGit("init -q -b main", root);
			RunGit("config user.email test@example.com", root);
			RunGit("config user.name Test", root);
			File.WriteAllText(Path.Combine(root, ".gitignore"), "*.tmp\n");
			RunGit("add .gitignore", root);
			RunGit("commit -q -m c0", root);
			for (int i = 1; i <= 4; i++)
			{
				File.WriteAllText(Path.Combine(root, "f.txt"), "v" + i);
				RunGit("add f.txt", root);
				RunGit("commit -q -m c" + i, root);
			}
			return root;
		}

		// ===== 用例1：IsControlVisible ↔ RootBorder.Height 联动（单元级）=====

		[Fact]
		public void NotificationBar_IsControlVisible_DrivesRootBorderHeight()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 600, Height = 200 };
				var bar = new NotificationBarUserControl();
				window.Content = bar;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				bool initialCollapsed = bar.RootBorder.Height == 0.0;

				bar.IsControlVisible = true;
				// 展开/收起为 0.7s Transition（对齐 WPF Storyboard），补帧驱动动画完结
				bool expanded = WaitForHeightAnimated(bar, 28.0, 10000);

				bar.IsControlVisible = false;
				bool collapsed = WaitForHeightAnimated(bar, 0.0, 10000);

				window.Close();
				return initialCollapsed && expanded && collapsed;
			});
		}

		// ===== 用例2：仓库菜单"二分查找"全链路（命令→通知条→Good/Bad/Skip→reset 收起）=====
		// 线程口径（2026-09-11 首跑修正）：必须整体包在 HeadlessAppBootstrap.Run 里——
		// MainWindow/NotificationBar 只能由 UI 线程创建访问（E2e11 等全模块同款模式）；
		// 直接在 xunit 线程调 E2eMainWindowHarness.OpenRepository 会抛
		// "The calling thread cannot access this object because a different thread owns it"。

		[Fact]
		public void Bisect_FromRepositoryMenuCommand_ShowsNotificationBarWithGoodBadSkip()
		{
			string repoRoot = CreateBisectRepo();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repoRoot, out var window);
					try
					{
						var bar = window.GetVisualDescendants().OfType<NotificationBarUserControl>().First();
						Assert.True(bar != null, "仓库视图中应存在通知条控件");
						Assert.True(bar.RootBorder.Height == 0.0, "初始（无进行中操作）通知条应收起");

						// 菜单栏"仓库→二分查找"同款命令（MainWindowMenuManager.cs）。
						// 等待口径（2026-09-11 首跑实证）：bisect 命令 → 后台 JobQueue 执行 git →
						// Post 回 UI 触发 InvalidateAndRefresh → 新刷新 job 又入队后台 → 完成后 Post 回
						// UpdateRepositoryStatus → NotificationBar.Refresh()。链条跨 UI/后台线程多跳，
						// WaitForRepositoryJobs 的排空存在"Post 回调在 IsIdle 之后才入队"的竞态，
						// 且沙箱单条 git 派生 ~300ms——必须按管线终态（RepositoryState）轮询，
						// 超时 30s（对齐挂具慢环境口径），而非依赖队列瞬时空闲。
						RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Start);
						bool bisectState = UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryStatus?.RepositoryState is RepositoryState.BisectInProgress;
						}, 30000);
						Assert.True(bisectState, "bisect start 后仓库状态应刷新为 BisectInProgress");
						Assert.True(File.Exists(Path.Combine(repoRoot, ".git", "BISECT_START")),
							"git bisect start 应已写入 BISECT_START");

						// 状态到达后通知条随 Refresh() 展开（0.7s Transition），等动画完结
						bool expanded = WaitForHeightAnimated(bar, 28.0, 30000);
						Assert.True(expanded, "bisect start 后通知条应展开（高度 28）");

						// 文案：以本地化后的"Bisecting, started from '"开头
						Assert.True(bar.NotificationTextBlock.IsVisible, "通知条文本应可见");
						var firstRun = bar.NotificationTextBlock.Inlines.OfType<Avalonia.Controls.Documents.Run>().FirstOrDefault();
						Assert.True(firstRun != null && firstRun.Text.StartsWith(
							E2eMainWindowHarness.Tr("Bisecting, started from '"), StringComparison.Ordinal),
							"通知条文案应以 Bisecting 起头，实际: " + (firstRun != null ? firstRun.Text : "<null>"));

						// Good / Bad / Skip 三键可见且文案正确（bisect 的操作入口只在这里）
						Assert.True(bar.Button1.IsVisible, "Good 按钮应可见");
						Assert.True(bar.Button2.IsVisible, "Bad 按钮应可见");
						Assert.True(bar.Button3.IsVisible, "Skip 按钮应可见");
						Assert.Equal(E2eMainWindowHarness.Tr("Good"), bar.Button1.Content as string);
						Assert.Equal(E2eMainWindowHarness.Tr("Bad"), bar.Button2.Content as string);
						Assert.Equal(E2eMainWindowHarness.Tr("Skip"), bar.Button3.Content as string);
						Assert.True(bar.AbortButton.IsVisible, "Abort 按钮应可见");

						// 点 Good（真实 Click 路由 → Button1_Click → bisect good）：仍未收敛，通知条保持展开。
						// 等待口径：BISECT_LOG 追加 "# good:" 记录证明 git bisect good 真实执行（经后台 JobQueue）。
						bar.Button1.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						bool goodMarked = UiClick.WaitFor(delegate
						{
							string logPath = Path.Combine(repoRoot, ".git", "BISECT_LOG");
							return File.Exists(logPath)
								&& File.ReadAllText(logPath).Contains("# good:");
						}, 30000);
						Assert.True(goodMarked, "点击 Good 应执行 git bisect good（BISECT_LOG 追加记录）");
						bool stillExpanded = WaitForHeightAnimated(bar, 28.0, 10000);
						Assert.True(stillExpanded, "标记 good（无 bad 基准）后 bisect 仍在进行，通知条应保持展开");

						// reset（Abort 对 bisect 的最终路径）：通知条应收起。
						// 等待口径：BISECT_START 消失（git 侧终态）+ RepositoryState 刷新回 OK（UI 管线终态）。
						RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Reset);
						bool resetState = UiClick.WaitFor(delegate
						{
							return !File.Exists(Path.Combine(repoRoot, ".git", "BISECT_START"))
								&& repoControl.RepositoryStatus?.RepositoryState is RepositoryState.OK;
						}, 30000);
						Assert.True(resetState, "bisect reset 后仓库状态应刷新为 OK（BISECT_START 已清除）");
						bool collapsedAgain = WaitForHeightAnimated(bar, 0.0, 30000);
						string diag = "Height=" + bar.RootBorder.Height
							+ ", State=" + (repoControl.RepositoryStatus?.RepositoryState?.GetType().Name ?? "<null>")
							+ ", gitignore=" + File.Exists(Path.Combine(repoRoot, ".gitignore"));
						Assert.True(collapsedAgain, "bisect reset 后通知条应收起（仓库含已提交 .gitignore，不应触发 gitignore 建议）。诊断: " + diag);
						Assert.False(File.Exists(Path.Combine(repoRoot, ".git", "BISECT_START")),
							"git bisect reset 应清除 BISECT_START");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repoRoot);
					}
				});
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { /* 清理尽力而为 */ }
			}
		}
		// ===== 用例3：好/坏互斥——同一提交标记了一种后点另一种，应弹 git 错误且不落双标记 =====
		// 用户问题（2026-09-11）："好和坏可以同时选中，应该是标记了一种后，会导致git提示
		// 不能同时标记好和坏"。git 实证（2.34.1/2.50）：bisect start 后首个标记（good 或 bad）
		// HEAD 不动（git 等待另一基准，输出为空、退出码 0）；此时点另一个标记 = 同一提交
		// 标记好+坏——git 会先把双标记写进 refs/bisect/*，再以退出码 1 输出
		// "<sha> was both good and bad"；双标记落库后所有后续 bisect 命令全部卡死。
		// 修复（BisectGitCommand.FindOppositeMarkConflict）：执行 good/bad 前按 git 实时
		// refs 预检相反标记，冲突时直接返回 git 同款错误（ErrorWindow 提示），不执行命令。
		[Fact]
		public void Bisect_MarkGoodThenBadOnSameCommit_ShowsGitErrorAndDoesNotDoubleMark()
		{
			string repoRoot = CreateBisectRepo();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repoRoot, out var window);
					try
					{
						var bar = window.GetVisualDescendants().OfType<NotificationBarUserControl>().First();
						RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Start);
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryStatus?.RepositoryState is RepositoryState.BisectInProgress;
						}, 30000), "bisect start 后仓库状态应刷新为 BisectInProgress");
						Assert.True(WaitForHeightAnimated(bar, 28.0, 30000), "bisect start 后通知条应展开");

						// 正序：点 Good（首个标记，HEAD 不动）→ 同一提交再点 Bad → 应弹 git 错误
						AssertBisectMarkThenOppositeMarkConflicts(
							repoControl, bar, repoRoot, first: BisectGitCommand.BisectCommand.Good, opposite: BisectGitCommand.BisectCommand.Bad);

						// 逆序：reset 后点 Bad（首个标记）→ 同一提交再点 Good → 应弹 git 错误（预检的另一分支）
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryStatus?.RepositoryState is RepositoryState.OK;
						}, 30000), "reset 后状态应回到 OK（前置）");
						RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Start);
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryStatus?.RepositoryState is RepositoryState.BisectInProgress;
						}, 30000), "第二轮 bisect start 后状态应刷新为 BisectInProgress");
						AssertBisectMarkThenOppositeMarkConflicts(
							repoControl, bar, repoRoot, first: BisectGitCommand.BisectCommand.Bad, opposite: BisectGitCommand.BisectCommand.Good);

						// 收尾：reset 后通知条应收起（会话未被毒化的最终证明）
						RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Reset);
						bool resetState = UiClick.WaitFor(delegate
						{
							return !File.Exists(Path.Combine(repoRoot, ".git", "BISECT_START"))
								&& repoControl.RepositoryStatus?.RepositoryState is RepositoryState.OK;
						}, 30000);
						Assert.True(resetState, "bisect reset 后仓库状态应刷新回 OK");
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
				try { Directory.Delete(repoRoot, true); } catch { /* 清理尽力而为 */ }
			}
		}

		/// <summary>好/坏互斥单轮验证：点 first 标记（首个标记成功落库、HEAD 不动）→
		/// 声明预期错误弹窗 → 同一提交点 opposite → 断言：①弹出 git 同款错误
		/// （"was both good and bad"，看门狗捕获）；②相反标记未落库（BISECT_LOG 无
		/// opposite 记录、refs/bisect/bad 不存在）；③reset 后状态回 OK。</summary>
		private static void AssertBisectMarkThenOppositeMarkConflicts(
			RepositoryUserControl repoControl, NotificationBarUserControl bar, string repoRoot,
			BisectGitCommand.BisectCommand first, BisectGitCommand.BisectCommand opposite)
		{
			string firstLogToken = (first == BisectGitCommand.BisectCommand.Good) ? "# good:" : "# bad:";
			string oppositeLogToken = (opposite == BisectGitCommand.BisectCommand.Good) ? "# good:" : "# bad:";
			Button firstButton = (first == BisectGitCommand.BisectCommand.Good) ? bar.Button1 : bar.Button2;
			Button oppositeButton = (opposite == BisectGitCommand.BisectCommand.Good) ? bar.Button1 : bar.Button2;

			firstButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			bool firstMarked = UiClick.WaitFor(delegate
			{
				string logPath = Path.Combine(repoRoot, ".git", "BISECT_LOG");
				return File.Exists(logPath) && File.ReadAllText(logPath).Contains(firstLogToken);
			}, 30000);
			Assert.True(firstMarked, "点击首个标记应真实执行 git bisect（BISECT_LOG 追加 " + firstLogToken + " 记录）");

			// 预期错误弹窗口径（模块17 同款基建）：看门狗自动关闭 ErrorWindow 并记录文本。
			// 基线清空 → 触发 → Peek 轮询断言 → Take 排空（防 Run 收尾把预期弹窗误判为失败）。
			HeadlessAppBootstrap.TakeCapturedErrorDialogs();
			oppositeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			bool errorShown = UiClick.WaitFor(delegate
			{
				return HeadlessAppBootstrap.PeekCapturedErrorDialogs().Any(delegate(string d)
				{
					return d.Contains("was both good and bad");
				});
			}, 30000);
			Assert.True(errorShown, "同一提交标记好后又点坏（或反之）应弹 git 错误提示（was both good and bad）");
			HeadlessAppBootstrap.TakeCapturedErrorDialogs();

			// 未毒化：相反标记不得落库（预检拦截，git bisect 命令未执行）
			string bisectLog = File.ReadAllText(Path.Combine(repoRoot, ".git", "BISECT_LOG"));
			Assert.False(bisectLog.Contains(oppositeLogToken), "冲突的相反标记不应写入 BISECT_LOG（会话不得被双标记毒化）");
			// 修复（2026-09-11，本用例首版断言在逆序轮恒误报）：原断言固定查 refs/bisect/bad
			// 存在 + 目录文件数>1——逆序轮 first=Bad 时 refs/bisect/bad 是 first 标记的合法
			// 落库产物，断言恒失败。改为按 opposite 类型定向检查相反标记（opposite=Good → 查
			// refs/bisect/good-/old- 前缀；opposite=Bad → 查 refs/bisect/bad/new），并直接问
			// git（for-each-ref）而非数目录文件：松散 ref 与 packed-refs 两种存储形态都覆盖，
			// 也规避目录短暂不存在时 GetFiles 抛异常。
			string bisectRefs = RunGit("for-each-ref --format=%(refname) refs/bisect/", repoRoot);
			bool oppositeMarkLanded = (opposite == BisectGitCommand.BisectCommand.Good)
				? (bisectRefs.Contains("refs/bisect/good-") || bisectRefs.Contains("refs/bisect/old-"))
				: (bisectRefs.Contains("refs/bisect/bad") || bisectRefs.Contains("refs/bisect/new"));
			Assert.False(oppositeMarkLanded,
				"相反标记不应落库 refs/bisect/*（会话不得被双标记毒化，实际 refs：" + bisectRefs + "）");

			// 会话仍可用：reset 干净收尾
			RepositoryUserControl.Commands.Bisect.Execute(repoControl, BisectGitCommand.BisectCommand.Reset);
			bool resetOk = UiClick.WaitFor(delegate
			{
				return !File.Exists(Path.Combine(repoRoot, ".git", "BISECT_START"))
					&& repoControl.RepositoryStatus?.RepositoryState is RepositoryState.OK;
			}, 30000);
			Assert.True(resetOk, "冲突被拦截后 bisect 会话应仍可用（reset 正常收尾）");
		}
	}
}
