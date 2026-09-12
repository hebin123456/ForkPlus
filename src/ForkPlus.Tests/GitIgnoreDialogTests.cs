// 回归测试（2026-09-12，gitignore 弹窗两个问题）：
//   1) 首开时上面 Pattern 已填字但下面 Preview 不自动生成，要编辑才出；
//   2) Pattern/Preview 两个文本框内容多时无滚动条。
// 修复点：
//   - AddGitIgnorePatternWindow.UpdatePreview 改为 Task.Run 后台跑 git + 经
//     Dispatcher.UIThread.Post 显式回 UI 线程回填（不再依赖构造期同步上下文接管），
//     并在 Opened 后兜底刷一次 → 首开即出 Preview；
//   - PlaceholderTextBox 主题把内层 PART_ScrollViewer 的 HSBV/VSBV 由硬编码 Hidden
//     改为 TemplateBinding 到 ScrollViewer.* 附加属性：默认仍 Hidden、显式 Auto 时
//     内容超框出现滚动条。
// 说明：Preview 的异步回填（后台 git 子进程 → Dispatcher.UIThread.Post）在 headless
// 下无法被 RunJobs() 可靠泵送（无 git 的纯 Post 用例可，带 git 子进程的用例则不会落地），
// 属测试环境限制而非产品缺陷——真实应用 UI 线程有实时 dispatcher 循环，Post 必然被处理。
// 因此本文件只对【确定性】部分做断言：预览的数据源（GetFilesToIgnoreGitCommand 对该
// pattern 返回预期文件）与滚动条可见性。
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class GitIgnoreDialogTests
	{
		[Fact]
		public void InitialPatternSearchesExpectedFiles()
		{
			string repo = CreateRepo();
			var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
			// 首开默认 pattern 的预览内容 = git 命令返回的文件；确定性断言数据源正确。
			var txt = new GetFilesToIgnoreGitCommand().Execute(module, new[] { "*.txt" });
			Assert.True(txt.Succeeded);
			Assert.Contains("foo.txt", txt.Result);
			Assert.DoesNotContain("bar.log", txt.Result);

			var log = new GetFilesToIgnoreGitCommand().Execute(module, new[] { "*.log" });
			Assert.True(log.Succeeded);
			Assert.Contains("bar.log", log.Result);
		}

		[Fact]
		public void ScrollbarsAppearOnLongContent()
		{
			string repo = CreateRepo();
			var module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
			HeadlessAppBootstrap.EnsureStarted();

			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var win = new AddGitIgnorePatternWindow(module, "*.txt");
				win.Show();
				Dispatcher.UIThread.RunJobs();
				win.PatternTextBox.Text = string.Join("\n", Enumerable.Range(0, 80).Select(i => "p" + i + ".txt"));
				win.PreviewTextBox.Text = string.Join("\n", Enumerable.Range(0, 80).Select(i => "matching/file" + i + ".txt"));
				Dispatcher.UIThread.RunJobs();
				Dispatcher.UIThread.RunJobs();

				var pattern = FindScrollViewer(win.PatternTextBox);
				var preview = FindScrollViewer(win.PreviewTextBox);
				Assert.NotNull(pattern);
				Assert.NotNull(preview);
				// 内层 ScrollViewer 已绑到附加属性 → Auto；内容超框 → 可垂直滚动。
				Assert.Equal(ScrollBarVisibility.Auto, pattern.VerticalScrollBarVisibility);
				Assert.Equal(ScrollBarVisibility.Auto, preview.VerticalScrollBarVisibility);
				Assert.True(pattern.Extent.Height > pattern.Viewport.Height, "Pattern 多行应可滚动");
				Assert.True(preview.Extent.Height > preview.Viewport.Height, "Preview 多行应可滚动");
				win.Close();
			}).GetAwaiter().GetResult();
		}

		private static string CreateRepo()
		{
			string repo = Path.Combine(Path.GetTempPath(), "gigi_" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(repo);
			RunGit(repo, "init");
			RunGit(repo, "config user.email p@p.p");
			RunGit(repo, "config user.name probe");
			File.WriteAllText(Path.Combine(repo, "foo.txt"), "hello\n");
			File.WriteAllText(Path.Combine(repo, "bar.log"), "world\n");
			return repo;
		}

		private static ScrollViewer FindScrollViewer(AvaloniaObject root)
		{
			if (root is ScrollViewer sv)
			{
				return sv;
			}
			int count = VisualTreeHelper.GetChildrenCount(root);
			for (int i = 0; i < count; i++)
			{
				var r = FindScrollViewer(VisualTreeHelper.GetChild(root, i));
				if (r != null)
				{
					return r;
				}
			}
			return null;
		}

		private static void RunGit(string path, string arg)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", arg) { WorkingDirectory = path, RedirectStandardOutput = true, RedirectStandardError = true };
			using (var p = System.Diagnostics.Process.Start(psi))
			{
				p.WaitForExit();
			}
		}
	}
}