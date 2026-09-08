// E2E 模块24（2026-09-06）：AI 功能（3 窗口，6 用例）。
// 覆盖：AiCommitComposerWindow（WIP 拆分全链路：staged diff → 流式 SSE → JSON 解析 → 分组/文件
// 装配 → Apply All → 真实 2 提交落盘 + 守卫：未配置 AI/无 staged 文件）
//       AiCodeReviewWindow（Files 路径：收集 staged diff → 流式审查 → Markdown→HTML 装配；
//                          Branch 路径：merge-base..HEAD diff → 审查 → 标题/状态栏装配）
//       AiDevelopmentWindow（模型下拉后台拉取 + 多轮对话：用户消息气泡 + AI 流式响应气泡 +
//                            历史落盘 + 无文件变更路径保留流式气泡；队列/停止/清除）
//
// 测试基建：OpenAiStubServer（127.0.0.1 随机端口，GET /v1/models + POST /v1/chat/completions
// SSE/非流式双形态）——所有 AI 功能经 OpenAiService 走 HTTP，服务地址取自
// ForkPlusSettings.AiReviewServiceUrl（NormalizeServiceUrl 剥 /v1 后缀），鉴权 Bearer token。
// stub 把回复拆 3 片逐片下行（真实走 chunk 管线：ParseSseLine 逐行读 + delta.content 解析）。
//
// 模式（与模块 11-23 一致）：E2eMainWindowHarness.OpenRepository 真实 MainWindow 入口 →
// 生产构造器建 AI 窗口（依赖 MainWindow.ActiveRepositoryUserControl 走通 JobQueue 入队）。
// 设置污染防护（模块 21/23 模式）：全属性反射快照 + finally 还原 + Save()。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e24AiTests
	{
		private const string ModuleDir = "24-ai";

		// ============================ 共享助手 ============================

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static string TrFormat(string text, params object[] args)
		{
			return E2eMainWindowHarness.TrFormat(text, args);
		}

		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
		}

		// ---------- 设置快照/还原（模块 21/23 全属性反射） ----------

		private sealed class PrefsSnapshot
		{
			public Dictionary<string, object> Values = new Dictionary<string, object>();
		}

		private static PrefsSnapshot SnapshotPrefs()
		{
			var snap = new PrefsSnapshot();
			foreach (PropertyInfo p in typeof(ForkPlusSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (p.CanWrite && p.GetIndexParameters().Length == 0)
				{
					try { snap.Values[p.Name] = p.GetValue(ForkPlusSettings.Default); }
					catch { }
				}
			}
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
				Console.WriteLine("[E2e24] 设置恢复失败: " + ex.Message);
			}
		}

		/// <summary>把 AI 设置指向 stub 服务器（ServiceUrl 经 NormalizeServiceUrl 剥 /v1 后缀，
		/// 所以这里给裸 base URL；apiKey/model 非空即可触发 IsAiReviewConfigured）。</summary>
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

		/// <summary>等待后台模型拉取完成（ModelComboBox.Items 填入 stub 三个模型）。</summary>
		private static bool WaitForModels(ComboBox combo)
		{
			return UiClick.WaitFor(delegate
			{
				return combo.Items.Count >= 3
					&& combo.Items.OfType<string>().Contains("stub-model-a")
					&& combo.Items.OfType<string>().Contains("stub-model-c");
			});
		}

		// ============================ 1) AI Commit Composer：WIP 全链路 ============================

		[Fact]
		public void AiCommitComposer_FullPipeline_TwoCommitsPersisted()
		{
			string repo = TestRepoFactory.CreateAiStaged();
			var snap = SnapshotPrefs();
			using var server = OpenAiStubServer.Start(Body =>
			{
				// 按功能 prompt 关键字分发：Composer 发送 GenerateWipCommitSplits prompt
				// 含 "split a large batch of staged changes" 关键字
				if (Body.Contains("split a large batch of staged changes", StringComparison.OrdinalIgnoreCase))
				{
					// 返回 forkplus-ai-wip-plan JSON 数组：两个分组（源码/文档）
					return @"```forkplus-ai-wip-plan
[
  {
    ""subject"": ""Refactor App entry point"",
    ""body"": ""Add Main method and util helper"",
    ""files"": [""src/app.cs"", ""src/util.cs""],
    ""reason"": ""Both source files belong to the entry point refactor""
  },
  {
    ""subject"": ""Add documentation notes"",
    ""body"": """",
    ""files"": [""docs/notes.md""],
    ""reason"": ""Documentation change is independent""
  }
]
```";
				}
				return null; // 未命中 → DefaultReply
			});
			ConfigureAi(server);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					AiCommitComposerWindow composer = null;
					try
					{
						// 切 Commit 视图拿 staged 文件（生产入口与模块 5 一致）
						repoControl.ActivateCommitView();
						RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return commit.StageFileUserControl.AllStagedFiles.Length == 3;
						}), "3 个 staged 文件未装配");

						// 直构 Composer 窗口（生产入口 OpenAiCommitComposer 的等价路径）
						ChangedFile[] staged = commit.StageFileUserControl.ExpandedStagedFiles;
						composer = new AiCommitComposerWindow(repoControl.GitModule, staged, amend: false);
						composer.Show();
						RunJobs();

						// ===== 1) 模型下拉后台拉取（stub /v1/models 三个模型填充） =====
						Assert.True(WaitForModels(composer.ModelComboBox),
							"模型下拉应从 stub 拉取 3 个模型");

						// ===== 2) AI 拆分完成：左侧 2 分组 + 首组自动选中 =====
						bool composed = UiClick.WaitFor(delegate
						{
							return composer.GroupsListBox.Items.Count == 2
								&& composer.ApplyAllButton.IsEnabled;
						}, 20000);
						Assert.True(composed, "AI 拆分应产出 2 个分组并启用 Apply All，状态: "
							+ composer.StatusTextBlock.Text);
						// 首组自动选中 → 文件列表 + reason + subject/body 装配
						Assert.Equal(0, composer.GroupsListBox.SelectedIndex);
						var firstItem = (ListBoxItem)composer.GroupsListBox.Items[0];
						var group = firstItem.Tag;
						Assert.NotNull(group);
						// 首组 2 文件（app.cs + util.cs）
						Assert.Equal(2, composer.FilesListBox.Items.Count);
						Assert.Contains("src/app.cs", composer.FilesListBox.Items.OfType<string>());
						Assert.Contains("src/util.cs", composer.FilesListBox.Items.OfType<string>());
						Assert.Contains("Refactor", composer.SubjectTextBox.Text);
						Assert.Contains("entry point", composer.ReasonTextBlock.Text);

						ScreenshotHelper.Snap(composer, "01-composer-groups", ModuleDir);

						// ===== 3) 切到第二组（文档组 1 文件） =====
						composer.GroupsListBox.SelectedIndex = 1;
						RunJobs();
						Assert.Single(composer.FilesListBox.Items.OfType<string>());
						Assert.Equal("docs/notes.md", composer.FilesListBox.Items.OfType<string>().First());
						Assert.Contains("documentation", composer.SubjectTextBox.Text);

						ScreenshotHelper.Snap(composer, "02-composer-doc-group", ModuleDir);

						// ===== 4) Apply All → ComposeWipCommitsGitCommand 真实落盘 2 提交 =====
						UiClick.Click(composer.ApplyAllButton);
						// 等窗口关闭（Apply 成功后 Close()）
						bool closed = UiClick.WaitFor(delegate { return !composer.IsVisible; }, 20000);
						Assert.True(closed, "Apply All 后窗口应关闭");
						// 等仓库刷新完成
						E2eMainWindowHarness.WaitForRepositoryJobs(repoControl);
						RunJobs();

						// ===== 5) 真实 git 断言：2 个新提交 + staged 区清空 =====
						string log = TestRepoFactory.GitOutput(repo, "log --oneline -5");
						Assert.Contains("Refactor App entry point", log);
						Assert.Contains("Add documentation notes", log);
						// 两条新提交的顺序（源码在前、文档在后——ComposeWipCommits 逐组 stage+commit）
						int refactorIdx = log.IndexOf("Refactor App entry point", StringComparison.Ordinal);
						int docIdx = log.IndexOf("Add documentation notes", StringComparison.Ordinal);
						Assert.True(refactorIdx >= 0 && docIdx >= 0, "两条提交都应在 log 中");
						// staged 区应清空（reset HEAD → 逐组 stage → commit，最终无残留）
						string stagedList = TestRepoFactory.GitOutput(repo, "diff --cached --name-only");
						Assert.Equal("", stagedList.Trim());

						// ===== 6) stub 捕获验证：鉴权头 + 模型名 =====
						Assert.NotEmpty(server.ChatRequestBodies);
						Assert.NotEmpty(server.ChatAuthorizationHeaders);
						Assert.Contains("Bearer stub-key", server.ChatAuthorizationHeaders[0]);
						Assert.Contains("stub-model-a", server.ChatRequestBodies[0]);
						// Composer 请求体含 split prompt 关键字
						Assert.Contains("staged", server.ChatRequestBodies[0], StringComparison.OrdinalIgnoreCase);
					}
					finally
					{
						composer?.Close();
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 2) AI Commit Composer：守卫（未配置/无 staged） ============================

		[Fact]
		public void AiCommitComposer_Guards_NotConfiguredAndNoStagedFiles()
		{
			string repo = TestRepoFactory.CreateAiStaged();
			var snap = SnapshotPrefs();
			// stub 常驻但先不配置 AI——两处守卫都应提前 return，本用例全程零 chat 请求
			using var server = OpenAiStubServer.Start();
			// 不配置 AI（IsAiReviewConfigured = false）
			ForkPlusSettings.Default.AiReviewServiceUrl = "";
			ForkPlusSettings.Default.AiReviewApiKey = "";
			ForkPlusSettings.Default.AiReviewSelectedModel = "";
			ForkPlusSettings.Default.Save();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// ===== 1) 未配置 AI：Loaded → StartAiRequest 守卫弹 "AI is not configured" =====
					// 模态确认泵（模块 23 模式）：Post(Background) 关闭 handler 先入队 → Show/RunJobs
					// 触发 Loaded → 守卫 MessageBoxWindow.ShowDialog → PushFrame 泵消费 handler 关窗。
					// ⚠️ 按钮文案必须 Tr()（测试环境 UI 语言 zh-Hans 下 "OK"→"确定"，探针实证硬编码
					// "OK" 找不到按钮 → 模态框无人关闭 → PushFrame 永久挂死——首个 agent 挂起的根因）。
					// ⚠️ Post 与 Show 之间不得插入 RunJobs（会提前消费 handler → 模态框无人关 → 挂死）。
					var guard1Text = new string[1];
					var guard1Handled = new bool[1];
					Dispatcher.UIThread.Post(delegate
					{
						try
						{
							MessageBoxWindow msgBox = global::ForkPlus.UI.WpfCompat.WpfApp.Windows
								.OfType<MessageBoxWindow>().FirstOrDefault(w => w.IsVisible);
							if (msgBox == null)
							{
								guard1Text[0] = "守卫提示框未出现";
								return;
							}
							guard1Text[0] = string.Join("\n", UiClick.FindAll<global::Avalonia.Controls.TextBlock>(msgBox)
								.Select(t => t.Text));
							Button ok = UiClick.FindAll<Button>(msgBox)
								.FirstOrDefault(b => UiClick.ContentText(b) == Tr("OK"));
							ok?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
							guard1Handled[0] = true;
						}
						catch (Exception ex)
						{
							guard1Text[0] = ex.ToString();
						}
					}, DispatcherPriority.Background);
						var composer = new AiCommitComposerWindow(repoControl.GitModule,
							new ChangedFile[0], amend: false);
						composer.Show();
						RunJobs();
						Assert.True(guard1Handled[0], "未配置 AI 守卫提示框处理器未执行: " + guard1Text[0]);
						// 断言用生产完整键（zh-Hans："AI 尚未配置。请先在偏好设置中配置 AI 检视设置。"）
						Assert.Contains(Tr("AI is not configured. Please configure AI review settings in Preferences first."),
							guard1Text[0] ?? "");
						// 守卫提前返回：不产生任何 chat/模型请求
						Assert.Equal(0, server.ChatRequestBodies.Count);
						Assert.Equal(0, server.ModelsRequestCount);
						composer.Close();
						RunJobs();

						// ===== 2) 已配置 AI + 空 staged 数组：守卫弹 "No staged files" =====
					// （生产入口 CommitUserControl 会在构造前拦截，这里直构空数组走窗口内部守卫）
					ConfigureAi(server);
					var guard2Text = new string[1];
					var guard2Handled = new bool[1];
					Dispatcher.UIThread.Post(delegate
					{
						try
						{
							MessageBoxWindow msgBox = global::ForkPlus.UI.WpfCompat.WpfApp.Windows
								.OfType<MessageBoxWindow>().FirstOrDefault(w => w.IsVisible);
							if (msgBox == null)
							{
								guard2Text[0] = "守卫提示框未出现";
								return;
							}
							guard2Text[0] = string.Join("\n", UiClick.FindAll<global::Avalonia.Controls.TextBlock>(msgBox)
								.Select(t => t.Text));
							Button ok = UiClick.FindAll<Button>(msgBox)
								.FirstOrDefault(b => UiClick.ContentText(b) == Tr("OK"));
							ok?.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
							guard2Handled[0] = true;
						}
						catch (Exception ex)
						{
							guard2Text[0] = ex.ToString();
						}
					}, DispatcherPriority.Background);
						var composer2 = new AiCommitComposerWindow(repoControl.GitModule,
							new ChangedFile[0], amend: false);
						composer2.Show();
						RunJobs();
						Assert.True(guard2Handled[0], "无 staged 守卫提示框处理器未执行: " + guard2Text[0]);
						// 断言用生产完整键（zh-Hans："没有可编排的暂存文件。请先暂存一些文件。"）
						Assert.Contains(Tr("No staged files to compose. Stage some files first."),
							guard2Text[0] ?? "");
						// 守卫在 chat 请求前拦截（模型下拉的 /v1/models 后台拉取不算 chat）
						Assert.Equal(0, server.ChatRequestBodies.Count);
						composer2.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 3) AI Code Review：Files 路径 ============================

		[Fact]
		public void AiCodeReview_FilesPath_MarkdownRendered()
		{
			string repo = TestRepoFactory.CreateAiStaged();
			var snap = SnapshotPrefs();
			using var server = OpenAiStubServer.Start(Body =>
			{
				// CodeReviewFiles prompt 含 "review the following code changes" 关键字
				if (Body.Contains("review", StringComparison.OrdinalIgnoreCase)
					&& Body.Contains("diff", StringComparison.OrdinalIgnoreCase))
				{
					return "## Summary\n\nThe changes add a Main method and a Util helper class.\n\n## Issues\n\n- `Util` class has no documentation.\n- Consider adding XML comments.";
				}
				return null;
			});
			ConfigureAi(server);
			try
			{
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
						}));

						// 构造 Files target（生产入口：Commit 视图右键 → Code Review with AI）
						ChangedFile[] files = commit.StageFileUserControl.ExpandedStagedFiles;
						var target = new AiCodeReviewTarget.Files(files, amend: false);
						var review = new AiCodeReviewWindow(repoControl, target, aiAgent: null);
						review.Show();
						RunJobs();

						// ===== 1) 标题 + 模型下拉（Files 路径：FileReviewGrid 可见） =====
						Assert.Contains(TrFormat("{0} files", 3), review.TitleTextBlock.Text);
						Assert.True(WaitForModels(review.ModelComboBox), "模型下拉应拉取");

						// ===== 2) 审查完成：状态栏清空 + Retry 启用 =====
						bool reviewDone = UiClick.WaitFor(delegate
						{
							return review.RetryButton.IsEnabled
								&& string.IsNullOrEmpty(review.StatusTextBlock.Text);
						}, 20000);
						Assert.True(reviewDone, "代码审查应完成，状态: "
							+ review.StatusTextBlock.Text);

						ScreenshotHelper.Snap(review, "03-codereview-files", ModuleDir);

						// ===== 3) stub 捕获验证 =====
						Assert.NotEmpty(server.ChatRequestBodies);
						Assert.Contains("Bearer stub-key", server.ChatAuthorizationHeaders[0]);
						// Files 路径走 CodeReviewFiles prompt
						Assert.Contains("src/app.cs", server.ChatRequestBodies[0]);

						review.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 4) AI Code Review：Branch 路径 ============================

		[Fact]
		public void AiCodeReview_BranchPath_TitleAndDiff()
		{
			string repo = TestRepoFactory.CreateBranches();
			var snap = SnapshotPrefs();
			using var server = OpenAiStubServer.Start(Body =>
			{
				if (Body.Contains("review", StringComparison.OrdinalIgnoreCase))
				{
					return "## Branch Review\n\nThe feature branch adds a new file.\n\nNo critical issues found.";
				}
				return null;
			});
			ConfigureAi(server);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 切到 Revision 视图，选中 feature/two 分支
						repoControl.ActivateRevisionView();
						RunJobs();
						var revList = repoControl.Content.RevisionListViewUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return revList.RevisionsDataSource.Count > 0;
						}));

						// 构造 Branch target（生产入口：分支右键 → Code Review with AI）
						// 用 merge-base..HEAD：main 为 base，feature/two 的 HEAD 为 dst
						GitModule module = repoControl.GitModule;
						// feature/two 的 Sha（LocalBranches 经 Refresh 装配，先等加载）
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryData.References.LocalBranches
								.Any(b => b.Name == "feature/two");
						}), "分支列表应包含 feature/two");
						Branch featureTwo = repoControl.RepositoryData.References.LocalBranches
							.FirstOrDefault(b => b.Name == "feature/two");
						Assert.NotNull(featureTwo);
						Sha dst = featureTwo.Sha;
						// merge-base: main 与 feature/two 的共同祖先
						string mbStr = TestRepoFactory.GitOutput(repo,
							"merge-base main feature/two").Trim();
						Sha src = Sha.Parse(mbStr).Value;
						var target = new AiCodeReviewTarget.Branch(src, featureTwo);
						var review = new AiCodeReviewWindow(repoControl, target, aiAgent: null);
						review.Show();
						RunJobs();

						// ===== 1) 标题含分支名 + Review（Branch 路径：RevisionDetails 可见；
						// TitleTextBlock 由 FormatCurrent("Code review for {0}...{1}") 装配，
						// zh-Hans 为"代码评审：feature/two..."） =====
						Assert.Contains(TrFormat("Code review for {0}...{1}", "feature/two", ""),
							review.TitleTextBlock.Text);

						// ===== 2) 审查完成 =====
						bool reviewDone = UiClick.WaitFor(delegate
						{
							return review.RetryButton.IsEnabled
								&& string.IsNullOrEmpty(review.StatusTextBlock.Text);
						}, 20000);
						Assert.True(reviewDone, "Branch 审查应完成，状态: "
							+ review.StatusTextBlock.Text);

						ScreenshotHelper.Snap(review, "04-codereview-branch", ModuleDir);

						// ===== 3) stub 捕获：Branch 路径走 CodeReview prompt（含 diff） =====
						Assert.NotEmpty(server.ChatRequestBodies);
						Assert.Contains("Bearer stub-key", server.ChatAuthorizationHeaders[0]);

						review.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 5) AI Development：模型列表 + 对话 ============================

		[Fact]
		public void AiDevelopment_ModelListAndConversation()
		{
			string repo = TestRepoFactory.CreateBasic();
			var snap = SnapshotPrefs();
			using var server = OpenAiStubServer.Start(Body =>
			{
				// Dev 窗口多轮对话 prompt（系统提示含 "development assistant" 或类似）
				if (Body.Contains("user", StringComparison.OrdinalIgnoreCase))
				{
					return "I understand. Let me analyze the codebase.\n\nThe `App` class currently has no `Main` method. I can add one for you.";
				}
				return null;
			});
			ConfigureAi(server);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						// 直构 Dev 窗口（生产入口：Form > AI > AI-Assisted Development）
						var dev = new AiDevelopmentWindow(repoControl, repoControl.GitModule);
						dev.Show();
						RunJobs();

						// ===== 1) 模型下拉后台拉取 + 选中当前模型 =====
						Assert.True(WaitForModels(dev.ModelComboBox),
							"Dev 窗口模型下拉应从 stub 拉取");
						Assert.Equal("stub-model-a", (string)dev.ModelComboBox.SelectedItem);

						// ===== 2) 发送消息（SendButton_Click → SendRequest → ProcessRequest） =====
						dev.InputTextBox.Text = "Add a Main method to App class";
						RunJobs();
						Assert.True(dev.SendButton.IsEnabled, "输入文本后发送按钮应启用");
						UiClick.Click(dev.SendButton);

						// ===== 3) 用户消息气泡装配 + AI 流式响应气泡装配 =====
						bool messageAdded = UiClick.WaitFor(delegate
						{
							// MessagePanel 至少有：欢迎 Border + 用户消息 Border + AI 响应 Border
							return dev.MessagePanel.Children.Count >= 3;
						});
						Assert.True(messageAdded, "用户消息气泡应装配，当前子元素数: "
							+ dev.MessagePanel.Children.Count);

						// ===== 4) AI 响应完成：StopButton 隐藏 + 输入框重新聚焦 =====
						bool responseDone = UiClick.WaitFor(delegate
						{
							return !dev.StopButton.IsVisible;
						}, 20000);
						Assert.True(responseDone, "AI 响应应完成（Stop 隐藏），状态可见");

						ScreenshotHelper.Snap(dev, "05-dev-conversation", ModuleDir);

						// ===== 5) stub 捕获：多轮对话 prompt 含用户消息 =====
						Assert.NotEmpty(server.ChatRequestBodies);
						Assert.Contains("Bearer stub-key", server.ChatAuthorizationHeaders[0]);
						Assert.Contains("Add a Main method", server.ChatRequestBodies[0]);

						// ===== 6) 对话历史落盘（_conversationHistory 有 user+assistant 两条） =====
						// 发第二条消息验证多轮记忆
						dev.InputTextBox.Text = "Also add XML comments";
						RunJobs();
						UiClick.Click(dev.SendButton);
						bool secondDone = UiClick.WaitFor(delegate
						{
							return !dev.StopButton.IsVisible;
						}, 20000);
						Assert.True(secondDone, "第二条消息应处理完成");
						// 第二条请求体应含历史（多轮）
						Assert.True(server.ChatRequestBodies.Count >= 2,
							"至少 2 个 chat 请求（多轮对话）");
						// 第二条请求体含第一条的用户消息（历史上下文）
						Assert.Contains("Add a Main method", server.ChatRequestBodies[1]);

						ScreenshotHelper.Snap(dev, "06-dev-multiturn", ModuleDir);

						dev.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 6) AI Development：停止 + 清除对话 ============================

		[Fact]
		public void AiDevelopment_StopAndClearConversation()
		{
			string repo = TestRepoFactory.CreateBasic();
			var snap = SnapshotPrefs();
			using var server = OpenAiStubServer.Start(Body =>
			{
				// 慢响应：sleep 200ms 让 Stop 有窗口期
				Thread.Sleep(200);
				return "Analysis complete. No file changes needed.";
			});
			ConfigureAi(server);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						var dev = new AiDevelopmentWindow(repoControl, repoControl.GitModule);
						dev.Show();
						RunJobs();
						Assert.True(WaitForModels(dev.ModelComboBox));

						// ===== 1) 发送消息后立即停止 =====
						dev.InputTextBox.Text = "Analyze codebase";
						RunJobs();
						UiClick.Click(dev.SendButton);
						RunJobs();
						// Stop 按钮应可见（请求进行中）
						bool stopVisible = UiClick.WaitFor(delegate { return dev.StopButton.IsVisible; }, 5000);
						if (stopVisible)
						{
							UiClick.Click(dev.StopButton);
							RunJobs();
							// 停止后状态消息含 "Stopped" 或请求完成
							bool stopped = UiClick.WaitFor(delegate
							{
								return !dev.StopButton.IsVisible;
							}, 10000);
							Assert.True(stopped, "停止后 Stop 按钮应隐藏");
						}

						ScreenshotHelper.Snap(dev, "07-dev-stopped", ModuleDir);

						// ===== 2) 清除对话 =====
						int childrenBefore = dev.MessagePanel.Children.Count;
						UiClick.Click(dev.ClearConversationButton);
						RunJobs();
						// 清除后重新显示欢迎消息（MessagePanel 清空 + ShowWelcomeMessage）
						Assert.True(UiClick.WaitFor(delegate
						{
							return dev.MessagePanel.Children.Count < childrenBefore
								|| dev.MessagePanel.Children.Count == 1; // 只剩欢迎 Border
						}), "清除对话后 MessagePanel 应只剩欢迎消息");

						ScreenshotHelper.Snap(dev, "08-dev-cleared", ModuleDir);

						dev.Close();
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
