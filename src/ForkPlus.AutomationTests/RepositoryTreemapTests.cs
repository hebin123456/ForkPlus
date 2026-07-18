using System;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 仓库树图窗口 ST：通过 Repository 菜单打开 Repository Treemap 窗口，
	/// 验证窗口能弹出、可关闭。需要先打开仓库。
	/// </summary>
	public class RepositoryTreemapTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void OpenRepositoryTreemap_WindowAppears()
		{
			string repoPath = CreateTempGitRepo();
			// 追加几次提交，让 treemap 有内容可渲染
			AddCommit(repoPath, "file1.txt", "content 1\n", "Add file1");
			AddCommit(repoPath, "file2.txt", "content 2\n", "Add file2");

			using (var app = LaunchApp($"\"{repoPath}\""))
			{
				Thread.Sleep(5000);
				app.RefreshMainWindow();

				// 展开 Repository 菜单（WPF 菜单懒加载，必须先展开父项）
				Assert.True(ExpandRootMenu(app, "Repository") || ExpandRootMenu(app, "仓库"),
					"未找到 Repository 菜单");

				// 点击 "Repository Treemap..."
				var treemapItem = FindMenuItemByText(app.Window, "Repository Treemap");
				if (treemapItem == null) treemapItem = FindMenuItemByText(app.Window, "仓库树图");
				Assert.NotNull(treemapItem);
				try { treemapItem.Click(); } catch { }

				// 窗口标题格式 "{repo-name} - Repository Overview" 或 "{repo-name} - 仓库概览"
				var win = WaitForTopLevelWindow(app, "Repository Overview", System.TimeSpan.FromSeconds(15))
						  ?? WaitForTopLevelWindow(app, "仓库概览", System.TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				// 给 treemap 渲染一些时间
				Thread.Sleep(2000);

				CloseWindow(win);
				Thread.Sleep(500);
				Assert.False(app.Application.HasExited, "关闭 treemap 窗口后主应用退出。");
			}
		}
	}
}
