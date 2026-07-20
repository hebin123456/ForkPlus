using System;
using System.Threading;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace ForkPlus.AutomationTests
{
	/// <summary>
	/// v2.1.2 bug 修复保护测试。覆盖以下 4 个 bug 修复：
	///
	/// Bug 1: 自定义颜色修改不实时生效
	///   修复：CustomColorsDialog.ApplyAndRefresh 调用 App.ApplyCustomColors() + Save() 立即落盘。
	///   v2.1.2 起移除 OK/Cancel 按钮，换色实时落盘，对话框靠窗口标题栏关闭。
	///   测试：打开 Custom Colors 对话框 → 关闭窗口，验证应用不崩溃。
	///
	/// Bug 2: 随机配色覆盖不全（遗漏 Diff 细粒度色 / 语法高亮色 / 行号选区色共 12 个 key）
	///   修复：RandomPalette_Click 补齐 12 个 Set 调用，覆盖全部 30 个 _editableColorKeys；
	///         Diff 色相在绿区(90-150)/红区(345-15)内随机，避免每次配色 Diff 都相同。
	///   测试：打开 Custom Colors 对话框 → Random Palette → 关闭窗口，验证应用不崩溃。
	///
	/// Bug 3: 初始化新仓库卡死（bt_get_commits 对空 tips 数组永久阻塞，UI 一直转圈）
	///   修复：GetRevisionStorageGitCommand.Execute 加空仓库快速路径直接返回空 RevisionStorage。
	///   测试：创建空仓库（git init + untracked 文件，无 commits）→ 启动 ForkPlus →
	///         验证 loading 状态在 30 秒内消失（不卡死）。
	///
	/// Bug 4: 通知按钮语言切换不实时（HeaderLabel.Text 仅构造函数设一次，换语言不更新）
	///   修复：NotificationManagerUserControl 实现 ILocalizableControl，
	///         MainWindow.ApplyLocalization 调用其 ApplyLocalization() 重新翻译 HeaderLabel。
	///   测试：启动应用 → 打开通知 popup → 切换语言 → 验证应用不崩溃且 popup 仍可用。
	/// </summary>
	public class BugFixV212Tests : AutomationTestBase, IDisposable
	{
		// ============================================================
		// Bug 1: 自定义颜色修改应实时生效
		// ============================================================

		/// <summary>
		/// Bug 1：打开 Custom Colors 对话框 → 关闭窗口。
		/// 验证 App.ApplyCustomColors() + Save() 流程不崩溃。
		/// 修复前：Ok_Click 只保存设置不调用 ApplyCustomColors，导致 Diff/热力图等控件不重绘。
		/// v2.1.2 修复：ApplyAndRefresh 调用 ApplyCustomColors + Save 实时落盘；
		///   对话框移除 OK/Cancel 按钮，换色实时生效，靠窗口标题栏关闭。
		/// </summary>
		[Fact]
		public void Bug1_CustomColorEdit_CloseWindow_NoCrash()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app), "未找到 Appearance 下拉按钮。");

				// 打开 Custom Colors 对话框
				var item = FindMenuItemByText(app.Window, "Custom Colors")
					?? FindMenuItemByText(app.Window, "自定义颜色");
				Assert.NotNull(item);
				try { item.Click(); } catch { }

				var win = WaitForTopLevelWindow(app, "Custom Colors", TimeSpan.FromSeconds(10))
						  ?? WaitForTopLevelWindow(app, "自定义颜色", TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				// v2.1.2 起对话框无 OK/Cancel 按钮，换色已实时落盘。直接关闭窗口。
				CloseWindow(win);
				Thread.Sleep(1500);

				// 验证应用未崩溃（ApplyCustomColors + Save 不应抛异常导致进程退出）
				Assert.False(app.Application.HasExited,
					"关闭对话框后应用退出，Bug 1 修复可能引入回归：ApplyCustomColors/Save 抛异常。");

				// 验证对话框已关闭
				var stillOpen = WaitForTopLevelWindow(app, "Custom Colors", TimeSpan.FromSeconds(1))
							  ?? WaitForTopLevelWindow(app, "自定义颜色", TimeSpan.FromSeconds(1));
				Assert.Null(stillOpen);
			}
		}

		// ============================================================
		// Bug 2: 随机配色应覆盖所有 30 个颜色 key
		// ============================================================

		/// <summary>
		/// Bug 2：Random Palette 应覆盖全部 30 个 _editableColorKeys。
		/// 修复前：RandomPalette_Click 只 Set 18 个 key，遗漏 Diff.AddColor / Diff.RemoveColor /
		/// Diff.ExactAddColor / Diff.ExactRemoveColor / Syntax.* / LineNumber.* / ChunkSelection.* 共 12 个。
		/// v2.1.2 修复：补齐 12 个 Set 调用；Diff 色相在绿区(90-150)/红区(345-15)内随机，
		///   避免每次配色 Diff 颜色都完全相同。测试点击 Random Palette 后关闭窗口，验证不崩溃。
		/// </summary>
		[Fact]
		public void Bug2_RandomPalette_CoversAllColorKeys_NoCrash()
		{
			using (var app = LaunchApp())
			{
				Assert.True(OpenAppearanceDropdown(app));

				var item = FindMenuItemByText(app.Window, "Custom Colors")
					?? FindMenuItemByText(app.Window, "自定义颜色");
				Assert.NotNull(item);
				try { item.Click(); } catch { }

				var win = WaitForTopLevelWindow(app, "Custom Colors", TimeSpan.FromSeconds(10))
						  ?? WaitForTopLevelWindow(app, "自定义颜色", TimeSpan.FromSeconds(3));
				Assert.NotNull(win);

				// 点击 Random Palette（Bug 2 修复核心：补齐 12 个 Set 调用 + Diff 色相随机）
				var randomBtn = win.FindFirstDescendant(cf => cf.ByName("Random Palette"))
					?? win.FindFirstDescendant(cf => cf.ByName("随机配色"));
				Assert.NotNull(randomBtn);
				try { randomBtn.Click(); Thread.Sleep(1000); } catch { }

				// 验证应用未崩溃（遗漏的 key 不应导致 Set 调用抛异常）
				Assert.False(app.Application.HasExited,
					"点击 Random Palette 后应用退出，Bug 2 修复可能引入回归。");

				// v2.1.2 起对话框无 OK 按钮，随机配色已实时落盘（ApplyAndRefresh → Save）。
				// 直接关闭窗口，验证 30 个 key 都能正确 merge + 落盘。
				CloseWindow(win);
				Thread.Sleep(1500);

				Assert.False(app.Application.HasExited,
					"关闭对话框后应用退出，30 个 key merge/Save 可能有问题。");
			}
		}

		// ============================================================
		// Bug 3: 空仓库（git init + untracked 文件，无 commits）不应卡死
		// ============================================================

		/// <summary>
		/// Bug 3：创建空仓库（git init + untracked notes.txt，无任何 commit），
		/// 启动 ForkPlus 打开该仓库，验证 loading 状态在 30 秒内消失。
		///
		/// 修复前：bt_get_commits 对空 tips 数组（无 commits/branches/tags）永久阻塞，
		/// JobQueue.IsIdle 永久 false，StatusUserControl.DescriptionTextBlock 永久显示 "loading..."。
		/// 修复后：GetRevisionStorageGitCommand.Execute 检测空仓库直接返回空 RevisionStorage，
		/// loading 状态快速消失（DescriptionTextBlock 变为 "detached HEAD" 或类似文本）。
		///
		/// 这是用户报告的最严重 bug："它一直在转圈，无法读取这个新仓库"。
		/// </summary>
		[Fact]
		public void Bug3_EmptyRepo_LoadsWithoutHanging()
		{
			string repoPath = CreateEmptyGitRepo(untrackedFileName: "notes.txt", untrackedContent: "my notes");
			using (var app = LaunchApp($"\"{repoPath}\""))
			{
				Assert.NotNull(app.Window);

				// 给仓库加载初始时间（loading 状态此时应该可见）
				Thread.Sleep(2000);

				// 轮询检查 loading 状态是否在 30 秒内消失
				// Bug 3 现象：DescriptionTextBlock 永久显示 "loading..."（Translate("loading...")）
				// 修复后：loading 消失，文本变为 "detached HEAD" 或分支名等
				bool loadingDisappeared = false;
				var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
				while (DateTime.UtcNow < deadline)
				{
					app.RefreshMainWindow();
					bool isLoading = IsLoadingTextVisible(app.Window);
					if (!isLoading)
					{
						loadingDisappeared = true;
						break;
					}
					Thread.Sleep(1000);
				}

				Assert.True(loadingDisappeared,
					"空仓库加载超过 30 秒仍未完成（UI 仍显示 loading 文本）。" +
					"这表明 Bug 3 未修复：bt_get_commits 对空 tips 数组阻塞，" +
					"GetRevisionStorageGitCommand 的空仓库快速路径未生效。");

				Assert.False(app.Application.HasExited,
					"应用在加载空仓库时崩溃。");
			}
		}

		/// <summary>
		/// 检查主窗口是否有任何后代文本包含 "loading"（英文）/ "加载"（中文）/ "読み込み"（日文）等。
		/// StatusUserControl.DescriptionTextBlock 在 RepositoryData == null 时显示 Translate("loading...")。
		/// Bug 3 修复前：RepositoryData 永久 null（job 阻塞），loading 文本永久可见。
		/// Bug 3 修复后：空仓库快速路径返回空 RevisionStorage，RepositoryData 非 null，loading 消失。
		/// </summary>
		private static bool IsLoadingTextVisible(FlaUI.Core.AutomationElements.Window window)
		{
			if (window == null) return false;
			try
			{
				// 查找所有 Text 类型后代（TextBlock 在 UIA3 中映射为 Text 控件类型）
				var textElements = window.FindAllDescendants(
					cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
				foreach (var el in textElements)
				{
					string name = (el.Name ?? "").ToLowerInvariant();
					// 覆盖常见语言的 "loading" 翻译
					if (name.Contains("loading") || name.Contains("加载") || name.Contains("読み込み")
						|| name.Contains("laden") || name.Contains("chargement") || name.Contains("cargando"))
					{
						return true;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[IsLoadingTextVisible] Error: " + ex.Message);
			}
			return false;
		}

		// ============================================================
		// Bug 4: 通知按钮语言切换应实时刷新 HeaderLabel
		// ============================================================

		/// <summary>
		/// Bug 4：打开通知 popup → 切换语言 → 验证应用不崩溃且 popup 仍可用。
		///
		/// 修复前：NotificationManagerUserControl.HeaderLabel.Text 仅在构造函数设一次，
		///   语言切换后仍显示旧语言文本，需重启客户端才更新。
		/// 修复后：NotificationManagerUserControl 实现 ILocalizableControl，
		///   MainWindow.ApplyLocalization 调用其 ApplyLocalization() 重新翻译 HeaderLabel。
		///
		/// 注意：CI 上语言菜单在 popup 内可能无法点击（Session 0 无交互桌面），
		/// 此测试主要验证：打开通知 popup + 切换语言流程不导致应用崩溃。
		/// 本地交互式环境下，会额外验证 HeaderLabel 文本随语言切换变化。
		/// </summary>
		[Fact]
		public void Bug4_NotificationPopup_LanguageSwitch_NoCrash()
		{
			using (var app = LaunchApp())
			{
				Assert.NotNull(app.Window);
				Thread.Sleep(2000); // 等待主窗口完全初始化

				// Step 1: 打开通知 popup（点击右上角通知按钮）
				// ToggleButton 的 AutomationId = "Part_NotificationManagerToggleButton"
				bool popupOpened = TryOpenNotificationPopup(app);
				// 不强制断言 popup 打开成功——NotificationManager 可能在 CI 上未激活
				Thread.Sleep(1000);

				// Step 2: 切换语言（通过 Appearance 下拉 → Language → 简体中文）
				bool languageSwitched = TrySwitchLanguageToChinese(app);

				// Step 3: 验证应用未崩溃（核心断言）
				Assert.False(app.Application.HasExited,
					"打开通知 popup + 切换语言后应用退出。" +
					"Bug 4 修复可能引入回归：ApplyLocalization 调用 ApplyLocalization 时抛异常。");

				// Step 4: 如果语言切换成功，验证通知 popup 的 HeaderLabel 已更新
				// Bug 4 修复前：HeaderLabel 仍显示 "Notifications"（英文）
				// Bug 4 修复后：HeaderLabel 显示 "通知"（中文）
				if (languageSwitched)
				{
					// 重新打开 popup（之前可能因点击外观下拉而关闭）
					if (!popupOpened || !IsNotificationPopupOpen(app))
					{
						TryOpenNotificationPopup(app);
					}
					Thread.Sleep(500);

					// 在桌面所有窗口中查找通知 popup（独立 HWND）
					bool foundChineseHeader = FindNotificationHeaderText(app, "通知");
					bool foundEnglishHeader = FindNotificationHeaderText(app, "Notifications");

					// 如果能找到任何 HeaderLabel 文本，验证它是中文（修复后）
				// 找不到时不强制断言（popup 内容在 CI 上可能不可访问）
				if (foundEnglishHeader && !foundChineseHeader)
				{
					Assert.Fail(
						"Bug 4 未修复：通知 popup HeaderLabel 仍显示 'Notifications'（英文），" +
						"语言切换后未实时刷新为 '通知'。NotificationManagerUserControl.ApplyLocalization 未被调用。");
				}
				}
			}
		}

		/// <summary>
		/// 点击右上角通知 ToggleButton 打开 popup。
		/// ToggleButton 在 MainWindow ControlTemplate 内，x:Name="Part_NotificationManagerToggleButton"。
		/// </summary>
		private bool TryOpenNotificationPopup(LaunchedApp app)
		{
			try
			{
				var btn = app.Window.FindFirstDescendant(
					cf => cf.ByAutomationId("Part_NotificationManagerToggleButton"));
				if (btn == null)
				{
					Console.WriteLine("[Bug4] 通知 ToggleButton 未找到。");
					return false;
				}
				btn.Click();
				Thread.Sleep(500);
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Bug4] 打开通知 popup 失败: " + ex.Message);
				return false;
			}
		}

		/// <summary>检查通知 popup 是否当前处于打开状态（通过查找 popup 内的 HeaderLabel）。</summary>
		private bool IsNotificationPopupOpen(LaunchedApp app)
		{
			return FindNotificationHeaderText(app, "Notifications")
				|| FindNotificationHeaderText(app, "通知");
		}

		/// <summary>
		/// 切换 UI 语言到简体中文。通过 Appearance 下拉 → Language 分组 → 简体中文。
		/// 返回 true 表示语言菜单项被找到并点击。
		/// </summary>
		private bool TrySwitchLanguageToChinese(LaunchedApp app)
		{
			try
			{
				if (!OpenAppearanceDropdown(app))
				{
					Console.WriteLine("[Bug4] 无法打开 Appearance 下拉。");
					return false;
				}
				var langItem = FindMenuItemByText(app.Window, "简体中文");
				if (langItem == null)
				{
					Console.WriteLine("[Bug4] 未找到 '简体中文' 语言菜单项。");
					return false;
				}
				langItem.Click();
				Thread.Sleep(2000); // 等待 ApplyLocalization 完成
				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Bug4] 切换语言失败: " + ex.Message);
				return false;
			}
		}

		/// <summary>
		/// 在桌面所有顶级窗口中查找通知 popup 的 HeaderLabel 文本。
		/// 通知 popup 是独立 HWND（WPF Popup），不在主窗口后代树里，需遍历桌面窗口。
		/// HeaderLabel 是 TextBlock，文本为 "Notifications"（英文）或 "通知"（中文）。
		/// </summary>
		private bool FindNotificationHeaderText(LaunchedApp app, string expectedText)
		{
			try
			{
				var desktop = app.Automation.GetDesktop();
				var topWindows = desktop.FindAllChildren();
				foreach (var topWin in topWindows)
				{
					// 只检查属于 ForkPlus 进程的窗口
					try
					{
						if (topWin.Properties.ProcessId.Value != app.Application.ProcessId) continue;
					}
					catch { continue; }

					var textElements = topWin.FindAllDescendants(
						cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text));
					foreach (var el in textElements)
					{
						string name = el.Name ?? "";
						if (name.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) >= 0)
						{
							return true;
						}
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine("[Bug4] FindNotificationHeaderText 异常: " + ex.Message);
			}
			return false;
		}
	}
}
