// 基建自检（阶段0，2026-09-05）：验证 TestRepoFactory / ScreenshotHelper / UiClick 三件套可用。
// 这是全模块 E2E 的前置闸门——基建坏了后面 25 个模块全废。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2eInfraSmokeTests
	{
		[Fact]
		public void TestRepoFactory_Basic_CreatesValidRepo()
		{
			string root = TestRepoFactory.CreateBasic();
			try
			{
				Assert.True(Directory.Exists(Path.Combine(root, ".git")));
				Assert.True(File.Exists(Path.Combine(root, "a.txt")));
				string content = File.ReadAllText(Path.Combine(root, "a.txt"));
				Assert.Contains("line4-appended", content); // 未暂存修改存在
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		[Fact]
		public void TestRepoFactory_Branches_CreatesBranchesAndTags()
		{
			string root = TestRepoFactory.CreateBranches();
			try
			{
				string branches = GitOut(root, "branch");
				Assert.Contains("feature/one", branches);
				Assert.Contains("feature/two", branches);
				string tags = GitOut(root, "tag");
				Assert.Contains("v1.0", tags);
				Assert.Contains("v2.0", tags);
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		[Fact]
		public void TestRepoFactory_Conflict_LeavesConflictState()
		{
			string root = TestRepoFactory.CreateConflict();
			try
			{
				string status = GitOut(root, "status");
				Assert.Contains("both modified", status);
				Assert.True(File.Exists(Path.Combine(root, "conflicted.txt")));
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		[Fact]
		public void TestRepoFactory_Stash_HasTwoStashes()
		{
			string root = TestRepoFactory.CreateStash();
			try
			{
				string list = GitOut(root, "stash list");
				Assert.Equal(2, list.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
				Assert.Contains("stash-one", list);
				Assert.Contains("stash-two", list);
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		[Fact]
		public void TestRepoFactory_BareRemote_WorkAheadOfOrigin()
		{
			string work = TestRepoFactory.CreateBareRemote();
			try
			{
				string status = GitOut(work, "status -sb");
				Assert.Contains("ahead 1", status); // 领先一个提交（供 push 测试）
				string remotes = GitOut(work, "remote -v");
				Assert.Contains("remote.git", remotes); // origin 是本地 bare
			}
			finally
			{
				TestRepoFactory.Cleanup(Path.GetDirectoryName(work)); // 删整个 fpe2e_bare_xxx
			}
		}

		[Fact]
		public void TestRepoFactory_Binary_PngIsValidImage()
		{
			string root = TestRepoFactory.CreateBinary();
			try
			{
				byte[] png = File.ReadAllBytes(Path.Combine(root, "img.png"));
				// PNG 魔数
				Assert.Equal(137, png[0]);
				Assert.Equal(80, png[1]);   // P
				Assert.Equal(78, png[2]);   // N
				Assert.Equal(71, png[3]);   // G
			}
			finally
			{
				TestRepoFactory.Cleanup(root);
			}
		}

		[Fact]
		public void ScreenshotHelper_And_UiClick_Smoke()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new ForkPlus.UI.CustomWindow { Width = 400, Height = 300 };
				var button = new Avalonia.Controls.Button { Content = "ClickMe" };
				var text = new TextBlock { Text = "count:0" };
				var panel = new Avalonia.Controls.StackPanel();
				panel.Children.Add(button);
				panel.Children.Add(text);
				window.Content = panel;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				int clicks = 0;
				button.Click += delegate { clicks++; text.Text = "count:" + clicks; };

				// 基建三件套联动：查找 → 点击 → 截图
				var found = UiClick.FindButtonByText(window, "ClickMe");
				Assert.Same(button, found);
				UiClick.Click(found);
				Assert.Equal(1, clicks);
				Assert.Equal("count:1", text.Text);

				int nonBlank = ScreenshotHelper.Snap(window, "00-infra-smoke", "00-infra");
				Assert.True(nonBlank > 200, "截图非空白像素过少: " + nonBlank);
				window.Close();
			});
		}

		private static string GitOut(string cwd, string args)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", args)
			{
				WorkingDirectory = cwd,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string output = p.StandardOutput.ReadToEnd();
			p.WaitForExit();
			return output;
		}
	}
}
