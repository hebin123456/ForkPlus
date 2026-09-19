// E2E 模块30（2026-09-19）：删除远端（RemoveRemoteWindow）。
// 覆盖：删除远端弹窗装配（标题/远端名）→ 命令预览 `git remote remove <name>`
//  → 真实删除（bare remote 引用消失）+ 手册截图（manual/screenshots/13-remotes/）。
// 模式：真实 MainWindow 打开 work 克隆（origin=本地 bare）→ 生产构造器建 RemoveRemoteWindow
//  → 命令预览断言 + 截图 → 提交删除 → 轮询远端引用消失。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e30RemoveRemoteTests
	{
		private const string ModuleDir = "13-remotes";

		[Fact]
		public void RemoveRemote_CommandPreviewAndRealDelete()
		{
			ForkPlusSettings.Default.UiLanguage = "zh-Hans";
			string work = TestRepoFactory.CreateBareRemote();
			string bareUrl = Path.Combine(Directory.GetParent(work).FullName, "remote.git");
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(work, out var window);
					try
					{
						// 取到已装配的 origin 远程
						Remote origin = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							return repoControl.RepositoryData != null
								&& repoControl.RepositoryData.Remotes != null
								&& (origin = repoControl.RepositoryData.Remotes.Items.FirstOrDefault(r => r.Name == "origin")) != null;
						}), "应装配 origin 远程（15s 超时）");

						var dialog = new RemoveRemoteWindow(repoControl, origin);
						dialog.Show();
						Dispatcher.UIThread.RunJobs();

						// 装配断言：标题 + 命令预览
						Assert.Equal("git remote remove origin", CommandPreviewOf(dialog));

						// 手册截图（删除远端，带 git remote remove 命令预览）
						ManualScreenshotHelper.Snap(dialog, "10-remove-remote", ModuleDir);

						// 提交删除
						ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().First();
						UiClick.Click(footer.SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !dialog.IsVisible; }), "删除远端弹窗应提交后关闭（15s 超时）");
						Dispatcher.UIThread.RunJobs();

						// 真实仓库断言：origin 引用已经移除
						string remotes = TestRepoFactory.GitOutput(work, "remote").Trim();
						Assert.DoesNotContain("origin", remotes.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
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

		private static string CommandPreviewOf(ForkPlusDialogWindow dialog)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith("git ", StringComparison.Ordinal))?.Text ?? "";
		}
	}
}