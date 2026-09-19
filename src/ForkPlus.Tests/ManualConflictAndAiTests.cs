// 用户手册截图测试(子代理 ManualConflictAndAi, 2026-09-19):为 ForkPlus 用户手册
// ch-14 合并冲突解决 补充"三方合并编辑器"与"AI 辅助解决冲突"两小节的中文界面截图。
// 场景构造口径与 ManualAgentB 的 Ch14_Conflicts 方法/E2e10MergeConflictTests/
// E2e24AiTests 参考测试一致:
//   - 三方并排编辑器:TestRepoFactory.CreateConflict() 建冲突仓库 → Commit 视图选中
//     Unmerged 文件 → MergeConflictUserControl 装配 → 双选 ResolveButton → 模态泵
//     (Dispatcher.UIThread.Post(processor) 内 WaitForMergeWindowLoaded) 打开
//     SideBySideMergeWindow → 截图 03。E2e10 用例3 已实测通过(约10s),是可靠地基。
//   - AI 冲突解析:OpenAiStubServer.Start(body=>...) + ForkPlusSettings 指向 stub
//     (AiReviewServiceUrl/AiReviewApiKey/AiReviewSelectedModel, 参考 E2e24 ConfigureAi)。
//     在 MergeConflictUserControl 触发 AiResolveButton_点击 → BuildResolveConflictsPrompt
//     (含 "Resolve ALL conflicts" 关键字) → stub 检测到该关键字则 sleep 留下截 busy 态的
//     窗口期并返回去掉冲突标记的合并内容 → 确认对话框(阻塞模态)由异步模态泵点击 Apply
//     → ResolveMergeConflictGitCommand 写回 + git add → 截图 04/05/06/07。
// 截图落盘到 docs/manual/screenshots/14-conflicts/ (ManualScreenshotHelper.Snap)。
// 覆盖子目录说明:
//   03-side-by-side-merge  三方并排合并窗口(图14-3)
//   04-ai-resolve-button   冲突视图上的"AI 解决冲突"按钮
//   05-ai-resolving        AI 正在解决冲突(busy 态)
//   06-ai-resolve-confirm  "AI 已解决所有冲突。是否应用解决后的内容?"确认弹窗
//   07-ai-resolved         AI 解析写回后的已解决视图
using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ManualConflictAndAiTests
	{
		// ============================ 共享助手 ============================

		/// <summary>打开仓库切 Commit 视图,选中冲突文件并等 MergeConflictUserControl 装配。
		/// 与 ManualAgentB.Ch14_Conflicts / E2e10 同款口径。</summary>
		private static MergeConflictUserControl OpenCommitViewAndWaitConflict(
			string repo, string filePath, out MainWindow outWindow)
		{
			RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out MainWindow window);
			outWindow = window;
			repoControl.ActivateCommitView();
			Dispatcher.UIThread.RunJobs();
			CommitUserControl commit = repoControl.Content.CommitUserControl;
			StageFileUserControl stage = commit.StageFileUserControl;
			Assert.True(UiClick.WaitFor(delegate
			{
				return stage.AllUnstagedFiles.Any(f => f.Path == filePath);
			}), "工作区状态未装配（未找到未暂存文件 " + filePath + "）");
			stage.UnstagedFilesFileListUserControl.SelectFile(filePath);
			Dispatcher.UIThread.RunJobs();
			MergeConflictUserControl conflict = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				conflict = UiClick.FindAll<MergeConflictUserControl>(window).FirstOrDefault();
				return conflict != null && conflict.FileNameTextBlock.FilePath == filePath;
			}), "选中 Unmerged 文件后应出现并完成装配 MergeConflictUserControl");
			return conflict;
		}

		/// <summary>在模态泵内等待三方合并窗口出现并完成三编辑器装配,返回窗口实例。
		/// 与 ManualAgentB/E2e10 同款实现。</summary>
		private static SideBySideMergeWindow WaitForMergeWindowLoaded()
		{
			SideBySideMergeWindow mergeWindow = null;
			int tries = 0;
			while (tries++ < 300)
			{
				Dispatcher.UIThread.RunJobs();
				mergeWindow = WpfApp.Windows.OfType<SideBySideMergeWindow>().FirstOrDefault();
				if (mergeWindow != null
					&& mergeWindow.LocalMergeEditor.MergeConflictView != null
					&& mergeWindow.RemoteMergeEditor.MergeConflictView != null
					&& mergeWindow.MergedMergeEditor.MergeConflictView != null)
				{
					return mergeWindow;
				}
			}
			if (mergeWindow == null)
			{
				throw new InvalidOperationException("三方合并窗口未出现（模态泵 300 轮超时）");
			}
			throw new InvalidOperationException("三方编辑器视图未装配（三编辑器 MergeConflictView 应非空）");
		}

		// ---- AI 设置快照/还原(防持久化污染;命名空间与 ForkPlusSettings 属性需存在)----
		private sealed class AiPrefsSnapshot
		{
			public readonly string ServiceUrl;
			public readonly string ApiKey;
			public readonly string SelectedModel;
			public AiPrefsSnapshot(string svc, string key, string model)
			{
				ServiceUrl = svc; ApiKey = key; SelectedModel = model;
			}
		}

		private static AiPrefsSnapshot SnapshotAiPrefs()
		{
			return new AiPrefsSnapshot(
				ForkPlusSettings.Default.AiReviewServiceUrl,
				ForkPlusSettings.Default.AiReviewApiKey,
				ForkPlusSettings.Default.AiReviewSelectedModel);
		}

		private static void RestoreAiPrefs(AiPrefsSnapshot snap)
		{
			ForkPlusSettings.Default.AiReviewServiceUrl = snap.ServiceUrl;
			ForkPlusSettings.Default.AiReviewApiKey = snap.ApiKey;
			ForkPlusSettings.Default.AiReviewSelectedModel = snap.SelectedModel;
			ForkPlusSettings.Default.Save();
		}

		/// <summary>把 AI 设置指向 stub 服务器(与 E2e24 ConfigureAi 同口径)。</summary>
		private static void ConfigureAi(OpenAiStubServer server)
		{
			ForkPlusSettings.Default.AiReviewServiceUrl = server.BaseUrl;
			ForkPlusSettings.Default.AiReviewApiKey = "stub-key";
			ForkPlusSettings.Default.AiReviewSelectedModel = "stub-model-a";
			ForkPlusSettings.Default.AiReviewRetryCount = 0;
			ForkPlusSettings.Default.AiReviewTimeoutSeconds = 30;
			ForkPlusSettings.Default.AiReviewAutoFetchModels = true;
			ForkPlusSettings.Default.Save();
		}

		// ============================ ch-14 三方合并编辑器(用例1) ============================
		// 参考 ManualAgentB.Ch14_Conflicts:经模态泵打开 SideBySideMergeWindow 并截图 03,
		// 不提交直接关窗(保持仓库未解决状态)。

		[Fact]
		public void Ch14_ThreeWayMerge_Screenshot()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateConflict();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "conflicted.txt", out var window);
					try
					{
						// 默认双选 → Merge 按钮可用,点开三方编辑器
						Assert.True(conflict.LocalCheckBox.IsChecked.GetValueOrDefault(), "默认 ours 应勾选");
						Assert.True(conflict.RemoteCheckBox.IsChecked.GetValueOrDefault(), "默认 theirs 应勾选");
						var handled = new bool[1];
						var handlerError = new string[1];
						Dispatcher.UIThread.Post(delegate
						{
							SideBySideMergeWindow mergeWindow = null;
							try
							{
								mergeWindow = WaitForMergeWindowLoaded();
								ManualScreenshotHelper.Snap(mergeWindow, "03-side-by-side-merge", "14-conflicts");
								mergeWindow.Close(false); // 不提交,关窗返回
								handled[0] = true;
							}
							catch (Exception ex)
							{
								handlerError[0] = ex.ToString();
								// 防死锁:handler 异常时必须关窗,否则模态 DispatcherFrame 永不退出
								try { mergeWindow?.Close(false); } catch { }
							}
						}, DispatcherPriority.Background);

						UiClick.Click(conflict.ResolveButton);

						Assert.True(handlerError[0] == null, "模态泵 handler 异常:\n" + handlerError[0]);
						Assert.True(handled[0], "三方窗口 handler 未执行（模态泵未推进？）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally { TestRepoFactory.Cleanup(repo); }
		}

		// ============================ ch-14 AI 辅助解决冲突(用例2) ============================

		[Fact]
		public void Ch14_AIResolve_Screenshot()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string repo = TestRepoFactory.CreateConflict();
			AiPrefsSnapshot snap = SnapshotAiPrefs();
			using var server = OpenAiStubServer.Start(Body =>
			{
				// BuildResolveConflictsPrompt 返回含 "Resolve ALL conflicts" 关键字;
				// 命中后 sleep 600ms 留下截 busy 态的窗口期,并返回去掉冲突标记的合并内容。
				if (Body.Contains("Resolve ALL conflicts", StringComparison.OrdinalIgnoreCase))
				{
					Thread.Sleep(600);
					return "our line\ntheir line\n"; // 合并两侧,无 <<<<<<< 标记
				}
				return null; // 未命中 → DefaultReply
			});
			ConfigureAi(server);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					MergeConflictUserControl conflict = OpenCommitViewAndWaitConflict(repo, "conflicted.txt", out var window);
					try
					{
						// AI 配置完毕且未解决 → 冲突视图应显示 "AI 解决冲突" 按钮
						Assert.True(conflict.AiResolveButton.IsVisible,
							"AI Resolve 按钮应可见(AI 已配置且未解决)");
						ManualScreenshotHelper.Snap(window, "04-ai-resolve-button", "14-conflicts");

						var handled = new bool[1];
						var handlerError = new string[1];
						// 异步模态泵:轮询等确认对话框出现 → 截图 06 → 点 Apply → AI 写回。
						// 用 await Task.Delay 轮询(非 Thread.Sleep 占住 UI 线程),给 AI 流程的
						// 异步 continuation 和 ShowDialog 的模态 Frame 机会推进。
						Dispatcher.UIThread.Post(async delegate
						{
							try
							{
								MessageBoxWindow confirm = null;
								for (int tries = 0; tries < 700; tries++)
								{
									await System.Threading.Tasks.Task.Delay(50);
									confirm = WpfApp.Windows.OfType<MessageBoxWindow>()
										.FirstOrDefault(w => w.IsVisible);
									if (confirm != null)
									{
										break;
									}
								}
								if (confirm == null)
								{
									handlerError[0] = "AI 确认对话框未出现(700 轮超时)";
									return;
								}
								ManualScreenshotHelper.Snap(confirm, "06-ai-resolve-confirm", "14-conflicts");
								Button apply = UiClick.FindAll<Button>(confirm)
									.FirstOrDefault(b => UiClick.ContentText(b) == E2eMainWindowHarness.Tr("Apply"));
								if (apply == null)
								{
									handlerError[0] = "确认弹窗 Apply 按钮未找到";
									confirm.Close(false);
									return;
								}
								UiClick.Click(apply);
								handled[0] = true;
							}
							catch (Exception ex)
							{
								handlerError[0] = ex.ToString();
							}
						}, DispatcherPriority.Background);

						// 触发 AI 解析:读取带冲突标记文件 → 发请求(stub sleep 窗口) → 截 busy 态
						UiClick.Click(conflict.AiResolveButton);
						ManualScreenshotHelper.Snap(window, "05-ai-resolving", "14-conflicts");

						// 等待确认 + 写回完成(stub 返回后 continuation + 模态泵推进)
						Assert.True(UiClick.WaitFor(delegate
						{
							return handled[0] || handlerError[0] != null;
						}, 30000), "AI 解析确认处理未在超时内完成");
						Assert.True(handlerError[0] == null, "模态泵 handler 异常:\n" + handlerError[0]);
						Assert.True(handled[0], "AI 确认对话框处理未执行");

						// ===== git 验证:AI 返回内容写回 + add,无冲突标记 =====
						Dispatcher.UIThread.RunJobs();
						string content = File.ReadAllText(Path.Combine(repo, "conflicted.txt"));
						Assert.Equal("our line\ntheir line\n", content);
						Assert.False(content.Contains("<<<<<<<") || content.Contains(">>>>>>>"),
							"AI 写回后文件不应残留冲突标记");
						string status = TestRepoFactory.GitOutput(repo, "status --porcelain");
						Assert.False(status.Contains("UU"), "AI 解决后不应有 unmerged，实际:\n" + status);
						Assert.True(status.Contains("M  conflicted.txt"),
							"AI 解决后应为已暂存修改，实际:\n" + status);

						ManualScreenshotHelper.Snap(window, "07-ai-resolved", "14-conflicts");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestoreAiPrefs(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}