using System;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 仓库统计窗口 ST：通过 Repository 菜单打开 Repository Statistics 窗口，
	/// 验证窗口能弹出、可关闭。需要先打开一个仓库（Repository 菜单在有仓库 tab 时才可见）。
	/// </summary>
	public class RepositoryStatisticsTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void OpenRepositoryStatistics_WindowAppears()
		{
			string repoPath = CreateTempGitRepo();
			using (var app = LaunchApp($"\"{repoPath}\""))
			{
				// 等待仓库加载完成（菜单懒加载，需 Repository 菜单可见）
				Thread.Sleep(5000);
				app.RefreshMainWindow();

				// 展开 Repository 菜单
				var repoMenu = FindMenuItemByText(app.Window, "Repository");
				if (repoMenu == null) repoMenu = FindMenuItemByText(app.Window, "仓库");
				Assert.NotNull(repoMenu);
				try { repoMenu.Click(); Thread.Sleep(500); } catch { }

				// 点击 "Repository Statistics..."
				var statsItem = FindMenuItemByText(app.Window, "Repository Statistics");
				if (statsItem == null) statsItem = FindMenuItemByText(app.Window, "仓库统计");
				Assert.NotNull(statsItem);
				try { statsItem.Click(); } catch { }

				// 等待窗口出现（标题 "Repository Statistics" 或 "仓库统计"）
				var win = WaitForTopLevelWindow(app, "Repository Statistics", System.TimeSpan.FromSeconds(15))
						  ?? WaitForTopLevelWindow(app, "仓库统计", System.TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				// 关闭窗口
				CloseWindow(win);
				Thread.Sleep(500);
				Assert.False(app.Application.HasExited, "关闭统计窗口后主应用退出。");
			}
		}
	}
}
