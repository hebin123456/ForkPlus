using System;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.WindowsApi;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// 偏好设置窗口 ST：通过 Ctrl+, 快捷键（ShowPreferencesWindowCommand.Shortcut）
	/// 打开 Preferences 窗口，验证窗口能弹出、可关闭。
	/// 备选：通过 File 菜单 → Preferences... 打开（ExpandRootMenu 带 Alt+F 键盘 fallback）。
	/// </summary>
	public class PreferencesWindowTests : AutomationTestBase, IDisposable
	{
		[Fact]
		public void OpenPreferences_WindowAppears()
		{
			using (var app = LaunchApp())
			{
				// 等待主窗口完全初始化（菜单、命令绑定等）
				Thread.Sleep(2000);

				// Strategy 1: Ctrl+, 直接快捷键（ShowPreferencesWindowCommand.Shortcut）
				bool opened = TryOpenPreferencesViaShortcut(app);

				// Strategy 2: File 菜单 → Preferences...（带 Alt+F 键盘 fallback）
				if (!opened)
				{
					Assert.True(ExpandRootMenu(app, "File") || ExpandRootMenu(app, "文件"),
						"未找到 File 菜单，且 Ctrl+, 快捷键未打开 Preferences 窗口");

					var prefItem = FindMenuItemByText(app.Window, "Preferences");
					if (prefItem == null) prefItem = FindMenuItemByText(app.Window, "偏好设置");
					Assert.NotNull(prefItem);
					try { prefItem.Click(); } catch { }
				}

				var win = WaitForTopLevelWindow(app, "Preferences", TimeSpan.FromSeconds(10))
						  ?? WaitForTopLevelWindow(app, "偏好设置", TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				// 给 Preferences 窗口内部 tab 渲染一些时间
				Thread.Sleep(1000);

				CloseWindow(win);
				Thread.Sleep(500);
				Assert.False(app.Application.HasExited, "关闭 Preferences 窗口后主应用退出。");
			}
		}

		/// <summary>
		/// 用 Ctrl+, 快捷键打开 Preferences 窗口。
		/// ShowPreferencesWindowCommand.Shortcut = KeyGesture(Key.OemComma, ModifierKeys.Control)。
		/// 返回 true 表示窗口成功弹出。
		/// </summary>
		private bool TryOpenPreferencesViaShortcut(LaunchedApp app)
		{
			try
			{
				app.Window.Focus();
				Thread.Sleep(500);
				// VK_OEM_COMMA = 0xBC, LCONTROL = 0xA2
				PressKeyCombination(VirtualKey.LCONTROL, (VirtualKey)0xBC);
				// 等待 Preferences 窗口出现
				var win = WaitForTopLevelWindow(app, "Preferences", TimeSpan.FromSeconds(5))
						  ?? WaitForTopLevelWindow(app, "偏好设置", TimeSpan.FromSeconds(2));
				if (win != null)
				{
					Console.WriteLine("[Preferences] Opened via Ctrl+, shortcut");
					return true;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Preferences] Ctrl+, shortcut failed: " + ex.Message);
			}
			return false;
		}
	}
}
