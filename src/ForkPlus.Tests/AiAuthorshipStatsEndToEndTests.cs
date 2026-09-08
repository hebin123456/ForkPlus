// 端到端（修复7：同步原版 ForkPlus 3.13.x git-ai 特性）：
// 真实 git-ai（checkpoint mock_ai 造 AI 归属数据）+ 真实 git 仓库 → StatisticsUserControl
// AI Authorship 统计区（JobQueue → GetGitAiStatsGitCommand → 饼图/细分列表/摘要）。
// git-ai 未安装的环境自动跳过（FindGitAi 返回 null），不影响其它环境跑全量。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class AiAuthorshipStatsEndToEndTests
	{
		/// <summary>找 git-ai 可执行文件：PATH → ~/.local/bin → /root/.local/bin；找不到返回 null（跳过测试）。</summary>
		[Null]
		private static string FindGitAi()
		{
			string name = OperatingSystem.IsWindows() ? "git-ai.exe" : "git-ai";
			string fromPath = Environment.GetEnvironmentVariable("PATH")?
				.Split(Path.PathSeparator)
				.Select(p => Path.Combine(p.Trim(), name))
				.FirstOrDefault(p => File.Exists(p));
			if (fromPath != null)
			{
				return fromPath;
			}
			string[] candidates = new string[2]
			{
				Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", name),
				Path.Combine("/root", ".local", "bin", name)
			};
			return candidates.FirstOrDefault(File.Exists);
		}

		/// <summary>建测试仓库：1 个人类提交 + 1 个 mock_ai checkpoint 提交（+2 AI 行）。</summary>
		private static string CreateAiTestRepo(string gitAi)
		{
			string root = Path.Combine(Path.GetTempPath(), "fpaistats_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			string oldDir = Directory.GetCurrentDirectory();
			try
			{
				Directory.SetCurrentDirectory(root);
			Run("git", "init -q");
			Run("git", "config user.email test@example.com");
			Run("git", "config user.name Test");
			// GetRepositoryStatsGitCommand 原版基线：提交数 <= 2 视为 "no changes" 不出统计，
			// 故先造 3 个人类提交让主统计可用，再做 AI checkpoint 提交。
			for (int i = 1; i <= 3; i++)
			{
				File.WriteAllText(Path.Combine(root, "human.txt"), "human line " + i + "\nhuman line 2\n");
				Run("git", "add human.txt");
				Run("git", "commit -q -m human-" + i);
			}
			File.WriteAllText(Path.Combine(root, "ai_generated.py"), "ai line 1\nai line 2\n");
				Run("git", "add ai_generated.py");
				// 注册 AI 归属（mock_ai preset）后提交；authorship note 由 git-ai 后台写入 refs/notes/ai
				Run(gitAi, "checkpoint mock_ai");
				Thread.Sleep(500);
				Run("git", "commit -q -m ai-stuff");
				// 等 note 落盘（后台服务异步写），stats 里 ai_additions 应为 2
				for (int i = 0; i < 20; i++)
				{
					Thread.Sleep(500);
					string probe = RunCapture(gitAi, "stats HEAD --json");
					if (probe != null && probe.Contains("\"ai_additions\":2"))
					{
						return root;
					}
				}
				throw new Exception("git-ai authorship note 未在 10s 内出现（stats HEAD: " +
					RunCapture(gitAi, "stats HEAD --json") + "）");
			}
			finally
			{
				Directory.SetCurrentDirectory(oldDir);
			}
		}

		private static void Run(string exe, string args)
		{
			var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = Directory.GetCurrentDirectory()
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception(exe + " " + args + " 失败: " + err);
			}
		}

		[Null]
		private static string RunCapture(string exe, string args)
		{
			var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = Directory.GetCurrentDirectory()
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string stdout = p.StandardOutput.ReadToEnd();
			p.StandardError.ReadToEnd();
			p.WaitForExit();
			return p.ExitCode == 0 ? stdout : null;
		}

		[Fact]
		public void StatisticsControl_AiAuthorship_ShowsAiDataAndBreakdown()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string gitAi = FindGitAi();
			if (gitAi == null)
			{
				return; // 环境无 git-ai，跳过（不 fail，保持全量套件可跑）
			}
			string repoRoot = CreateAiTestRepo(gitAi);
			string savedGitAiPath = ForkPlusSettings.Default.GitAiInstancePath;
			bool savedAttribution = ForkPlusSettings.Default.AiAttributionEnabled;
			try
			{
				ForkPlusSettings.Default.GitAiInstancePath = gitAi;
				ForkPlusSettings.Default.AiAttributionEnabled = true;
				var module = new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);

				string[] summaryHolder = new string[1];
				string[] errorHolder = new string[1];
				string[] breakdownHolder = new string[1];
				string[] fallbackHolder = new string[1];
				bool[] statsVisibleHolder = new bool[1];
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					var control = new StatisticsUserControl();
					var scrollViewer = new ScrollViewer { Content = control };
					var window = new Window { Width = 1200, Height = 900, Content = scrollViewer };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					control.ShowStatistics(module);

					// 轮询等 AI 统计任务完成（JobQueue 后台线程 → Dispatcher.Post 回投），
					// 同时等仓库统计完成（StatsContainer 可见，否则截图只有 Fallback）
					for (int i = 0; i < 150; i++)
					{
						TaskDelay(200);
						Dispatcher.UIThread.RunJobs();
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						string summary = control.AiStatsSummary.Text;
						// 4 提交：human-1(+2 人类) + human-2/3(各 +1) + ai-stuff(+2 AI) → 2/4 = 50%
						// 注意：摘要里是全角括号（50%），统一只匹配 "50%" 避免半/全角坑
						bool done = summary != null
							&& summary.IndexOf("50%", StringComparison.Ordinal) >= 0
							&& control.StatsContainer.IsVisible;
						if (done || control.AiStatsError.IsVisible)
						{
							break;
						}
					}
					summaryHolder[0] = control.AiStatsSummary.Text;
				errorHolder[0] = control.AiStatsError.IsVisible ? control.AiStatsError.Text : null;
				statsVisibleHolder[0] = control.StatsContainer.IsVisible;
				fallbackHolder[0] = control.FallbackUserControl.FallbackTitle;
				var items = control.AiBreakdownListBox.ItemsSource as List<StatisticsUserControl.AiAgentViewModel>;
					breakdownHolder[0] = items == null ? "<null>" : string.Join(" | ", items.Select(x => x.Name + "=" + x.AiLines + "/" + x.Accepted + " " + x.Share));

					// 渲染一帧存证据（docs/evidence/fix7-ai-authorship.png；headless 无渲染时静默跳过）。
				// AI Authorship 在页面底部（Row 7），先把 ScrollViewer 滚到底再截图，否则只有顶部。
					try
					{
						scrollViewer.ScrollToEnd();
						Dispatcher.UIThread.RunJobs();
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						using (var frame = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window))
						{
							if (frame != null)
							{
								string evidenceDir = FindEvidenceDir();
								if (evidenceDir != null)
								{
									frame.Save(Path.Combine(evidenceDir, "fix7-ai-authorship.png"));
								}
							}
						}
					}
					catch
					{
					}
					window.Close();
					return 0;
				}).GetAwaiter().GetResult();

				Assert.True(statsVisibleHolder[0],
					"仓库统计区未显示（StatsContainer 仍 Collapsed）。Fallback=" + fallbackHolder[0]
					+ "；AI摘要=" + summaryHolder[0] + "；AI错误=" + (errorHolder[0] ?? "<无>"));
				Assert.Null(errorHolder[0]);
				Assert.NotNull(summaryHolder[0]);
				Assert.Contains("50%", summaryHolder[0]); // 全角括号（50%），只匹配 50% 避免半/全角坑
				// 细分列表应有 mock_ai 条目（model=unknown 被 DisplayName 抑制，只剩工具名）
				Assert.NotNull(breakdownHolder[0]);
				Assert.Contains("mock_ai", breakdownHolder[0]);
				Assert.Contains("2", breakdownHolder[0]);
			}
			finally
			{
				ForkPlusSettings.Default.GitAiInstancePath = savedGitAiPath;
				ForkPlusSettings.Default.AiAttributionEnabled = savedAttribution;
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}

		private static void TaskDelay(int ms)
		{
			System.Threading.Tasks.Task.Delay(ms).GetAwaiter().GetResult();
		}

		/// <summary>从测试程序集目录向上找 docs/evidence（仓库根）；找不到返回 null。</summary>
		[Null]
		private static string FindEvidenceDir()
		{
			string dir = Path.GetDirectoryName(typeof(AiAuthorshipStatsEndToEndTests).Assembly.Location);
			while (dir != null)
			{
				string candidate = Path.Combine(dir, "docs", "evidence");
				if (Directory.Exists(candidate))
				{
					return candidate;
				}
				dir = Path.GetDirectoryName(dir);
			}
			return null;
		}
	}
}
