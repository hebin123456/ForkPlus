// 回归测试（2026-10-08，"建议添加 .gitignore 的通知条点『Add .gitignore…』没反应"）：
// 根因：AddGitignoreTemplateWindow 构造函数里直接访问 base.DescriptionTextBlock——
// Avalonia 下 dialog chrome（Title/Description 行）由 ForkPlusDialogWindow 延迟到
// Initialized/内容变更后的 Dispatcher 回调才创建，构造期该字段恒为 null →
// DescriptionTextBlock.Inlines 抛 NullReferenceException。点击通知条按钮时对话框
// 构造函数即崩溃，窗口打不开，表现为"按钮没反应"。
// 修复：改走 CustomizeDescriptionTextBlock 的 pending 机制（构造期安全，chrome 就绪
// 后回放），并在回调里强制 IsVisible=true（AddDialogHeader 会按空描述默认隐藏该行）。
// 本用例走完整用户路径：打开含未跟踪文件且无 .gitignore 的仓库 → 通知条出现
// "Add .gitignore…" 按钮 → 真实 Click 路由 → 断言模板窗口打开且描述行可见。
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class GitignoreSuggestionNotificationBarTests
	{
		/// <summary>仓库含 1 个已提交文件 + 1 个未跟踪文件，且根目录无 .gitignore
		/// （满足通知条 gitignore 建议的显示条件：.gitignore 缺失 && FilesCount != 0）。</summary>
		private static string CreateRepoWithoutGitignore()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpgitignore_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			RunGit("init -q -b main", root);
			RunGit("config user.email test@example.com", root);
			RunGit("config user.name Test", root);
			File.WriteAllText(Path.Combine(root, "tracked.txt"), "hello\n");
			RunGit("add tracked.txt", root);
			RunGit("commit -q -m init", root);
			File.WriteAllText(Path.Combine(root, "untracked.txt"), "world\n");
			return root;
		}

		[Fact]
		public void GitignoreButton_OpensTemplateWindow()
		{
			string repoRoot = CreateRepoWithoutGitignore();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repoRoot, out var window);
					try
					{
						var bar = window.GetVisualDescendants().OfType<NotificationBarUserControl>().First();
						// 通知条随仓库状态刷新展开并显示 "Add .gitignore…"（0.7s 动画 + 后台刷新，轮询等）
						bool suggestionShown = UiClick.WaitFor(delegate
						{
							Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);
							return bar.Button1.IsVisible
								&& (bar.Button1.Content as string) == E2eMainWindowHarness.Tr("Add .gitignore…");
						}, 30000);
						Assert.True(suggestionShown, "无 .gitignore 的仓库应弹出添加 .gitignore 建议（Button1 文案）");

						// 模态 ShowDialog 会 PushFrame 阻塞；装一个定时器在对话框出现时关闭它
						// （AddGitignoreTemplateWindow 不在 HeadlessAppBootstrap 看门狗名单内）。
						AddGitignoreTemplateWindow openedDialog = null;
						var closer = new DispatcherTimer(TimeSpan.FromMilliseconds(100), DispatcherPriority.Default, delegate
						{
							openedDialog = LifetimeWindows().OfType<AddGitignoreTemplateWindow>().FirstOrDefault();
							openedDialog?.Close();
						});
						closer.Start();
						try
						{
							// 真实 Click 路由 → Button1_Click → ShowGitignoreTemplateWindow → 命令 → 新窗口
							bar.Button1.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
						}
						finally
						{
							closer.Stop();
						}

						Assert.True(openedDialog != null, "点击『Add .gitignore…』应打开 AddGitignoreTemplateWindow（原为 NRE 崩溃无反应）");

						// 描述行（"Choose .gitignore template for your project"）应可见且含 .gitignore 超链接
						TextBlock description = openedDialog.GetVisualDescendants().OfType<TextBlock>()
							.FirstOrDefault(delegate(TextBlock t)
							{
								return t.Inlines != null && t.Inlines.OfType<Avalonia.Controls.Documents.Run>()
									.Any((Avalonia.Controls.Documents.Run r) => r.Text == "Choose ");
							});
						Assert.True(description != null, "模板窗口描述行应包含 'Choose ' 内联文本");
						Assert.True(description.IsVisible, "描述行应可见（修复前 AddDialogHeader 会按空描述默认隐藏）");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repoRoot);
					}
				});
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { /* 清理尽力而为 */ }
			}
		}

		private static Window[] LifetimeWindows()
		{
			if (Application.Current?.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime lifetime)
			{
				return lifetime.Windows.ToArray();
			}
			return new Window[0];
		}

		private static void RunGit(string args, string cwd)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = cwd
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception("git " + args + " 失败: " + err);
			}
		}
	}
}