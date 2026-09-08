// Linux GUI 冒烟（高效路径，替代 Xvfb 截图 + xdotool 坐标点击）：
// Avalonia.Headless in-process 驱动——继承真实 App 拿到全套 App.axaml 资源/样式，
// 控件级"构造-显示-断言"，崩溃时异常堆栈直接进测试输出。
// （原 Windows-only FlaUI/UIA3 套件 ForkPlus.AutomationTests 已删除 2026-09-02，UI 冒烟全部归一到 headless。）
using System;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using ForkPlus.UI.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	// 同一 Collection：与 UiSmokeHeadlessTests/DetachedPopupBehaviorTests/ResourceCompatTests
	// 串行（共享 headless Application 单例，见 HeadlessAppBootstrap——ModuleInitializer 启动、
	// 程序集加载期即绪；Collection 串行保留，避免多个类的窗口操作在 UI 线程上交错）。
	[Collection("HeadlessAvalonia")]
	public class MenuWindowSmokeTests
	{
		// 启动基建与 Run 助手统一收拢在 HeadlessAppBootstrap。

		[Fact]
		public void AccountsWindow_ConstructsAndShows_WithoutCrash()
		{
			// 真机 bug：菜单"文件→账号..."点击后进程退出。第一步先隔离窗口本身：
			// 真实 App 资源环境下直接构造 + 非模态 Show，构造路径任何异常都会带完整堆栈冒出来。
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				Exception ex = null;
				try
				{
					var window = new global::ForkPlus.UI.Dialogs.Accounts.AccountsWindow();
					window.Show();
					Dispatcher.UIThread.RunJobs();
					window.Close();
				}
				catch (Exception e)
				{
					ex = e;
				}
				return ex;
			});
			Assert.True(crash == null, "账号窗口崩溃堆栈：\n" + crash);
		}

		[Fact]
		public void ShowAccountsWindowCommand_PostedJob_OpensAccountsWindow()
		{
			// 菜单"文件→账号..."项点击最终执行的就是 ShowAccountsWindowCommand.Execute（见
			// MainWindowMenuManager.CreateFileMenuItems），其内部 Dispatcher.Post 打开模态窗口。
			// MainLoop 后台线程处理 posted job；等待 AccountsWindow 出现（超时=静默失败），
			// posted job 内抛异常会带堆栈打到测试进程（复现真机崩溃路径）。
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				new ShowAccountsWindowCommand().Execute();
			}).GetAwaiter().GetResult();
			bool appeared = SpinWait.SpinUntil(delegate
			{
				return Dispatcher.UIThread.InvokeAsync(delegate
				{
					return global::ForkPlus.UI.WpfCompat.WpfApp.Windows.Any(delegate (Window w)
					{
						return w is global::ForkPlus.UI.Dialogs.Accounts.AccountsWindow && w.IsVisible;
					});
				}).GetAwaiter().GetResult();
			}, 15000);
			Assert.True(appeared, "账号窗口未打开（posted job 崩溃或静默失败）");
			// 清理：关闭窗口让模态 PushFrame 退出，避免 posted job 悬挂。
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				global::ForkPlus.UI.WpfCompat.WpfApp.Windows
					.FirstOrDefault(delegate (Window w) { return w is global::ForkPlus.UI.Dialogs.Accounts.AccountsWindow; })
					?.Close();
			}).GetAwaiter().GetResult();
		}
	}
}
