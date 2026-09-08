// E2E 模块25（2026-09-06）：通用对话框（9 类窗口，7 用例）。
// 覆盖：MessageBoxWindow（三形态 + Submit/Esc 关窗——模块 5/13/23 模态泵的独立闭环）
//       ErrorWindow（纯文本模式 + RepositoryIsLocked 修复按钮真实删除 index.lock）
//       AskPassWindow（四模式：Username/SshPassphrase/SshUserPassword/fallback——
//                      AskPassRequest.Parse 正则分流 + 输入框互斥 + Remember 显隐 + Result 装配）
//       CustomColorsDialog（30 可编辑颜色项 + 按钮四件套 + Reset All 恢复默认零污染）
//       KeyboardShortcutsWindow（纯代码 UI：段落标题本地化 + 键位徽章 + Esc 关窗）
//       AboutWindow（版本号/版权/Logo 装配，无 footer）
//       UpdateCheckWindow（无外网失败终态：CheckingPanel→ResultPanel + 错误文案 + Close）
//       PerformanceDiagnosticsWindow（Samples 装配 + Refresh + Copy 剪贴板）
//       通知中心（MainWindow 无账号 ToggleButton 隐藏态 + NotificationManagerUserControl 直构装配）
//
// 模式（与模块 11-24 一致）：生产构造器直构 + Show() 非模态（chrome 经 Dispatcher.Post
// 延迟创建，RunJobs 后 Footer SubmitButton/CancelButton 入树——ForkPlusDialogWindow 基类
// AddFooter 动态注入，SubmitButton/CancelButton 不在各窗口 axaml 里）。按钮定位一律
// UiClick.FindAll<Button>(window) + ContentText == Tr(...)（Footer 按钮无 x:Name 可直取）。
// 设置污染防护（模块 21/23/24 全属性反射快照）：CustomColors 用例改 ForkPlusSettings.CustomColors。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e25CommonDialogsTests
	{
		private const string ModuleDir = "25-common-dialogs";

		// ============================ 共享助手 ============================

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static string TrFormat(string text, params object[] args)
		{
			return E2eMainWindowHarness.TrFormat(text, args);
		}

		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>视觉树里找 Content 文本匹配的第一个可见按钮（Footer 按钮无 x:Name，模块 23 模式）。</summary>
		private static Button FindButton(Visual root, string content)
		{
			return UiClick.FindAll<Button>(root)
				.FirstOrDefault(b => UiClick.ContentText(b) == content && b.IsVisible);
		}

		/// <summary>视觉树全部可见按钮的 Content 文本集合（断言按钮形态）。</summary>
		private static List<string> VisibleButtonTexts(Visual root)
		{
			return UiClick.FindAll<Button>(root)
				.Where(b => b.IsVisible)
				.Select(b => UiClick.ContentText(b))
				.ToList();
		}

		/// <summary>视觉树全部 TextBlock 文本集合。</summary>
		private static List<string> AllTexts(Visual root)
		{
			return UiClick.FindAll<global::Avalonia.Controls.TextBlock>(root)
				.Select(t => t.Text)
				.Where(t => !string.IsNullOrEmpty(t))
				.ToList();
		}

		/// <summary>键位徽章文本序列（Border 包 TextBlock 的徽章结构，按视觉树深度优先顺序
		/// 收集）。KeyboardShortcutsWindow.CreateKeysPanel 把 "Ctrl+P" 按 '+' 拆成独立徽章
		/// （Ctrl 徽章 / "+" 分隔 TextBlock / P 徽章），完整组合字符串不出现在任何单一
		/// TextBlock 里——断言按键 token 相邻序列而非组合串。</summary>
		private static List<string> KeyBadgeTexts(Visual root)
		{
			return UiClick.FindAll<global::Avalonia.Controls.Border>(root)
				.Select(b => b.Child as global::Avalonia.Controls.TextBlock)
				.Where(tb => tb != null && !string.IsNullOrEmpty(tb.Text))
				.Select(tb => tb.Text)
				.ToList();
		}

		/// <summary>徽章序列中是否存在给定的相邻按键 token 序列（如 Ctrl→P、Ctrl→Shift→F）。</summary>
		private static bool HasAdjacentKeys(List<string> badges, params string[] keys)
		{
			for (int i = 0; i + keys.Length <= badges.Count; i++)
			{
				bool match = true;
				for (int j = 0; j < keys.Length; j++)
				{
					if (badges[i + j] != keys[j])
					{
						match = false;
						break;
					}
				}
				if (match)
				{
					return true;
				}
			}
			return false;
		}

		// ---------- 设置快照/还原（模块 21/23/24 全属性反射） ----------

		private sealed class PrefsSnapshot
		{
			public Dictionary<string, object> Values = new Dictionary<string, object>();
		}

		private static PrefsSnapshot SnapshotPrefs()
		{
			var snap = new PrefsSnapshot();
			foreach (PropertyInfo p in typeof(ForkPlusSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (p.CanWrite && p.GetIndexParameters().Length == 0)
				{
					try { snap.Values[p.Name] = p.GetValue(ForkPlusSettings.Default); }
					catch { }
				}
			}
			return snap;
		}

		private static void RestorePrefs(PrefsSnapshot snap)
		{
			try
			{
				foreach (var pair in snap.Values)
				{
					typeof(ForkPlusSettings).GetProperty(pair.Key)?.SetValue(ForkPlusSettings.Default, pair.Value);
				}
				ForkPlusSettings.Default.Save();
			}
			catch (Exception ex)
			{
				Console.WriteLine("[E2e25] 设置恢复失败: " + ex.Message);
			}
		}

		// ============================ 1) MessageBox：三形态 + Submit/Esc 关窗 ============================

		[Fact]
		public void MessageBox_ThreeVariantsAndClosePaths()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// ===== 1) 标准形态（双按钮）：chrome 延迟装配 + Submit 点击关窗 =====
				var box1 = new global::ForkPlus.UI.Dialogs.MessageBoxWindow(
					"Delete repository", "The repository will be removed from the list. This cannot be undone.",
					"Delete", "Cancel");
				box1.Show();
				RunJobs();
				// chrome（标题/描述/Footer 按钮）经 Dispatcher.Post 延迟创建——RunJobs 后入树
				Assert.Contains(Tr("Delete repository"), AllTexts(box1));
				Assert.Contains(Tr("The repository will be removed from the list. This cannot be undone."), AllTexts(box1));
				Assert.NotNull(FindButton(box1, Tr("Delete")));
				Assert.NotNull(FindButton(box1, Tr("Cancel")));

				global::ForkPlus.Tests.ScreenshotHelper.Snap(box1, "01-messagebox-standard", ModuleDir);

				UiClick.Click(FindButton(box1, Tr("Delete")));
				RunJobs();
				Assert.False(box1.IsVisible, "Submit 点击后窗口应关闭");

				// ===== 2) 无取消按钮形态（showCancelButton:false）=====
				var box2 = new global::ForkPlus.UI.Dialogs.MessageBoxWindow(
					"Operation done", "All files staged.", "OK", "Cancel", showCancelButton: false);
				box2.Show();
				RunJobs();
				Assert.NotNull(FindButton(box2, Tr("OK")));
				Assert.Null(FindButton(box2, Tr("Cancel"))); // 无可见 Cancel 按钮
				UiClick.Click(FindButton(box2, Tr("OK")));
				RunJobs();
				Assert.False(box2.IsVisible, "OK 点击后窗口应关闭");

				// ===== 3) 警告图标形态 + Esc 关窗（OnKeyDown → OnCancel，ShowCancelButton=true 时有效） =====
				var box3 = new global::ForkPlus.UI.Dialogs.MessageBoxWindow(
					"Warning title", "Something needs attention.", "OK", showCancelButton: true, showWarningIcon: true);
				box3.Show();
				RunJobs();
				Assert.NotNull(FindButton(box3, Tr("OK")));
				Assert.NotNull(FindButton(box3, Tr("Cancel")));
				// 警告图标形态：Logo + 24x24 警告图叠加（视觉树 Image ≥2）
				Assert.True(UiClick.FindAll<global::Avalonia.Controls.Image>(box3).Count >= 2,
					"警告图标形态应叠加 Logo + Warning 两个 Image");

				global::ForkPlus.Tests.ScreenshotHelper.Snap(box3, "02-messagebox-warning", ModuleDir);

				// Esc → OnKeyDown → OnCancel → Close(false)（模块 8 KeyDown 路由模式）
				box3.RaiseEvent(new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Escape
				});
				RunJobs();
				Assert.False(box3.IsVisible, "Esc 按键后窗口应关闭");
			});
		}

		// ============================ 2) ErrorWindow：纯文本 + index.lock 真实修复 ============================

		[Fact]
		public void ErrorWindow_PlainTextAndIndexLockRecovery()
		{
			string repo = TestRepoFactory.CreateBasic();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// 看门狗暂停（模块 25 基建）：默认每 200ms 自动关闭可见 ErrorWindow（模块 16
					// 防挂死机制），本用例需要窗口存活做断言/截图/点击——finally 恢复后 Run 收尾
					// 的 CloseVisibleErrorDialogs 兜底关残留。
					HeadlessAppBootstrap.SetErrorDialogWatchdogSuspended(true);
					try
					{
						// ===== 1) 纯文本模式：消息装配 + FirstButton 隐藏 + Close 按钮 =====
						var plain = new global::ForkPlus.UI.Dialogs.ErrorWindow("boom: something went wrong");
						plain.Show();
						RunJobs();
						Assert.Equal("boom: something went wrong", plain.MessageTextBox.Text);
						Assert.False(plain.FirstButton.IsVisible, "纯文本模式无上下文修复按钮");
						Assert.NotNull(FindButton(plain, Tr("Close")));
						Assert.Contains(Tr("Git Error"), AllTexts(plain));

						global::ForkPlus.Tests.ScreenshotHelper.Snap(plain, "03-errorwindow-plain", ModuleDir);

						UiClick.Click(FindButton(plain, Tr("Close")));
						RunJobs();
						Assert.False(plain.IsVisible);

						// ===== 2) RepositoryIsLocked 模式：修复按钮显示 + 真实删除 index.lock =====
						RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
						global::ForkPlus.UI.Dialogs.ErrorWindow locked = null;
						try
						{
							// 模拟 git 报 "index.lock exists"（真实发生场景：并发 git 操作）
							string lockPath = Path.Combine(repoControl.GitModule.GitDir(), "index.lock");
							File.WriteAllText(lockPath, "stale lock");

							locked = new global::ForkPlus.UI.Dialogs.ErrorWindow(repoControl,
								new GitCommandError.RepositoryIsLocked(
									"fatal: Unable to create '.../index.lock': File exists.",
									"fatal: Unable to create '.../index.lock': File exists."));
							locked.Show();
							RunJobs();

							// FirstButton 显示 + 文案本地化 + 消息 = FriendlyDescription
							Assert.True(locked.FirstButton.IsVisible, "RepositoryIsLocked 应显示修复按钮");
							Assert.Equal(Tr("Remove .git/index.lock"), UiClick.ContentText(locked.FirstButton));
							Assert.Contains("index is locked", locked.MessageTextBox.Text);

							global::ForkPlus.Tests.ScreenshotHelper.Snap(locked, "04-errorwindow-locked", ModuleDir);

							// 点击修复 → RemoveLockIndexFile → File.Delete(index.lock) + Close()
							UiClick.Click(locked.FirstButton);
							RunJobs();
							Assert.True(UiClick.WaitFor(delegate { return !locked.IsVisible; }),
								"修复点击后窗口应关闭");
							Assert.False(File.Exists(lockPath), "index.lock 应被真实删除");
						}
						finally
						{
							locked?.Close();
							E2eMainWindowHarness.CloseRepositoryTab(window, repo);
						}
					}
					finally
					{
						HeadlessAppBootstrap.SetErrorDialogWatchdogSuspended(false);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 3) AskPassWindow：四模式 ============================

		[Fact]
		public void AskPassWindow_FourModesAndResult()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string repoPath = Path.Combine("/tmp", "fpe2e-askpass-repo");

				// ===== 1) Username 模式：明文框 + 无 Remember =====
				var username = new global::ForkPlus.UI.Dialogs.AskPassWindow(
					"Username for 'https://github.com':", repoPath);
				username.Show();
				RunJobs();
				Assert.Equal(Tr("User Name:"), username.InputTextBlock.Text);
				Assert.True(username.InputTextBox.IsVisible, "Username 模式应显示明文框");
				Assert.False(username.InputPasswordBox.IsVisible, "Username 模式应隐藏密码框");
				Assert.False(username.RememberCheckBox.IsVisible, "Username 模式无 Remember");
				// 标题/描述装配（chrome 文本，经视觉树断言——DialogTitle/DialogDescription 为 protected）
				Assert.Contains("fpe2e-askpass-repo", AllTexts(username));
				Assert.Contains("Username for 'https://github.com':", AllTexts(username));

				// 输入 → Submit → Result 装配 + 关窗
				username.InputTextBox.Text = "octocat";
				RunJobs();
				Button ok1 = FindButton(username, Tr("OK"));
				Assert.NotNull(ok1);
				UiClick.Click(ok1);
				RunJobs();
				Assert.False(username.IsVisible);
				Assert.Equal("octocat", username.Result);

				// ===== 2) SshPassphrase 模式：密码框 + Remember（不勾选 → 不写凭据管理器） =====
				var passphrase = new global::ForkPlus.UI.Dialogs.AskPassWindow(
					"Enter passphrase for key '/home/user/.ssh/id_ed25519':", repoPath);
				passphrase.Show();
				RunJobs();
				Assert.Equal(Tr("Passphrase:"), passphrase.InputTextBlock.Text);
				Assert.False(passphrase.InputTextBox.IsVisible);
				Assert.True(passphrase.InputPasswordBox.IsVisible);
				Assert.True(passphrase.RememberCheckBox.IsVisible, "SshPassphrase 模式应显示 Remember");
				// 描述 = 格式键（KeyPath 提取自正则捕获组，chrome 文本经视觉树断言）
				Assert.Contains(TrFormat("Passphrase for SSH key '{0}'", "/home/user/.ssh/id_ed25519"),
					AllTexts(passphrase));

				global::ForkPlus.Tests.ScreenshotHelper.Snap(passphrase, "05-askpass-passphrase", ModuleDir);

				passphrase.InputPasswordBox.Text = "ssh-secret";
				RunJobs();
				UiClick.Click(FindButton(passphrase, Tr("OK")));
				RunJobs();
				Assert.False(passphrase.IsVisible);
				Assert.Equal("ssh-secret", passphrase.Result);

				// ===== 3) SshUserPassword 模式（"git@github.com's password:" 正则） =====
				var sshPassword = new global::ForkPlus.UI.Dialogs.AskPassWindow(
					"git@github.com's password:", repoPath);
				sshPassword.Show();
				RunJobs();
				Assert.Equal(Tr("Password:"), sshPassword.InputTextBlock.Text);
				Assert.True(sshPassword.InputPasswordBox.IsVisible);
				Assert.False(sshPassword.InputTextBox.IsVisible);
				Assert.True(sshPassword.RememberCheckBox.IsVisible, "SshUserPassword 模式应显示 Remember");
				// Uri("ssh://git@github.com") → UserInfo="git" + Host="github.com" → "git@github.com"
				Assert.Contains(TrFormat("Passphrase for '{0}'", "git@github.com"),
					AllTexts(sshPassword));

				sshPassword.InputPasswordBox.Text = "pw123";
				RunJobs();
				UiClick.Click(FindButton(sshPassword, Tr("OK")));
				RunJobs();
				Assert.False(sshPassword.IsVisible);
				Assert.Equal("pw123", sshPassword.Result);

				// ===== 4) fallback 模式（不匹配任何正则/前缀 → Password 无 Remember） =====
				var fallback = new global::ForkPlus.UI.Dialogs.AskPassWindow(
					"Password for 'https://example.com':", "");
				fallback.Show();
				RunJobs();
				Assert.Equal(Tr("Password:"), fallback.InputTextBlock.Text);
				Assert.True(fallback.InputPasswordBox.IsVisible);
				Assert.False(fallback.InputTextBox.IsVisible);
				Assert.False(fallback.RememberCheckBox.IsVisible, "fallback 模式无 Remember");
				// 空 repositoryPath → 标题回退 "Credentials Required"（chrome 文本经视觉树断言）
				Assert.Contains(Tr("Credentials Required"), AllTexts(fallback));
				fallback.Close();
				RunJobs();
			});
		}

		// ============================ 4) CustomColorsDialog：装配 + Reset All ============================

		[Fact]
		public void CustomColors_AssemblyAndResetAll()
		{
			var snap = SnapshotPrefs();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
					dialog.Show();
					RunJobs();

					// ===== 1) 装配：30 可编辑颜色项 + 标题（主题名后缀）+ 按钮四件套本地化 =====
					Assert.Equal(30, dialog.ColorListControl.ItemCount);
					Assert.StartsWith(Tr("Custom Colors"), dialog.HeaderTextBlock.Text);
					Assert.Contains("(", dialog.HeaderTextBlock.Text); // "Custom Colors (Light)" 形态
					Assert.Equal(Tr("Reset All"), UiClick.ContentText(dialog.ResetAllButton));
					Assert.Equal(Tr("Random Palette"), UiClick.ContentText(dialog.RandomPaletteButton));
					Assert.Equal(Tr("Import Colors"), UiClick.ContentText(dialog.ImportColorsButton));
					Assert.Equal(Tr("Export Colors"), UiClick.ContentText(dialog.ExportColorsButton));
					// 无 footer：Submit/Cancel 按钮不存在（自定义布局关掉了基类 chrome）
					Assert.Empty(VisibleButtonTexts(dialog).Where(t => t == Tr("OK") || t == Tr("Cancel")));

					global::ForkPlus.Tests.ScreenshotHelper.Snap(dialog, "06-customcolors", ModuleDir);

					// ===== 2) Random Palette → 改色 + Reset All → 恢复默认（真实主题应用链） =====
					UiClick.Click(dialog.RandomPaletteButton);
					RunJobs();
					// RandomPalette 会改 _workingCopy + ApplyAndRefresh → CustomColors 落盘 + 主题重载
					bool randomized = UiClick.WaitFor(delegate
					{
						var colors = ForkPlusSettings.Default.CustomColors;
						return colors != null && colors.Count > 0;
					});
					Assert.True(randomized, "Random Palette 后应产生自定义色并落盘");

					UiClick.Click(dialog.ResetAllButton);
					RunJobs();
					// ResetAll：_workingCopy.Clear() + 全项恢复默认 + ApplyAndRefresh（同步）
					var afterReset = ForkPlusSettings.Default.CustomColors;
					Assert.True(afterReset == null || afterReset.Count == 0,
						"Reset All 后自定义色应清空，实际: " + (afterReset == null ? "null" : afterReset.Count + " 项"));

					dialog.Close();
					RunJobs();
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ 5) KeyboardShortcuts + About 装配 ============================

		[Fact]
		public void KeyboardShortcutsAndAbout_Assembly()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// ===== 1) KeyboardShortcutsWindow（纯代码 UI）：段落本地化 + 键位徽章 + Esc 关窗 =====
				var shortcuts = new global::ForkPlus.UI.Dialogs.KeyboardShortcutsWindow();
				shortcuts.Show();
				RunJobs();

				List<string> texts = AllTexts(shortcuts);
				// 段落标题经 Translate（General Navigation 等 5 段）
				Assert.Contains(Tr("General Navigation"), texts);
				Assert.Contains(Tr("All Commits View"), texts);
				// 键位徽章原文（不翻译）：按 '+' 拆成独立徽章（Ctrl 徽章 / P 徽章），
				// 断言相邻 token 序列——Ctrl+P（Quick Launch）与 Ctrl+Shift+F（Fetch）
				List<string> badges = KeyBadgeTexts(shortcuts);
				Assert.True(HasAdjacentKeys(badges, "Ctrl", "P"),
					"应存在 Ctrl+P 键位徽章序列（Quick Launch），实际徽章: " + string.Join(",", badges));
				Assert.True(HasAdjacentKeys(badges, "Ctrl", "Shift", "F"),
					"应存在 Ctrl+Shift+F 键位徽章序列（Fetch），实际徽章: " + string.Join(",", badges));
				Assert.Contains("Ctrl", badges); // token 原文不翻译
				Assert.Contains("Delete", badges); // 键名徽章原文（回归锁：自动本地化曾把 Delete 翻成"删除"）
				// 只读信息窗：Close（Cancel）按钮 + 无 Submit
				Assert.NotNull(FindButton(shortcuts, Tr("Close")));
				Assert.Null(FindButton(shortcuts, Tr("OK")));

				global::ForkPlus.Tests.ScreenshotHelper.Snap(shortcuts, "07-keyboardshortcuts", ModuleDir);

				// Esc 关窗（ShowCancelButton=true → OnKeyDown 生效）
				shortcuts.RaiseEvent(new KeyEventArgs
				{
					RoutedEvent = InputElement.KeyDownEvent,
					Key = Key.Escape
				});
				RunJobs();
				Assert.False(shortcuts.IsVisible, "Esc 应关闭快捷键窗口");

				// ===== 2) AboutWindow：版本号/版权/Logo（无 footer 只能 X 关闭） =====
				var about = new global::ForkPlus.UI.Dialogs.AboutWindow();
				about.Show();
				RunJobs();

				Assert.Equal(string.Format(Tr("Version {0}"), global::ForkPlus.App.Version),
					about.VersionTextBlock.Text);
				Assert.Equal(string.Format(Tr("Copyright © {0} Hebin"), DateTime.Now.Year),
					about.CopyrightTextBlock.Text);
				// Logo Image 在视觉树（90x90 图标）
				Assert.NotEmpty(UiClick.FindAll<global::Avalonia.Controls.Image>(about));
				// 无 footer（ShowFooter=false → 无 OK/Cancel/Close 确认按钮）；
				// 可见按钮仅内容区超链接（hebin.me）+ 标题栏图标按钮（ContentText=""）
				Assert.Null(FindButton(about, Tr("OK")));
				Assert.Null(FindButton(about, Tr("Cancel")));
				Assert.Null(FindButton(about, Tr("Close")));
				Assert.Contains("hebin.me", VisibleButtonTexts(about));

				global::ForkPlus.Tests.ScreenshotHelper.Snap(about, "08-about", ModuleDir);

				about.Close();
				RunJobs();
			});
		}

		// ============================ 6) UpdateCheck（环境自适应终态）+ PerformanceDiagnostics ============================

		/// <summary>取格式键本地化后首个参数占位前的文本（Contains 断言用——实际文本的参数值
		/// 不可预测：网络错误消息/版本号，沙盒直连失败与代理可达两种环境下的终态文案不同）。</summary>
		private static string FormatPrefix(string formatKey)
		{
			string formatted = TrFormat(formatKey, "E2ETOKEN", "E2ETOKEN");
			int idx = formatted.IndexOf("E2ETOKEN", StringComparison.Ordinal);
			return idx < 0 ? formatted : formatted.Substring(0, idx);
		}

		[Fact]
		public void UpdateCheckFailureAndDiagnostics()
		{
			var snap = SnapshotPrefs(); // MarkChecked/SkipVersion 可能写设置
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					// ===== 1) UpdateCheckWindow：Loaded 即检测 → 终态三形态之一 =====
					// 沙盒直连无外网 → HTTP 失败（"Update check failed: {异常消息}"）；
					// 配置了代理出口时检测可达 GitHub → "已是最新版" 或 "有新版本"。
					// 断言按三形态环境自适应（模块 21 Git 实例下拉同款口径）。
					var update = new global::ForkPlus.UI.Dialogs.UpdateCheckWindow();
					update.Show();
					RunJobs();

					// 检测期间：CheckingPanel 可见（不定进度）
					Assert.True(update.CheckingPanel.IsVisible, "检测期间应显示进度面板");

					// 等检测完成（HTTP 失败/超时或成功 → OnCheckCompleted → ResultPanel）。
					// UpdateChecker 2026-09 兼容 302 网关劫持后为两段尝试（直连 30s + 系统代理 30s），
					// 最坏 ~60s+，等待窗口放大到 95s 覆盖直连与代理双双超时的环境。
					bool done = UiClick.WaitFor(delegate
					{
						return update.ResultPanel.IsVisible && !update.CheckingPanel.IsVisible;
					}, 95000);
					Assert.True(done, "检测应以终态结束（ResultPanel）");

					// 终态文案：失败 / 已是最新版 / 有新版本 三形态之一
					string info = update.VersionInfoTextBlock.Text;
					bool isFailed = info.Contains(FormatPrefix("Update check failed: {0}"));
					bool isLatest = info.Contains(FormatPrefix("You are using the latest version (v{0})."));
					bool hasUpdate = info.Contains(FormatPrefix("A new version {0} is available (current: {1})."));
					Assert.True(isFailed || isLatest || hasUpdate,
						"检测终态文案应为失败/最新版/有更新三种之一，实际: " + info);

					// 形态断言：Download 按钮与 SkipVersion/ReleaseNotes 仅"有更新"形态显示
					if (hasUpdate)
					{
						Assert.NotNull(FindButton(update, Tr("Download")));
						Assert.True(update.SkipVersionCheckBox.IsVisible);
					}
					else
					{
						Assert.False(update.ReleaseNotesTextBox.IsVisible);
						Assert.False(update.SkipVersionCheckBox.IsVisible);
						Assert.Null(FindButton(update, Tr("Download")));
					}

					global::ForkPlus.Tests.ScreenshotHelper.Snap(update, "09-updatecheck-result", ModuleDir);

					// Close 按钮 → OnCancel → _cts.Cancel + 关窗
					UiClick.Click(FindButton(update, Tr("Close")));
					RunJobs();
					Assert.False(update.IsVisible);

					// ===== 2) PerformanceDiagnosticsWindow：Samples 装配 + Refresh + Copy =====
					var diagnostics = new global::ForkPlus.UI.Dialogs.PerformanceDiagnosticsWindow();
					diagnostics.Show();
					RunJobs();

					// 构造期 RefreshSamples：标头 + 慢样本/最近样本两段
					Assert.Contains("Slowest samples", diagnostics.SamplesTextBox.Text);
					Assert.Contains("Recent samples", diagnostics.SamplesTextBox.Text);

					// RefreshButton → RefreshSamples（重新装配，文本等价）
					UiClick.Click(diagnostics.RefreshButton);
					RunJobs();
					Assert.Contains("Slowest samples", diagnostics.SamplesTextBox.Text);

					// CopyButton → 剪贴板 SetText（headless Noop 提供器不抛异常即通过）
					UiClick.Click(diagnostics.CopyButton);
					RunJobs();

					global::ForkPlus.Tests.ScreenshotHelper.Snap(diagnostics, "10-diagnostics", ModuleDir);

					diagnostics.Close();
					RunJobs();
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ 7) 通知中心：隐藏按钮态 + 面板装配 ============================

		[Fact]
		public void NotificationManager_HiddenToggleAndPanelAssembly()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				// ===== 1) MainWindow：无通知账号 → ToggleButton 隐藏（IsActive=false → Hide(true)） =====
				var window = E2eMainWindowHarness.CreateWindow();
				try
				{
					RunJobs();
					// 模板内 ToggleButton（ToolTip = Notifications 定位——模板 part 字段私有）
					var toggle = window.GetVisualDescendants().OfType<global::Avalonia.Controls.Primitives.ToggleButton>()
						.FirstOrDefault(t => global::Avalonia.Controls.ToolTip.GetTip(t) as string == Tr("Notifications"));
					Assert.True(toggle != null, "标题栏通知 ToggleButton 应存在");
					Assert.False(ForkPlus.Accounts.NotificationManager.Current.IsActive,
						"测试环境无通知账号，IsActive 应为 false");
					Assert.False(toggle.IsVisible, "无账号时通知按钮应隐藏");

					// ===== 2) NotificationManagerUserControl 直构：面板装配（包进临时窗显示） =====
					var panel = new NotificationManagerUserControl();
					var host = new global::Avalonia.Controls.Window
					{
						Title = "Notifications panel",
						Content = panel,
						Width = 380,
						Height = 480
					};
					host.Show();
					RunJobs();

					// 构造器装配：HeaderLabel 本地化 + ListBox 空数据源 + RefreshButton 存在
					Assert.Equal(Tr("Notifications"), panel.HeaderLabel.Text);
					Assert.Equal(0, panel.ListBox.ItemCount); // 无账号 → 通知列表空
					Assert.NotNull(panel.RefreshButton);

					global::ForkPlus.Tests.ScreenshotHelper.Snap(host, "11-notification-panel", ModuleDir);

					host.Close();
					RunJobs();
				}
				finally
				{
					E2eMainWindowHarness.DetachWindow(window);
				}
			});
		}
	}
}
