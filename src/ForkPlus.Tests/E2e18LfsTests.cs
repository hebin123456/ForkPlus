// E2E 模块18（2026-09-06）：Git LFS 4 窗口 + 命令链（6 用例）。
// 覆盖：Track（空模式禁用 + 命令预览单/多模式 + 匹配文件预览（GitLfsGetPreviewFilesGitCommand
// 真实 ls-files -i --exclude）+ 真实 track 写 .gitattributes）/Status（列表装配（git lfs
// ls-files）+ Lock/Unlock 真实链路（经 LfsLocksApiServer 本地 HTTP locks API）+ Owner 显示/
// 清空 + 重复 Lock 409 错误弹窗 + 过滤）/Fetch（origin 装配 + `git lfs fetch origin` 预览 +
// 真实 fetch：对象落 .git/lfs/objects 且工作区仍指针）/Pull（预览 + 真实 pull：工作区
// smudge 成真实内容）/Init+Deinit（钩子落地/移除 + IsGitLfsInitialized 纯文件判定）/Prune
//（JobQueue 执行无错 + 预览匹配纯逻辑）。
//
// 模式：真实 MainWindow 打开仓库（Status/Fetch/Pull/Init/Deinit/Prune 走 JobQueue 生产管线），
// Track 窗口仅需 GitModule（直构）。截图 2026-09-06 口径：主窗口 1920×1080，弹窗按自然比例。
//
// 环境闭环（2026-09-06 探针实证）：
// ① 沙箱 git-lfs 3.0.2 已装（/usr/bin/git-lfs）——app 派生 git 经 PATH 找到它（模块17
//    gitflow 同款 PATH 查找口径）。
// ② 本地 bare 远程的 LFS 对象传输：源仓 pre-push 钩子（lfs install --local 装）在 push 时
//    复制对象进 bare/lfs/objects——远程 URL 必须绝对路径（相对路径不触发）；克隆检出指针
//    （无全局 filter 配置）；指针文件的 lfs clean 幂等 → install --local 后 status 仍干净。
// ③ locks API：git lfs lock/locks/unlock 走 HTTP LFS API（lfs.url 配置），file:// 远程
//    "missing protocol" 报错——测试基建 LfsLocksApiServer 在 127.0.0.1 随机端口实现
//    GET /locks + POST /locks + POST /locks/{id}/unlock 三端点（python 原型探针通过后移植）。
//    owner 固定 "Test User"（GetLfsLocksGitCommand 解析 `path\towner\tID:...` 三段 tab 输出）。
// ④ 空模式的 Track 预览：ls-files -i 无 --exclude → git 报 fatal（exit 128）→ 命令失败
//    回退 "0 files match"（生产行为，非 bug）。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e18LfsTests
	{
		private const string ModuleDir = "18-lfs";

		// ============================ 共享助手 ============================

		private static string GitOf(string repo, string args)
		{
			return TestRepoFactory.GitOutput(repo, args).Trim();
		}

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		/// <summary>命令预览文本（ForkPlusDialogWindow.AddCommandPreview 生成的 Consolas TextBlock）。</summary>
		private static string CommandPreviewOf(ForkPlusDialogWindow dialog)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith("git ", StringComparison.Ordinal))?.Text ?? "";
		}

		/// <summary>Status 窗口列表当前条目（ItemsSource 直接是 LfsFileViewModel[]）。</summary>
		private static LfsFileViewModel[] ItemsOf(GitLfsStatusWindow dialog)
		{
			return dialog.LfsFilesListBox.ItemsSource as LfsFileViewModel[] ?? new LfsFileViewModel[0];
		}

		/// <summary>从 LFS 指针文件解析 oid（"oid sha256:&lt;hex&gt;" 行）。</summary>
		private static string OidOfPointer(string path)
		{
			foreach (string line in File.ReadAllLines(path))
			{
				if (line.StartsWith("oid sha256:", StringComparison.Ordinal))
				{
					return line.Substring("oid sha256:".Length);
				}
			}
			throw new InvalidOperationException("指针文件缺 oid 行: " + path);
		}

		/// <summary>等待 RepositoryData 的 remotes 装配到位（Fetch/Pull 窗口构造器读它）。</summary>
		private static void WaitRemotesLoaded(RepositoryUserControl control)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData != null
					&& control.RepositoryData.Remotes != null
					&& control.RepositoryData.Remotes.Items.Any(r => r.Name == "origin");
			}), "remotes 应装配 origin（15s 超时）");
		}

		// ============================ 1) Track ============================

		[Fact]
		public void Track_ValidationPreviewAndRealTrack()
		{
			string repo = TestRepoFactory.CreateLfs();
			try
			{
				// 未跟踪的 *.dat ×2（供匹配预览；*.bin 已由工厂 track + 提交 data.bin）
				File.WriteAllText(Path.Combine(repo, "extra1.dat"), "e1\n");
				File.WriteAllText(Path.Combine(repo, "extra2.dat"), "e2\n");

				HeadlessAppBootstrap.Run(delegate
				{
					var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
					var dialog = new GitLfsTrackWindow(module, "");
					dialog.Show();
					Dispatcher.UIThread.RunJobs();
					ForkPlusDialogFooter footer = FooterOf(dialog);

					// 空模式：提交禁用 + 预览回退 0 files match（ls-files -i 无 exclude 报 fatal 的失败路径）
					Assert.False(footer.SubmitButton.IsEnabled, "空模式提交应禁用");
					Assert.True(UiClick.WaitFor(delegate
					{
						return dialog.PreviewLabelTextBlock.Text == E2eMainWindowHarness.TrFormat("0 files match");
					}), "空模式预览应回退 0 files match（15s 超时）");

					// 单模式：提交启用 + 命令预览 + 匹配文件预览（0.3s 防抖 + 后台 ls-files 真实执行）
					// ⚠️ Avalonia 12 的 TextBox.TextChanged 是异步派发（RaiseTextChangeEvents 内
					// Dispatcher.UIThread.Post(…, Normal)，跟随 WinUI 语义——TextChanging 才同步），
					// 程序化 .Text 赋值后必须泵一次 RunJobs 让派发事件落地再断言（模块 11/14/15/17
					// 全部如此；本用例首版漏泵 → "非空模式提交应启用"确定性失败，非生产 bug——
					// 真实应用有消息循环，事件必然在用户可感知前派发）。
					dialog.PatternTextBox.Text = "*.dat";
					Dispatcher.UIThread.RunJobs();
					Assert.True(footer.SubmitButton.IsEnabled, "非空模式提交应启用");
					Assert.Equal("git lfs track *.dat", CommandPreviewOf(dialog));
					Assert.True(UiClick.WaitFor(delegate
					{
						return dialog.PreviewLabelTextBlock.Text == E2eMainWindowHarness.TrFormat("{0} files match", 2);
					}), "*.dat 应匹配 2 个文件（15s 超时）");
					Assert.Contains("extra1.dat", dialog.PreviewTextBox.Text);
					Assert.Contains("extra2.dat", dialog.PreviewTextBox.Text);
					ScreenshotHelper.Snap(dialog, "01-track-preview", ModuleDir);

					// 多行模式：命令按行拼接（同样须泵：TextChanged 异步派发）
					dialog.PatternTextBox.Text = "*.dat\n*.bin";
					Dispatcher.UIThread.RunJobs();
					Assert.Equal("git lfs track *.dat *.bin", CommandPreviewOf(dialog));

					// 真实提交：git lfs track *.dat *.bin → .gitattributes 追加 *.dat（*.bin 已存在——
					// git-lfs 幂等 "already supported"，探针实证不重复追加）
					UiClick.Click(footer.SubmitButton);
					Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
						"Track 提交后应关闭弹窗（15s 超时）");
					string attributes = File.ReadAllText(Path.Combine(repo, ".gitattributes"));
					Assert.Contains("*.dat filter=lfs", attributes);
					Assert.Contains("*.bin filter=lfs", attributes);
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 2) Status（文件 + 锁全链路） ============================

		[Fact]
		public void Status_FilesLockUnlockAndFilter()
		{
			string repo = TestRepoFactory.CreateLfs();
			try
			{
				using (LfsLocksApiServer locksServer = LfsLocksApiServer.Start())
				{
					// locks 走 HTTP API（file:// 无 API server——探针实证 missing protocol）
					TestRepoFactory.GitOutput(repo, "config lfs.url " + locksServer.BaseUrl);

					HeadlessAppBootstrap.Run(delegate
					{
						RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
						try
						{
							var dialog = new GitLfsStatusWindow(repoControl);
							dialog.Show();
							Dispatcher.UIThread.RunJobs();

							// 列表装配：构造器 "LFS files" job（git lfs ls-files）→ Refresh → "LFS Locks"
							// job（git lfs locks → 空）→ 列表刷新。data.bin 是唯一 LFS 文件。
							Assert.True(UiClick.WaitFor(delegate
							{
								return ItemsOf(dialog).Any(i => i.Path == "data.bin");
							}), "LFS 文件列表应装配 data.bin（15s 超时）");
							Assert.Null(ItemsOf(dialog).First(i => i.Path == "data.bin").Owner);
							ScreenshotHelper.Snap(dialog, "02-status-files", ModuleDir);

							// 选中 + Lock：真实 git lfs lock data.bin → 服务器 201 → Refresh → Owner 显示
							LfsFileViewModel dataBin = ItemsOf(dialog).First(i => i.Path == "data.bin");
							dialog.LfsFilesListBox.SelectedItem = dataBin;
							Dispatcher.UIThread.RunJobs();
							UiClick.Click(dialog.LockButton);
							Assert.True(UiClick.WaitFor(delegate
							{
								return ItemsOf(dialog).FirstOrDefault(i => i.Path == "data.bin")?.Owner == "Test User";
							}), "Lock 后 Owner 应显示 Test User（15s 超时）");
							ScreenshotHelper.Snap(dialog, "03-status-locked", ModuleDir);

							// 重复 Lock：服务器 409 → 错误弹窗（被测行为——看门狗关闭 + 记录）
							HeadlessAppBootstrap.ExpectErrorDialogs();
							UiClick.Click(dialog.LockButton);
							Assert.True(UiClick.WaitFor(delegate
							{
								return HeadlessAppBootstrap.PeekCapturedErrorDialogs().Length >= 1;
							}), "重复 Lock 应捕获错误弹窗（15s 超时）");
							HeadlessAppBootstrap.TakeCapturedErrorDialogs();
							// 失败后 Refresh 照常：锁状态保持（owner 仍显示）
							Assert.True(UiClick.WaitFor(delegate
							{
								return ItemsOf(dialog).FirstOrDefault(i => i.Path == "data.bin")?.Owner == "Test User";
							}), "重复 Lock 失败后锁状态应保持（15s 超时）");

							// 过滤：FilterRequestChanged → DelayedAction → RefreshLfsFilesList（路径/owner 包含匹配）
							dialog.FilterTextBox.Text = "zzz";
							Assert.True(UiClick.WaitFor(delegate { return ItemsOf(dialog).Length == 0; }),
								"无匹配过滤应清空列表（15s 超时）");
							dialog.FilterTextBox.Text = "data";
							Assert.True(UiClick.WaitFor(delegate
							{
								return ItemsOf(dialog).Any(i => i.Path == "data.bin");
							}), "data 过滤应保留 data.bin（15s 超时）");

							// 重新选中 + Unlock：真实 git lfs unlock data.bin → 200 → Refresh → Owner 清空
							LfsFileViewModel locked = ItemsOf(dialog).First(i => i.Path == "data.bin");
							dialog.LfsFilesListBox.SelectedItem = locked;
							Dispatcher.UIThread.RunJobs();
							UiClick.Click(dialog.UnlockButton);
							Assert.True(UiClick.WaitFor(delegate
							{
								return ItemsOf(dialog).FirstOrDefault(i => i.Path == "data.bin")?.Owner == null;
							}), "Unlock 后 Owner 应清空（15s 超时）");
							ScreenshotHelper.Snap(dialog, "04-status-unlocked", ModuleDir);
						}
						finally
						{
							E2eMainWindowHarness.CloseRepositoryTab(window, repo);
						}
					});
				}
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 3) Fetch ============================

		[Fact]
		public void Fetch_RemoteSelectionAndRealFetch()
		{
			string work = TestRepoFactory.CreateLfsRemoteBehind();
			string root = Directory.GetParent(work).FullName;
			try
			{
				// 前置：工作区指针 + 本地无 LFS 对象（克隆未拉取）
				string dataBin = Path.Combine(work, "data.bin");
				Assert.StartsWith("version https://git-lfs", File.ReadAllText(dataBin));
				string oid = OidOfPointer(dataBin);
				// LFS 对象路径：objects/<oid[0:2]>/<oid[2:4]>/<oid>——第二级目录是 2 字符（如 bd/8d/bd8d...）。
				// 曾误写 Substring(2, 4)（4 字符目录），对象其实一直落盘成功，测试在错误路径下找文件而超时。
				string objectPath = Path.Combine(work, ".git", "lfs", "objects",
					oid.Substring(0, 2), oid.Substring(2, 2), oid);
				Assert.False(File.Exists(objectPath), "前置：克隆不应已持有 LFS 对象");

				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						WaitRemotesLoaded(repoControl);
						var dialog = new GitLfsFetchWindow(repoControl, repoControl.GitModule);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 装配：origin 选中 + 裸 lfs fetch 预览 + 提交启用
						Assert.Equal("origin", ((Remote)dialog.RemotesComboBox.SelectedItem).Name);
						Assert.True(footer.SubmitButton.IsEnabled, "选中远程时提交应启用");
						Assert.Equal("git lfs fetch origin", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "05-fetch-ready", ModuleDir);

						// 提交（入队即关）→ 真实 git lfs fetch origin → 对象落 .git/lfs/objects
						UiClick.Click(footer.SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
							"Fetch 提交后应关闭弹窗（15s 超时）");
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						Assert.True(UiClick.WaitFor(delegate { return File.Exists(objectPath); }),
							"fetch 应把 LFS 对象下载到 .git/lfs/objects（15s 超时）");
						// fetch 只取对象不 smudge：工作区仍是指针
						Assert.StartsWith("version https://git-lfs", File.ReadAllText(dataBin));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		// ============================ 4) Pull ============================

		[Fact]
		public void Pull_PreviewAndRealPull()
		{
			string work = TestRepoFactory.CreateLfsRemoteBehind();
			string root = Directory.GetParent(work).FullName;
			try
			{
				string dataBin = Path.Combine(work, "data.bin");
				Assert.StartsWith("version https://git-lfs", File.ReadAllText(dataBin));
				byte[] expected = TestRepoFactory.LfsBytes(); // 与工厂同种子复算

				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						WaitRemotesLoaded(repoControl);
						var dialog = new GitLfsPullWindow(repoControl, repoControl.GitModule);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();
						ForkPlusDialogFooter footer = FooterOf(dialog);

						// 装配：origin 选中 + 裸 lfs pull 预览
						Assert.Equal("origin", ((Remote)dialog.RemotesComboBox.SelectedItem).Name);
						Assert.True(footer.SubmitButton.IsEnabled, "选中远程时提交应启用");
						Assert.Equal("git lfs pull origin", CommandPreviewOf(dialog));
						ScreenshotHelper.Snap(dialog, "06-pull-ready", ModuleDir);

						// 提交 → 真实 git lfs pull origin（clone 已 install --local → 可 smudge）
						UiClick.Click(footer.SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }),
							"Pull 提交后应关闭弹窗（15s 超时）");
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						Assert.True(UiClick.WaitFor(delegate
						{
							return File.Exists(dataBin) && File.ReadAllBytes(dataBin).SequenceEqual(expected);
						}), "pull 应把工作区 data.bin smudge 成真实内容（15s 超时）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, work);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		// ============================ 5) Init / Deinit / 钩子判定 ============================

		[Fact]
		public void InitDeinitializeAndHookDetectionLogic()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
					var detector = new IsGitLfsInitializedGitCommand();
					string prePush = Path.Combine(repo, ".git", "hooks", "pre-push");

					// 纯文件判定：无钩子 → false
					Assert.False(detector.Execute(module), "无 pre-push 钩子应判定未初始化");

					// 钩子内容含 "git lfs pre-push" → true；不含 → false（内容判据而非存在性）
					File.WriteAllText(prePush, "#!/bin/sh\ngit lfs pre-push \"$@\"\n");
					Assert.True(detector.Execute(module), "含 git lfs pre-push 的钩子应判定已初始化");
					File.WriteAllText(prePush, "#!/bin/sh\necho custom hook\n");
					Assert.False(detector.Execute(module), "不含 lfs 内容的钩子应判定未初始化");
					File.Delete(prePush);

					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// InitGitLfsCommand → git lfs install --local → pre-push 钩子落地
						new InitGitLfsCommand().Execute(repoControl, repoControl.GitModule);
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						Assert.True(UiClick.WaitFor(delegate { return File.Exists(prePush); }),
							"Init 应写 pre-push 钩子（15s 超时）");
						Assert.True(File.ReadAllText(prePush).Contains("git lfs pre-push"),
							"钩子内容应含 git lfs pre-push");
						Assert.True(detector.Execute(module));

						// DeinitializeGitLfsCommand → git lfs uninstall --local → 钩子移除
						new DeinitializeGitLfsCommand().Execute(repoControl, repoControl.GitModule);
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						Assert.True(UiClick.WaitFor(delegate { return !File.Exists(prePush); }),
							"Deinit 应移除 pre-push 钩子（15s 超时）");
						Assert.False(detector.Execute(module));
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 6) Prune + 匹配预览纯逻辑 ============================

		[Fact]
		public void PruneJobAndPreviewFilesMatching()
		{
			string repo = TestRepoFactory.CreateLfs();
			try
			{
				File.WriteAllText(Path.Combine(repo, "extra1.dat"), "e1\n");
				File.WriteAllText(Path.Combine(repo, "extra2.dat"), "e2\n");

				HeadlessAppBootstrap.Run(delegate
				{
					var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);

					// GitLfsGetPreviewFilesGitCommand（Track 窗口预览数据源）：
					// ls-files -o -m -c -i --exclude=<pattern> → 匹配文件集合（HashSet 序不定，用包含断言）
					GitCommandResult<string[]> datMatch = new GitLfsGetPreviewFilesGitCommand().Execute(module, new[] { "*.dat" });
					Assert.True(datMatch.Succeeded);
					Assert.Equal(2, datMatch.Result.Length);
					Assert.Contains("extra1.dat", datMatch.Result);
					Assert.Contains("extra2.dat", datMatch.Result);

					GitCommandResult<string[]> binMatch = new GitLfsGetPreviewFilesGitCommand().Execute(module, new[] { "*.bin" });
					Assert.True(binMatch.Succeeded);
					Assert.Contains("data.bin", binMatch.Result); // 已提交（cached）的 LFS 文件
					Assert.DoesNotContain("normal.txt", binMatch.Result); // 不匹配模式

					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// GitLfsPruneCommand → JobQueue 执行 git lfs prune --verbose（真实运行；
						// 失败会弹 ErrorWindow → Run 收尾以根因文本失败——全绿即无错）
						new GitLfsPruneCommand().Execute(repoControl, repoControl.GitModule);
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						Dispatcher.UIThread.RunJobs();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});

				// prune 后仓库完好：ls-files 仍列 data.bin（被 main 引用的对象 retained）
				Assert.Contains("data.bin", GitOf(repo, "lfs ls-files"));
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
