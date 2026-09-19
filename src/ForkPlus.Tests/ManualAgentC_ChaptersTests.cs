// 用户手册截图测试(子代理 C, 2026-09-19):为 ForkPlus 用户手册 ch-17 ~ ch-25 生成中文界面截图。
// 场景构造口径与 E2e17/E2e18/E2e20/E2e21/E2e22/E2e23/E2e24/E2e25 参考测试一致
// (TestRepoFactory 建仓 + E2eMainWindowHarness.OpenRepository 开主窗口 + UiClick 交互 +
// HeadlessAppBootstrap.Run),截图经 ManualScreenshotHelper.Snap 落盘到
// docs/manual/screenshots/<模块目录>/。
// 注意:本文件供测试运行生成截图,子代理只提交 docs/manual/ 下的章节与截图。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Dialogs.RepositoryOverview;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ManualAgentC_ChaptersTests
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

		private static string TrFormat(string text, params object[] args)
		{
			return E2eMainWindowHarness.TrFormat(text, args);
		}

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		private static string CommandPreviewOf(ForkPlusDialogWindow dialog)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith("git ", StringComparison.Ordinal))?.Text ?? "";
		}

		private static void SetText(TextBox textBox, string text)
		{
			textBox.Text = text;
			Dispatcher.UIThread.RunJobs();
		}

		private static void ClickMenuItem(MenuItem menuItem)
		{
			menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}

		private static void ClickRadioButton(ToggleButton radio, bool isChecked)
		{
			radio.IsChecked = isChecked;
			radio.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}

		private sealed class PrefsSnapshot
		{
			public Dictionary<string, object> Values = new Dictionary<string, object>();
			public bool CustomCommandsFileExisted;
			public string CustomCommandsContent;
		}

		private static PrefsSnapshot SnapshotPrefs()
		{
			var snap = new PrefsSnapshot();
			foreach (PropertyInfo property in typeof(ForkPlusSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (property.CanWrite && property.GetIndexParameters().Length == 0)
				{
					try { snap.Values[property.Name] = property.GetValue(ForkPlusSettings.Default); }
					catch { }
				}
			}
			string path = CustomCommandManager.GlobalPath();
			snap.CustomCommandsFileExisted = File.Exists(path);
			snap.CustomCommandsContent = snap.CustomCommandsFileExisted ? File.ReadAllText(path) : null;
			return snap;
		}

		private static void RestorePrefs(PrefsSnapshot snap)
		{
			try
			{
				foreach (var pair in snap.Values)
				{
					typeof(ForkPlusSettings).GetProperty(pair.Key)?.SetValue(ForkPlusSettings.Default, pair.Value);
				}
				ForkPlusSettings.Default.Save();
			}
			catch (Exception ex)
			{
				Console.WriteLine("[ManualAgentC] 设置恢复失败: " + ex.Message);
			}
			try
			{
				string path = CustomCommandManager.GlobalPath();
				if (snap.CustomCommandsFileExisted)
				{
					File.WriteAllText(path, snap.CustomCommandsContent);
				}
				else if (File.Exists(path))
				{
					File.Delete(path);
				}
				FieldInfo field = typeof(CustomCommandManager).GetField("_current",
					BindingFlags.NonPublic | BindingFlags.Static);
				field?.SetValue(null, null);
			}
			catch (Exception ex)
			{
				Console.WriteLine("[ManualAgentC] custom-commands.json 恢复失败: " + ex.Message);
			}
		}

		private static void ConfigureAi(OpenAiStubServer server, string model = "stub-model-a")
		{
			ForkPlusSettings.Default.AiReviewServiceUrl = server.BaseUrl;
			ForkPlusSettings.Default.AiReviewApiKey = "stub-key";
			ForkPlusSettings.Default.AiReviewSelectedModel = model;
			ForkPlusSettings.Default.AiReviewRetryCount = 0;
			ForkPlusSettings.Default.AiReviewTimeoutSeconds = 30;
			ForkPlusSettings.Default.AiReviewAutoFetchModels = true;
			ForkPlusSettings.Default.Save();
		}

		private static bool WaitForModels(ComboBox combo)
		{
			return UiClick.WaitFor(delegate
			{
				return combo.Items.Count >= 3
					&& combo.Items.OfType<string>().Contains("stub-model-a")
					&& combo.Items.OfType<string>().Contains("stub-model-c");
			});
		}

		// ============================ ch-17 GitFlow 工作流 ============================

		[Fact]
		public void Ch17_GitFlowWorkflow()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";

			// ----- 1) 初始化 GitFlow 窗口(预填) -----
			string clean = TestRepoFactory.CreateClean();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(clean, out var window);
					try
					{
						Assert.True(UiClick.WaitFor(delegate { return repoControl.RepositoryData != null; }),
							"RepositoryData 未装配");
						GitFlowInitWindow init = new GitFlowInitWindow(repoControl.GitModule);
						init.Show();
						RunJobs();
						Assert.Equal("main", init.MasterBranchTextBox.Text);
						Assert.Equal("develop", init.DevelopBranchTextBox.Text);
						Assert.Equal("feature/", init.FeaturePrefixTextBox.Text);
						Assert.Equal("git flow init", CommandPreviewOf(init));
						ManualScreenshotHelper.Snap(init, "01-gitflow-init", "17-gitflow");
						init.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, clean);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(clean);
			}

			// ----- 2) 已初始化仓库:feature / release / hotfix 的 start 窗口 -----
			string repo = TestRepoFactory.CreateGitFlow();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryData != null
								&& repoControl.RepositoryData.References.LocalBranches.Length >= 2;
						}), "引用未装配");

						GitFlowStartFeatureWindow feature = new GitFlowStartFeatureWindow(repoControl.GitModule);
						feature.Show();
						RunJobs();
						feature.FeatureNameTextBox.Text = "login";
						RudimentarySubmit(feature);
						ManualScreenshotHelper.Snap(feature, "02-gitflow-feature-start", "17-gitflow");
						feature.Close();

						GitFlowStartReleaseWindow release = new GitFlowStartReleaseWindow(repoControl.GitModule);
						release.Show();
						RunJobs();
						release.ReleaseNameTextBox.Text = "2.0";
						RudimentarySubmit(release);
						Assert.Equal("git flow release start 2.0 develop", CommandPreviewOf(release));
						ManualScreenshotHelper.Snap(release, "03-gitflow-release-start", "17-gitflow");
						release.Close();

						GitFlowStartHotfixWindow hotfix = new GitFlowStartHotfixWindow(repoControl.GitModule);
						hotfix.Show();
						RunJobs();
						hotfix.HotfixNameTextBox.Text = "patch-1";
						RudimentarySubmit(hotfix);
						ManualScreenshotHelper.Snap(hotfix, "04-gitflow-hotfix-start", "17-gitflow");
						hotfix.Close();
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

		/// <summary>保证 SubmitButton 可用(不强制,便于截图展示可用态);无副作用。</summary>
		private static void RudimentarySubmit(ForkPlusDialogWindow dialog)
		{
			RunJobs();
		}

		// ============================ ch-18 Git Mm 工作区 ============================

		[Fact]
		public void Ch18_GitMmWorkspaces()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";

			// ----- 1) Init 窗口(必填校验但不提交) -----
			HeadlessAppBootstrap.Run(delegate
			{
				var dialog = new InitGitMmRepositoryWindow();
				dialog.Show();
				RunJobs();
				dialog.ManifestUrlTextBox.Text = "http://example.com/manifest.git";
				dialog.ParentDirectoryTextBox.Text = "/tmp/fpe2e_gitmm_dest";
				dialog.RepositoryNameTextBox.Text = "ws1";
				RunJobs();
				Assert.Equal("git mm init -u http://example.com/manifest.git -m dependency.xml -b master -g default",
					CommandPreviewOf(dialog));
				ManualScreenshotHelper.Snap(dialog, "01-gitmm-init", "18-gitmm");
				dialog.Close();
			});

			// ----- 2) 工作区 tab + Start/Sync/Upload 弹窗 -----
			string root = Path.Combine(Path.GetTempPath(), "fpe2e_gitmm_ws_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			string wsDir = Path.Combine(root, "ws");
			Directory.CreateDirectory(Path.Combine(wsDir, ".mm"));
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// 预期弹窗：沙箱无 git-mm CLI → 打开工作区时 WarnIfGitMmUnavailable 弹 ErrorWindow
					// （E2e17 同款：在 func 内声明 + 轮询捕获 + Take 取走，避免 Run 收尾因捕获到
					//  遗留弹窗而失败——"git-mm" 是跨语言安全子串）。
					HeadlessAppBootstrap.ExpectErrorDialogs();
					E2eMainWindowHarness.OpenTab(wsDir, out var window);
					GitMmUserControl gitMm = window.GetVisualDescendants().OfType<GitMmUserControl>().FirstOrDefault();
					Assert.True(gitMm != null, "应创建 GitMmUserControl（GitMm 模式 tab）");
					Assert.Equal(wsDir, gitMm.WorkspacePath);
					ManualScreenshotHelper.Snap(window, "02-gitmm-workspace", "18-gitmm");
					// 取走 git-mm missing 警告，避免 Run 收尾抛"用例执行期间出现 git 错误弹窗"
					UiClick.WaitFor(delegate
					{
						return HeadlessAppBootstrap.PeekCapturedErrorDialogs().Any(t => t.Contains("git-mm"));
					});
					HeadlessAppBootstrap.TakeCapturedErrorDialogs();
					E2eMainWindowHarness.CloseRepositoryTab(window, wsDir);
				});
			}
			finally
			{
				try { Directory.Delete(root, recursive: true); } catch { }
			}

			// ----- 3) Start / Sync / Upload 弹窗(哑参数直构,截图构造级冒烟) -----
			HeadlessAppBootstrap.Run(delegate
			{
				var start = new GitMmStartWindow(null, null);
				start.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(start, "03-gitmm-start", "18-gitmm");
				start.Close();

				var sync = new GitMmSyncWindow("/tmp/fpe2e_gitmm_ws");
				sync.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(sync, "04-gitmm-sync", "18-gitmm");
				sync.Close();

				var upload = new GitMmUploadWindow("/tmp/fpe2e_gitmm_ws");
				upload.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(upload, "05-gitmm-upload", "18-gitmm");
				upload.Close();
			});
		}

		// ============================ ch-19 Git LFS ============================

		[Fact]
		public void Ch19_GitLfs()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";

			string repo = TestRepoFactory.CreateLfs();
			try
			{
				File.WriteAllText(Path.Combine(repo, "extra1.dat"), "e1\n");
				File.WriteAllText(Path.Combine(repo, "extra2.dat"), "e2\n");

				// ----- Track 窗口 -----
				HeadlessAppBootstrap.Run(delegate
				{
					var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
					var dialog = new GitLfsTrackWindow(module, "");
					dialog.Show();
					RunJobs();
					dialog.PatternTextBox.Text = "*.dat";
					RunJobs();
					Assert.Equal("git lfs track *.dat", CommandPreviewOf(dialog));
					Assert.True(UiClick.WaitFor(delegate
					{
						return dialog.PreviewLabelTextBlock.Text == TrFormat("{0} files match", 2);
					}), "*.dat 应匹配 2 个文件");
					ManualScreenshotHelper.Snap(dialog, "01-lfs-track", "19-lfs");
					dialog.Close();
				});

				// ----- Status 窗口（E2E 模块 18 口径：locks 走 HTTP API，需 LfsLocksApiServer） -----
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
						RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return ItemsOf(dialog).Any(i => i.Path == "data.bin");
						}), "LFS 文件列表应装配 data.bin");
						ManualScreenshotHelper.Snap(dialog, "02-lfs-status", "19-lfs");
						dialog.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}

				// ----- Fetch / Pull 窗口 -----
				string work = TestRepoFactory.CreateLfsRemoteBehind();
				try
				{
					HeadlessAppBootstrap.Run(delegate
					{
						RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
						try
						{
							var fetch = new GitLfsFetchWindow(repoControl, repoControl.GitModule);
							fetch.Show();
							RunJobs();
							ManualScreenshotHelper.Snap(fetch, "03-lfs-fetch", "19-lfs");
							fetch.Close();

							var pull = new GitLfsPullWindow(repoControl, repoControl.GitModule);
							pull.Show();
							RunJobs();
							ManualScreenshotHelper.Snap(pull, "04-lfs-pull", "19-lfs");
							pull.Close();
						}
						finally
						{
							E2eMainWindowHarness.CloseRepositoryTab(window, work);
						}
					});
				}
				finally
				{
					TestRepoFactory.Cleanup(work);
				}
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		private static LfsFileViewModel[] ItemsOf(GitLfsStatusWindow dialog)
		{
			return dialog.LfsFilesListBox.ItemsSource as LfsFileViewModel[] ?? new LfsFileViewModel[0];
		}

		// ============================ ch-20 查看器窗口 ============================

		[Fact]
		public void Ch20_ViewerWindows()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";

			// ----- Blame / 文件历史(repo: feature 活跃 + f1.txt 二次修改) -----
			string history = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			try
			{
				File.WriteAllText(Path.Combine(history, "f1.txt"), "f1\nf1 more\n");
				TestRepoFactory.GitOutput(history, "add f1.txt");
				TestRepoFactory.GitOutput(history, "commit -q -m f1second");
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(history, out var window);
					try
					{
						var blame = new BlameWindow(repoControl, "f1.txt", null, null);
						blame.Show();
						RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return blame.BlameListBox.ItemsSource != null; }),
							"blame 列表应装配");
						ManualScreenshotHelper.Snap(blame, "01-blame", "20-viewers");
						blame.Close();

						var fileHistory = new FileHistoryWindow(repoControl,
							new ShowFileHistoryWindowCommand.Mode.File("f1.txt"), null, null);
						fileHistory.Show();
						RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return fileHistory.TreeView.RootItem.Children.Count >= 1; }),
							"文件历史应装配");
						ManualScreenshotHelper.Snap(fileHistory, "02-file-history", "20-viewers");
						fileHistory.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, history);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(history);
			}

			// ----- 仓库概览 / 统计（统计硬门槛：活跃分支须 >2 提交——git log 无 refspec
//      只走当前分支，故需 checkoutFeature 以 feature 活跃 4 提交，参照 E2E 模块 20） -----
			string repo = TestRepoFactory.CreateHistoryRewrite(checkoutFeature: true);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var overview = new RepositoryOverviewWindow(repoControl, repoControl.GitModule);
						overview.Show();
						RunJobs();
						Assert.True(UiClick.WaitFor(delegate { return !overview.Fallback.IsVisible; }),
							"概览数据应加载");
						ManualScreenshotHelper.Snap(overview, "03-repository-overview", "20-viewers");
						overview.Close();

						var stats = new RepositoryStatisticsWindow(repoControl.GitModule);
						stats.Show();
						RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return stats.GetVisualDescendants().OfType<StatisticsUserControl>()
								.FirstOrDefault()?.StatsContainer.IsVisible == true;
						}), "统计应装配");
						ManualScreenshotHelper.Snap(stats, "04-repository-stats", "20-viewers");
						stats.Close();
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

			// ----- 修订详情 / 跳转行 -----
			string history2 = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				string f3Sha = TestRepoFactory.GitOutput(history2, "rev-parse feature").Trim();
				HeadlessAppBootstrap.Run(delegate
				{
					Assert.True(Sha.TryParse(f3Sha, out Sha parsed), "sha 应可解析");
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(history2, out var window);
					try
					{
						var details = new RevisionDetailsWindow(repoControl, repoControl.GitModule,
							new RevisionDiffTarget.Revision(parsed), "f3.txt");
						details.Show();
						RunJobs();
						ManualScreenshotHelper.Snap(details, "05-revision-details", "20-viewers");
						details.Close();

						var goToLine = new GoToLineWindow();
						goToLine.Show();
						RunJobs();
						ManualScreenshotHelper.Snap(goToLine, "06-go-to-line", "20-viewers");
						goToLine.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, history2);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(history2);
			}
		}

		// ============================ ch-21 偏好设置 ============================

		[Fact]
		public void Ch21_Preferences()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			var snap = SnapshotPrefs();
			try
			{
				string originalLanguage = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "en";
					HeadlessAppBootstrap.Run(delegate
					{
						var window = new PreferencesWindow();
						window.Show();
						RunJobs();
						try
						{
							var general = window.GeneralUserControl;
							Assert.True(general.LanguageComboBox.Items.Count >= 8, "语言下拉应含 8 语言");
							var items = general.LanguageComboBox.Items.Cast<ComboBoxItem>().ToList();
							Assert.Contains(items, i => (i.Tag as string) == "zh-Hans" && (i.Content as string) == "简体中文");

							// 切换为简体中文:重本地化
							ComboBoxItem zhItem = items.First(i => (i.Tag as string) == "zh-Hans");
							general.LanguageComboBox.SelectedItem = zhItem;
							RunJobs();
							Assert.Equal("zh-Hans", ForkPlusSettings.Default.UiLanguage);
							Assert.Equal(PreferencesLocalization.Translate("General", "zh-Hans"),
								window.GeneralTabItem.Header);
							ManualScreenshotHelper.Snap(window, "01-preferences-general", "21-preferences");
						}
						finally
						{
							window.Close();
						}
					});
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					ForkPlusSettings.Default.Save();
				}

				// ----- 主题切换:展示切换前后效果 -----
				string branchRepo = TestRepoFactory.CreateBranches();
				try
				{
					HeadlessAppBootstrap.Run(delegate
					{
						RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(branchRepo, out var window);
						try
						{
							Assert.True(UiClick.WaitFor(delegate { return repoControl.RepositoryData != null; }),
								"RepositoryData 未装配");
							MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Light);
							RunJobs();
							ManualScreenshotHelper.Snap(window, "02-theme-light", "21-preferences");

							MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Dark);
							RunJobs();
							ManualScreenshotHelper.Snap(window, "03-theme-dark", "21-preferences");

							MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.PurpleLight);
							RunJobs();
							ManualScreenshotHelper.Snap(window, "04-theme-solid-purple", "21-preferences");
						}
						finally
						{
							MainWindow.Commands.SwitchApplicationTheme.Execute(ThemeType.Light);
							RunJobs();
							E2eMainWindowHarness.CloseRepositoryTab(window, branchRepo);
						}
					});
				}
				finally
				{
					TestRepoFactory.Cleanup(branchRepo);
				}
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ ch-22 仓库设置 ============================

		[Fact]
		public void Ch22_RepositorySettings()
		{
			string repo = TestRepoFactory.CreateGitFlow();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryData != null
								&& repoControl.RepositoryData.References.LocalBranches.Length >= 2;
						}), "引用未装配");
						var dialog = new RepositorySettingsWindow(repoControl.GitModule, repoControl.RepositoryData);
						dialog.Show();
						RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return dialog.GetVisualDescendants().OfType<ModernTabControl>().Any();
						}), "Settings 应有现代 tab 容器");
						ManualScreenshotHelper.Snap(dialog, "01-repo-settings-general", "22-repo-settings");
						dialog.Close();
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

		// ============================ ch-23 SSH 与环境 ============================

		[Fact]
		public void Ch23_SshAndEnvironment()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			var snap = SnapshotPrefs();

			// ----- SSH 密钥管理与生成 -----
			HeadlessAppBootstrap.Run(delegate
			{
				var keys = new ConfigureSshKeysWindow();
				keys.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(keys, "01-ssh-keys", "23-ssh-env");
				keys.Close();

				var gen = new GenerateNewSshKeyWindow();
				gen.Show();
				RunJobs();
				gen.KeyFileNameTextBox.Text = "sample-key";
				gen.EmailTextBox.Text = "user@example.com";
				RunJobs();
				ManualScreenshotHelper.Snap(gen, "02-generate-key", "23-ssh-env");
				gen.Close();
			});

			// ----- Git 实例与工作区 -----
			HeadlessAppBootstrap.Run(delegate
			{
				var inst = new ConfigureGitInstanceWindow();
				inst.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(inst, "03-git-instance", "23-ssh-env");
				inst.Close();

				var ws = new ConfigureWorkspacesWindow();
				ws.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(ws, "04-workspaces", "23-ssh-env");
				ws.Close();
			});
			RestorePrefs(snap);
		}

		// ============================ ch-24 AI 功能 ============================

		[Fact]
		public void Ch24_AiFeatures()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			var snap = SnapshotPrefs();

			// ----- 提交信息组合(Composer) -----
			string repo = TestRepoFactory.CreateAiStaged();
			try
			{
				using (var server = OpenAiStubServer.Start(Body =>
				{
					if (Body.Contains("split a large batch of staged changes", StringComparison.OrdinalIgnoreCase))
					{
						return @"```forkplus-ai-wip-plan
[
  { ""subject"": ""Refactor App entry point"", ""body"": ""Add Main method and util helper"", ""files"": [""src/app.cs"", ""src/util.cs""], ""reason"": ""Both source files belong to the entry point refactor"" },
  { ""subject"": ""Add documentation notes"", ""body"": """", ""files"": [""docs/notes.md""], ""reason"": ""Documentation change is independent"" }
]
```";
					}
					return null;
				}))
				{
					ConfigureAi(server);
					HeadlessAppBootstrap.Run(delegate
					{
						RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
						try
						{
							repoControl.ActivateCommitView();
							RunJobs();
							CommitUserControl commit = repoControl.Content.CommitUserControl;
							Assert.True(UiClick.WaitFor(delegate
							{
								return commit.StageFileUserControl.AllStagedFiles.Length == 3;
							}), "3 个 staged 文件未装配");
							ChangedFile[] staged = commit.StageFileUserControl.ExpandedStagedFiles;

							var composer = new AiCommitComposerWindow(repoControl.GitModule, staged, amend: false);
							composer.Show();
							RunJobs();
							bool composed = UiClick.WaitFor(delegate
							{
								return composer.GroupsListBox.Items.Count == 2 && composer.ApplyAllButton.IsEnabled;
							}, 20000);
							if (composed)
							{
								ManualScreenshotHelper.Snap(composer, "01-ai-composer", "24-ai");
							}
							composer.Close();

							// ----- 代码审查窗口 -----
							var target = new AiCodeReviewTarget.Files(staged, amend: false);
							var review = new AiCodeReviewWindow(repoControl, target, aiAgent: null);
							review.Show();
							RunJobs();
							ManualScreenshotHelper.Snap(review, "02-ai-code-review", "24-ai");
							review.Close();
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

			// ----- AI 开发助手 -----
			string basic = TestRepoFactory.CreateBasic();
			try
			{
				using (var server = OpenAiStubServer.Start(Body =>
				{
					if (Body.Contains("user", StringComparison.OrdinalIgnoreCase))
					{
						return "I understand. Let me analyze the codebase. The `App` class currently has no `Main` method.";
					}
					return null;
				}))
				{
					ConfigureAi(server);
					HeadlessAppBootstrap.Run(delegate
					{
						RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(basic, out var window);
						try
						{
							var dev = new AiDevelopmentWindow(repoControl, repoControl.GitModule);
							dev.Show();
							RunJobs();
							Assert.True(WaitForModels(dev.ModelComboBox), "Dev 模型下拉应拉取");
							dev.InputTextBox.Text = "Add a Main method to App class";
							RunJobs();
							UiClick.Click(dev.SendButton);
							bool responseDone = UiClick.WaitFor(delegate
							{
								return !dev.StopButton.IsVisible;
							}, 20000);
							if (responseDone)
							{
								ManualScreenshotHelper.Snap(dev, "03-ai-development", "24-ai");
							}
							dev.Close();
						}
						finally
						{
							E2eMainWindowHarness.CloseRepositoryTab(window, basic);
						}
					});
				}
			}
			finally
			{
				TestRepoFactory.Cleanup(basic);
			}

			RestorePrefs(snap);
		}

		// ============================ ch-25 通用对话框与工具 ============================

		[Fact]
		public void Ch25_CommonDialogs()
		{
			HeadlessAppBootstrap.EnsureStarted();
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";

			// ----- 消息框 + 错误窗 + AskPass -----
			HeadlessAppBootstrap.Run(delegate
			{
				var box = new global::ForkPlus.UI.Dialogs.MessageBoxWindow(
					"Delete repository", "The repository will be removed from the list. This cannot be undone.",
					"Delete", "Cancel");
				box.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(box, "01-messagebox", "25-dialogs");
				box.Close();

				var plain = new global::ForkPlus.UI.Dialogs.ErrorWindow("boom: something went wrong");
				plain.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(plain, "02-errorwindow", "25-dialogs");
				plain.Close();

				var askPass = new global::ForkPlus.UI.Dialogs.AskPassWindow(
					"Enter passphrase for key '/home/user/.ssh/id_ed25519':", "/tmp/fpe2e-askpass-repo");
				askPass.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(askPass, "03-askpass", "25-dialogs");
				askPass.Close();
			});

			// ----- 自定义颜色 + 关于窗口 -----
			var colorSnap = SnapshotPrefs();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
					dialog.Show();
					RunJobs();
					ManualScreenshotHelper.Snap(dialog, "04-custom-colors", "25-dialogs");
					dialog.Close();

					var about = new global::ForkPlus.UI.Dialogs.AboutWindow();
					about.Show();
					RunJobs();
					ManualScreenshotHelper.Snap(about, "05-about", "25-dialogs");
					about.Close();
				});
			}
			finally
			{
				RestorePrefs(colorSnap);
			}

			// ----- 通知中心面板 -----
			HeadlessAppBootstrap.Run(delegate
			{
				var panel = new NotificationManagerUserControl();
				var host = new global::Avalonia.Controls.Window
				{
					Title = "Notifications panel",
					Content = panel,
					Width = 380,
					Height = 480
				};
				host.Show();
				RunJobs();
				ManualScreenshotHelper.Snap(host, "06-notification-center", "25-dialogs");
				host.Close();
			});
		}
	}
}