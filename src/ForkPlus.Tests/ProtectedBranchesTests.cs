// WS2.4（受保护分支）测试：设置编解码 round-trip + 匹配辅助方法 + 确认对话框内容。
// - RepositorySettings.ProtectedBranches：Save→Load 往返（含 refs/heads/ 前缀条目原样保留）、
//   默认空数组（旧设置文件无该键）。
// - ProtectedBranchConfirmWindow.MatchProtected/Normalize：短名精确匹配（忽略大小写）、
//   refs/heads/ 前缀剥掉（两侧兼容）、后缀不误命中（"main" 不匹配 "domain"）、去重保序、
//   空输入安全。
// - ProtectedBranchConfirmWindow：headless 打开断言命中分支列表与设置管理提示文案。
using System;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.Dialogs;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ProtectedBranchesTests
	{
		// ============================ 设置编解码 ============================

		[Fact]
		public void RepositorySettings_ProtectedBranches_RoundTrip()
		{
			string repo = TestRepoFactory.CreateClean();
			try
			{
				string[] expected = new string[] { "main", "release/1.0", "refs/heads/dev" };
				GitModule module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				module.Settings.ProtectedBranches = expected;
				module.Settings.Save();
				// 重新从磁盘加载（走 Load → Decode 全链路）
				RepositorySettings reloaded = RepositorySettings.Load(new GitModule(repo, Path.Combine(repo, ".git"), null, null));
				Assert.NotNull(reloaded.ProtectedBranches);
				Assert.Equal(expected, reloaded.ProtectedBranches);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void RepositorySettings_ProtectedBranches_DefaultEmpty()
		{
			string repo = TestRepoFactory.CreateClean();
			try
			{
				// 无设置文件的全新仓库：默认空数组（非 null，四处拦截的空守卫依赖这一点）
				RepositorySettings settings = RepositorySettings.Load(new GitModule(repo, Path.Combine(repo, ".git"), null, null));
				Assert.NotNull(settings.ProtectedBranches);
				Assert.Empty(settings.ProtectedBranches);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 匹配辅助方法 ============================

		[Fact]
		public void MatchProtected_ShortNameExactIgnoreCase()
		{
			string[] protectedBranches = new string[] { "main", "release/1.0" };
			// 精确命中（含层级短名）
			Assert.Equal(new string[] { "main" },
				ProtectedBranchConfirmWindow.MatchProtected(protectedBranches, new string[] { "main" }));
			// 忽略大小写
			Assert.Equal(new string[] { "main" },
				ProtectedBranchConfirmWindow.MatchProtected(protectedBranches, new string[] { "MAIN" }));
			// 不命中：后缀/前缀部分串不算（短名精确匹配）
			Assert.Empty(ProtectedBranchConfirmWindow.MatchProtected(protectedBranches, new string[] { "domain", "ma", "feature/main" }));
			// 不命中：层级路径逐段不匹配
			Assert.Empty(ProtectedBranchConfirmWindow.MatchProtected(protectedBranches, new string[] { "release" }));
		}

		[Fact]
		public void MatchProtected_StripsRefsHeadsPrefixOnBothSides()
		{
			// 设置条目带 refs/heads/ 前缀：剥掉后匹配
			Assert.Equal(new string[] { "main" },
				ProtectedBranchConfirmWindow.MatchProtected(new string[] { "refs/heads/main" }, new string[] { "main" }));
			// 待检分支名是 full ref：剥掉后匹配
			Assert.Equal(new string[] { "main" },
				ProtectedBranchConfirmWindow.MatchProtected(new string[] { "main" }, new string[] { "refs/heads/main" }));
			// 两侧都带前缀
			Assert.Equal(new string[] { "main" },
				ProtectedBranchConfirmWindow.MatchProtected(new string[] { "refs/heads/main" }, new string[] { "refs/heads/Main" }));
		}

		[Fact]
		public void MatchProtected_DedupesAndKeepsOrder()
		{
			// 多分支命中：按输入顺序返回且去重
			Assert.Equal(new string[] { "release/1.0", "main" },
				ProtectedBranchConfirmWindow.MatchProtected(
					new string[] { "main", "release/1.0" },
					new string[] { "refs/heads/release/1.0", "main", "MAIN", "feature/x" }));
		}

		[Fact]
		public void MatchProtected_EmptyInputsAreSafe()
		{
			Assert.Empty(ProtectedBranchConfirmWindow.MatchProtected(null, new string[] { "main" }));
			Assert.Empty(ProtectedBranchConfirmWindow.MatchProtected(new string[0], new string[] { "main" }));
			Assert.Empty(ProtectedBranchConfirmWindow.MatchProtected(new string[] { "main" }, null));
			Assert.Empty(ProtectedBranchConfirmWindow.MatchProtected(new string[] { "main" }, new string[] { null, "", "  " }));
			// 空白条目不产生误命中
			Assert.Empty(ProtectedBranchConfirmWindow.MatchProtected(new string[] { "  " }, new string[] { "main" }));
		}

		[Fact]
		public void Normalize_TrimsAndStripsPrefix()
		{
			Assert.Equal("main", ProtectedBranchConfirmWindow.Normalize("refs/heads/main"));
			Assert.Equal("main", ProtectedBranchConfirmWindow.Normalize("  main  "));
			Assert.Equal("main", ProtectedBranchConfirmWindow.Normalize("REFS/HEADS/main"));
			Assert.Null(ProtectedBranchConfirmWindow.Normalize(null));
			Assert.Equal("", ProtectedBranchConfirmWindow.Normalize(""));
		}

		// ============================ 确认对话框 ============================

		[Fact]
		public void ConfirmWindow_ListsBranchesAndSettingsHint()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ProtectedBranchConfirmWindow dialog = new ProtectedBranchConfirmWindow(new string[] { "main", "release/1.0" });
				dialog.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					Assert.Equal(2, dialog.BranchesItemsControl.ItemCount);
					Assert.Equal("main", (string)dialog.BranchesItemsControl.ItemsSource.OfType<object>().First());
					Assert.Equal(E2eMainWindowHarness.Tr("You can manage protected branches in the repository settings."),
						dialog.HintTextBlock.Text);
				}
				finally
				{
					dialog.Close();
					Dispatcher.UIThread.RunJobs();
				}
			});
		}
	}
}
