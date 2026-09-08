// E2E 模块22（2026-09-06）：仓库设置窗口（RepositorySettingsWindow，4 tab，5 用例）。
// 覆盖：General（本地身份默认态（TestRepoFactory.Init 恒写 user.name=Test → 不勾全局
// + 启用 + 预填 Test/test@example.com）→ 勾选全局 → SetRepositoryLocalUserIdentity(null)
// → --unset 本地身份 → Refresh 全局模式（勾选+禁用）→ 取消勾选 → 空串身份写本地 →
// 本地模式 → 输入新身份 → Submit 关窗 → git config --local user.name/email 真实落盘 +
// 重开往返 + 新 GitModule 磁盘重载断言 + MainBranch 下拉（默认项 develop/main/master
// 探测 + 分支选择 → LeanBranchingMainBranch + null 默认项选中初态）+ NoFastForward 开关 +
// TabWidth 解析与非数字回退）/Commit（模板全局默认态 → 取消勾选启用编辑 →
// SetLocalCommitTemplateGitCommand 写 CommonGitDir/commit_msg_template.txt +
// config --local commit.template=绝对路径 + 重开内容往返 + 重勾全局 → UnsetLocalCommitTemplate +
// SignOff/SkipCommitMessage（联动正则框启停）/CommitMessageRegex 写回）/
// IssueTracker（应用级 ShowBugtrackerLinks=false → 整 tab fallback；仓库级禁用 → 内容
// fallback（"Issue tracker integration for '{0}' is disabled"）；勾选启用 → ShowContent
// 移除 overlay fallback）/
// IssueTracker 规则（Sample GitHub 规则添加（MenuItem）+ 字段装配 + regex valid/invalid
// 双态 + Level 单选（Local→.git/issuetracker / Shared→.issuetracker）+ 关窗保存 →
// .issuetracker INI 落盘 + 重开往返 + 删除确认泵（确认删→文件移除））/
// CustomCommands 本地模式（Location 容器显示（对比全局模式折叠）+ 本地命令 →
// .git/fork/custom-commands.json + Shared 切换 → .fork/custom-commands.json + OS 下拉联动）。
//
// 模式：RepositorySettingsWindow 直构（生产入口即 new RepositorySettingsWindow(module,
// repositoryData).ShowDialog()，构造器内 Initialize() 全量初始化 4 个 tab）。但
// GeneralUserControl.Refresh 读 MainWindow.ActiveRepositoryUserControl.RepositoryData
// （静态）——必须先经 E2eMainWindowHarness.OpenRepository 真实开仓（模块 5 起的标准路径）。
// 保存路径两条：footer SubmitButton（Close 按钮）→ OnSubmit → 四控件 Save()；window.Close()
// → OnClosing → 同样 Save()。本模块用 Submit 按钮走 OnSubmit 管线。
//
// 探针研读要点（全部实证后落断言）：
//   - Tab 容器是 ModernTabControl 且无 x:Name（唯一命名 TabItem 是 CustomCommandsTab）→
//     经 GetVisualDescendants().OfType<ModernTabControl>() 定位；TabItem.Header 是
//     HeaderedContentControl.Header → ForkPlusDialogWindow_Loaded 的自动本地化
//     （PreferencesLocalization.Apply 递归覆盖 Header/Content/Title）会翻译，按 Tr 断言；
//   - 本地身份：SetRepositoryLocalUserIdentityGitCommand → identity null → --unset、
//     非空（含空串，Quotify("")=`""` 仍是写值参数）→ config --local 写入；
//   - 提交模板：SetLocalCommitTemplateGitCommand → 无既有路径时写
//     <CommonGitDir>/commit_msg_template.txt + config --local commit.template=<绝对路径>；
//     Unset → config --unset commit.template（文件保留）；
//   - bugtracker 规则：SetBugtrackerRulesGitCommand → Shared 级写 <repo>/.issuetracker、
//     Local 级写 <GitDir>/issuetracker（INI 格式 [issuetracker "name"] + regex/url 行，
//     反斜杠转义翻倍，空集删除文件）；GetBugtrackerRules 读两个文件；
//   - ContentContainer.ShowFallback 是叠加式（原 XAML 内容 Grid 不移除，fallback 作为
//     第二个子控件覆盖其上）→ 查找 fallback 必须按 FallbackTitle/FallbackMessage 过滤
//     （ItemFallbackUserControl 恒为视觉后代，FirstOrDefault 裸取会拿错实例）；
//   - 本地自定义命令：SetLocalCustomCommands → 非共享 → <CommonGitDir>/fork/custom-commands.json，
//     共享 → <repo>/.fork/custom-commands.json（按名排序后落盘，CreateCustomCommandName
//     计数命名 "Custom Command"/"Custom Command1"）。
//
// 污染防护（模块 21 全属性反射快照沿用）：仅 IssueTracker 用例改应用级
// ForkPlusSettings.Default.ShowBugtrackerLinks（默认 true）——开头全属性快照 + finally 恢复。
// 仓库级设置（GitModule.Settings：LeanBranchingMainBranch/TabWidth/SignOff/...）与 git
// config --local 全部落在临时仓库内，TestRepoFactory.Cleanup 随仓库销毁，无跨用例污染。
//
// 探针口径（与既有模块一致）：TextBox.Text 赋值后 RunJobs（Avalonia 12 异步派发，模块 18
// 教训）；MenuItem Click 路由事件（模块 21 ClickMenuItem 同款）；模态 MessageBox 确认泵 =
// Dispatcher.Post(Background) 内找窗点按钮（模块 5/13/21 同款）；引用装配等待 =
// WaitForLoaded（模块 11 同款，RepositoryData.References.LocalBranches 是 Refresh 装配
// MainBranch 下拉的硬依赖）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls.RepositorySettings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
// GeneralUserControl 在 Preferences 与 RepositorySettings 两命名空间同名（本文件两者都
// 用到：Preferences 的 CustomCommandsUserControl + RepositorySettings 的设置控件）——
// 别名消歧，仅 LocalBranchItem/LocalBranchItemType 显式类型引用处使用。
using RepoSettingsGeneral = ForkPlus.UI.UserControls.RepositorySettings.GeneralUserControl;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e22RepositorySettingsTests
	{
		private const string ModuleDir = "22-repository-settings";

		// ============================ 共享助手 ============================

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static string TrFormat(string text, params object[] args)
		{
			return E2eMainWindowHarness.TrFormat(text, args);
		}

		/// <summary>Tab 容器（ModernTabControl 无 x:Name，经视觉树定位；TabItem.Header 本地化后
		/// 是 string，未经本地化也是 string——HeaderText 兼容 TextBlock 形态）。</summary>
		private static ModernTabControl TabsOf(ForkPlusDialogWindow dialog)
		{
			ModernTabControl tabs = dialog.GetVisualDescendants().OfType<ModernTabControl>().FirstOrDefault();
			Assert.NotNull(tabs);
			return tabs;
		}

		private static string HeaderText(TabItem tab)
		{
			return (tab.Header as string) ?? (tab.Header as TextBlock)?.Text;
		}

		/// <summary>按标题切 tab（自动本地化会翻译 Header，Tr 与之同源；兜底原文匹配）。</summary>
		private static void SelectTab(ForkPlusDialogWindow dialog, string title)
		{
			ModernTabControl tabs = TabsOf(dialog);
			TabItem tab = tabs.Items.OfType<TabItem>()
				.FirstOrDefault(t => HeaderText(t) == Tr(title) || HeaderText(t) == title);
			Assert.True(tab != null, "找不到 tab: " + title);
			tabs.SelectedItem = tab;
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>RadioButton 生产点击序（模块 21 ClickRadioButton 同款：IsCheckedChanged 绑定的
		/// 处理器赋值即触发，补发 Click 走完整点击管线且无订阅者时无害）。</summary>
		private static void ToggleRadio(global::Avalonia.Controls.Primitives.ToggleButton radio, bool isChecked)
		{
			radio.IsChecked = isChecked;
			radio.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>TextBox 赋值并泵 TextChanged（Avalonia 12 异步派发，模块 18 教训）。</summary>
		private static void SetText(TextBox textBox, string text)
		{
			textBox.Text = text;
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>点击 MenuItem（RaiseEvent 路由事件，模块 21 同款）。</summary>
		private static void ClickMenuItem(MenuItem menuItem)
		{
			menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>等引用与工作区状态装配完成（模块 11 同款——Refresh 装配 MainBranch
		/// 下拉读 RepositoryData.References.LocalBranches）。</summary>
		private static void WaitForLoaded(RepositoryUserControl control, int minLocalBranches)
		{
			Assert.True(UiClick.WaitFor(delegate
			{
				return control.RepositoryData != null
					&& control.RepositoryData.References.LocalBranches.Length >= minLocalBranches
					&& control.RepositoryStatus != null;
			}), "引用/工作区状态未装配（15s 超时）");
		}

		/// <summary>构造仓库设置窗（先真实开仓满足 GeneralUserControl.Refresh 的
		/// MainWindow.ActiveRepositoryUserControl 依赖）。</summary>
		private static RepositorySettingsWindow NewSettingsWindow(RepositoryUserControl repoControl)
		{
			var window = new RepositorySettingsWindow(repoControl.GitModule, repoControl.RepositoryData);
			window.Show();
			Dispatcher.UIThread.RunJobs(); // Initialize 4 tab + footer 装配
			return window;
		}

		/// <summary>点 Close（footer Submit）→ OnSubmit → 四控件 Save（本模块保存管线）。</summary>
		private static void CloseAndSave(RepositorySettingsWindow window)
		{
			UiClick.Click(FooterOf(window).SubmitButton);
			Dispatcher.UIThread.RunJobs();
		}

		/// <summary>git config --local 读值（空/未设 → null）。</summary>
		private static string LocalConfig(string repo, string key)
		{
			try
			{
				string output = TestRepoFactory.GitOutput(repo, "config --local --get " + key).Trim();
				return output.Length == 0 ? null : output;
			}
			catch
			{
				return null;
			}
		}

		// ---------- 应用级设置全属性快照（模块 21 模式，IssueTracker 用例用） ----------

		private sealed class AppSettingsSnapshot
		{
			public Dictionary<string, object> Values = new Dictionary<string, object>();
		}

		private static AppSettingsSnapshot SnapshotAppSettings()
		{
			var snap = new AppSettingsSnapshot();
			foreach (PropertyInfo property in typeof(ForkPlusSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (property.CanWrite && property.GetIndexParameters().Length == 0)
				{
					try
					{
						snap.Values[property.Name] = property.GetValue(ForkPlusSettings.Default);
					}
					catch
					{
					}
				}
			}
			return snap;
		}

		private static void RestoreAppSettings(AppSettingsSnapshot snap)
		{
			try
			{
				foreach (KeyValuePair<string, object> pair in snap.Values)
				{
					typeof(ForkPlusSettings).GetProperty(pair.Key).SetValue(ForkPlusSettings.Default, pair.Value);
				}
				ForkPlusSettings.Default.Save();
			}
			catch (Exception ex)
			{
				Console.WriteLine("[E2e22] 设置恢复失败: " + ex.Message);
			}
		}

		// ============================ 1) General：身份/主分支/杂项 ============================

		[Fact]
		public void GeneralSettings_IdentityAndBranches()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite(); // main + feature 两分支
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 2);
						var gitModule = repoControl.GitModule;

						var dialog = NewSettingsWindow(repoControl);
						try
						{
							// —— 4 tab 结构 + 本地化标题（Tab 头经 ForkPlusDialogWindow_Loaded 自动本地化）——
							ModernTabControl tabs = TabsOf(dialog);
							Assert.Equal(4, tabs.Items.Count);
							Assert.Equal(Tr("General"), HeaderText(tabs.Items.OfType<TabItem>().First()));
							Assert.Equal(Tr("Repository Settings"), dialog.Title);
							Assert.Equal(Tr("Close"), FooterOf(dialog).SubmitButton.Content as string);

							var general = dialog.GeneralUserControl;

							// —— 初态：仓库带本地身份（Init 恒写 user.name=Test）→ 本地模式（不勾全局 + 启用 + 预填）——
							Assert.False(general.UseGlobalGitCredentialsCheckBox.IsChecked == true, "已有本地身份不应勾选全局凭据");
							Assert.True(general.UserNameTextBox.IsEnabled);
							Assert.True(general.EmailTextBox.IsEnabled);
							Assert.Equal("Test", general.UserNameTextBox.Text);
							Assert.Equal("test@example.com", general.EmailTextBox.Text);

							// —— 勾选全局 → SaveLocalIdentity(null) → --unset 本地身份 → Refresh 全局模式（勾选+禁用）——
							UiClick.Toggle(general.UseGlobalGitCredentialsCheckBox, true);
							Assert.True(general.UseGlobalGitCredentialsCheckBox.IsChecked == true);
							Assert.False(general.UserNameTextBox.IsEnabled, "全局凭据模式输入框应禁用");
							Assert.False(general.EmailTextBox.IsEnabled);
							Assert.True(LocalConfig(repo, "user.name") == null, "勾选全局应 unset 本地身份");
							Assert.Null(LocalConfig(repo, "user.email"));

							// —— 取消勾选 → 空串身份写本地（Quotify("") 仍是写值参数）→ Refresh 本地模式 ——
							UiClick.Toggle(general.UseGlobalGitCredentialsCheckBox, false);
							Assert.False(general.UseGlobalGitCredentialsCheckBox.IsChecked == true);
							Assert.True(general.UserNameTextBox.IsEnabled, "本地身份模式输入框应启用");
							Assert.True(general.EmailTextBox.IsEnabled);

							// —— 输入身份（TextChanged 异步 → _saveLocalIdentityRequired）→ Submit 关窗保存 ——
							SetText(general.UserNameTextBox, "Local Repo User");
							SetText(general.EmailTextBox, "local-repo@example.com");
							CloseAndSave(dialog);
							Assert.Equal("Local Repo User", LocalConfig(repo, "user.name"));
							Assert.Equal("local-repo@example.com", LocalConfig(repo, "user.email"));
						}
						finally
						{
							if (dialog.IsVisible)
							{
								dialog.Close();
							}
						}

						// —— 重开：本地身份往返（不勾全局、预填）+ 主分支下拉 ——
						var dialog2 = NewSettingsWindow(repoControl);
						try
						{
							var general2 = dialog2.GeneralUserControl;
							Assert.False(general2.UseGlobalGitCredentialsCheckBox.IsChecked == true, "已有本地身份不应勾选全局");
							Assert.True(general2.UserNameTextBox.IsEnabled);
							Assert.Equal("Local Repo User", general2.UserNameTextBox.Text);
							Assert.Equal("local-repo@example.com", general2.EmailTextBox.Text);

							// —— MainBranch 下拉：默认项（main 探测）+ 分支项装配 ——
							var branchItems = general2.MainBranchComboBox.Items
								.OfType<RepoSettingsGeneral.LocalBranchItem>().ToList();
							Assert.Equal(4, branchItems.Count); // 默认项 + 分隔符 + main + feature
							Assert.Equal(RepoSettingsGeneral.LocalBranchItemType.Default, branchItems[0].ItemType);
							Assert.Equal(RepoSettingsGeneral.LocalBranchItemType.Separator, branchItems[1].ItemType);
							Assert.Equal(TrFormat("default ({0})", "main"), branchItems[0].Title);
							var featureItem = branchItems.First(i => i.ItemType == RepoSettingsGeneral.LocalBranchItemType.Branch && i.Title == "feature");
							// 初值未设 LeanBranchingMainBranch（默认 null）→ 默认项选中
							Assert.Equal(branchItems[0], general2.MainBranchComboBox.SelectedItem);

							// —— 选择 feature → 设置保存 ——
							general2.MainBranchComboBox.SelectedItem = featureItem;
							Dispatcher.UIThread.RunJobs();
							Assert.Equal("feature", gitModule.Settings.LeanBranchingMainBranch);

							// —— NoFastForward 开关（IsCheckedChanged 生产管线）——
							UiClick.Toggle(general2.NoFastForwardCheckBox, true);
							Assert.True(gitModule.Settings.LeanBranchingNoFastForward);

							// —— TabWidth：合法值写回 + 非数字回退保留旧值 ——
							SetText(general2.TabWidthTextBox, "8");
							Assert.Equal(8, gitModule.Settings.TabWidth);
							SetText(general2.TabWidthTextBox, "abc");
							Assert.Equal(8, gitModule.Settings.TabWidth); // 解析失败回退当前值

							CloseAndSave(dialog2);
						}
						finally
						{
							if (dialog2.IsVisible)
							{
								dialog2.Close();
							}
						}

						// —— 主分支设置持久化往返：新 GitModule 从磁盘重载（同模块实例的 Settings 是
							// 惰性缓存，同实例断言只证内存）——
							var freshModule = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
						Assert.Equal("feature", freshModule.Settings.LeanBranchingMainBranch);
						Assert.True(freshModule.Settings.LeanBranchingNoFastForward);
						Assert.Equal(8, freshModule.Settings.TabWidth);

						// —— 截图：General tab ——
						var dialog3 = NewSettingsWindow(repoControl);
						try
						{
							ScreenshotHelper.Snap(dialog3, "01-general-tab", ModuleDir);
						}
						finally
						{
							dialog3.Close();
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 2) Commit：模板与开关 ============================

		[Fact]
		public void CommitSettings_TemplateAndSwitches()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 2);
						var gitModule = repoControl.GitModule;

						var dialog = NewSettingsWindow(repoControl);
						try
						{
							var commit = dialog.CommitTemplateUserControl;

							// —— 初态：无本地模板（全局也无）→ 勾选全局 + 禁用 + 空 ——
							Assert.True(commit.UseGlobalCommitTemplateCheckBox.IsChecked == true, "无本地模板应默认使用全局配置");
							Assert.False(commit.CommitTemplateTextBox.IsEnabled);
							Assert.Equal("", commit.CommitTemplateTextBox.Text);
							Assert.Equal("", commit.CommitTemplatePathTextBlock.Text);

							// —— SignOff / SkipCommitMessage 开关（IsCheckedChanged 生产管线）——
							UiClick.Toggle(commit.AddSignedOffMessageCheckBox, true);
							Assert.True(gitModule.Settings.SignOff);
							UiClick.Toggle(commit.SkipCommitMessageCheckBox, true);
							Assert.True(gitModule.Settings.SkipCommitMessage);
							Assert.False(commit.CommitMessageRegexTextBox.IsEnabled, "跳过提交消息时正则框应禁用");
							UiClick.Toggle(commit.SkipCommitMessageCheckBox, false);
							Assert.True(commit.CommitMessageRegexTextBox.IsEnabled, "恢复时正则框应启用");

							// —— 正则写回（TextChanged）——
							SetText(commit.CommitMessageRegexTextBox, "^(feat|fix): .+");
							Assert.Equal("^(feat|fix): .+", gitModule.Settings.CommitMessageRegex);

							// —— 取消勾选全局 → SaveCommitTemplate（空内容）+ Refresh → 本地模板模式 ——
							UiClick.Toggle(commit.UseGlobalCommitTemplateCheckBox, false);
							Assert.False(commit.UseGlobalCommitTemplateCheckBox.IsChecked == true);
							Assert.True(commit.CommitTemplateTextBox.IsEnabled, "本地模板模式编辑框应启用");
							Assert.NotEqual("", commit.CommitTemplatePathTextBlock.Text); // .git/commit_msg_template.txt
							Assert.NotNull(LocalConfig(repo, "commit.template"));

							// —— 输入模板内容 → Close 保存 → 文件 + config 双断言 ——
							SetText(commit.CommitTemplateTextBox, "Template line one\n\nSigned-off-by: template");
							CloseAndSave(dialog);
							string templateFile = Path.Combine(Path.Combine(repo, ".git"), "commit_msg_template.txt");
							Assert.True(File.Exists(templateFile), "模板文件应已写入 .git/commit_msg_template.txt");
							string content = File.ReadAllText(templateFile);
							Assert.Contains("Template line one", content);
							Assert.Contains("Signed-off-by: template", content);
							// commit.template 写的是 CommonGitDir 绝对路径（SetLocalCommitTemplateGitCommand
							// 的 Path.Combine(gitModule.CommonGitDir, ...)），非相对路径
							string templateConfig = LocalConfig(repo, "commit.template");
							Assert.True(templateConfig != null && templateConfig.EndsWith("commit_msg_template.txt", StringComparison.Ordinal),
								"commit.template 应指向模板文件，实际: " + templateConfig);
						}
						finally
						{
							if (dialog.IsVisible)
							{
								dialog.Close();
							}
						}

						// —— 重开：本地模板内容往返 ——
						var dialog2 = NewSettingsWindow(repoControl);
						try
						{
							var commit2 = dialog2.CommitTemplateUserControl;
							Assert.False(commit2.UseGlobalCommitTemplateCheckBox.IsChecked == true, "本地模板存在不应勾选全局");
							Assert.True(commit2.CommitTemplateTextBox.IsEnabled);
							Assert.Contains("Template line one", commit2.CommitTemplateTextBox.Text);
							Assert.Contains("Signed-off-by: template", commit2.CommitTemplateTextBox.Text);

							// —— 重勾全局 → UnsetLocalCommitTemplate（config 清除，文件保留）——
							UiClick.Toggle(commit2.UseGlobalCommitTemplateCheckBox, true);
							Assert.Null(LocalConfig(repo, "commit.template"));
							CloseAndSave(dialog2);
						}
						finally
						{
							if (dialog2.IsVisible)
							{
								dialog2.Close();
							}
						}

						// —— 截图：Commit tab ——
						var dialog3 = NewSettingsWindow(repoControl);
						try
						{
							SelectTab(dialog3, "Commit");
							ScreenshotHelper.Snap(dialog3, "02-commit-tab", ModuleDir);
						}
						finally
						{
							dialog3.Close();
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 3) IssueTracker：禁用 fallback 双层 ============================

		[Fact]
		public void IssueTracker_FallbackStates()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			var snap = SnapshotAppSettings(); // 应用级 ShowBugtrackerLinks 被改
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 2);

						// —— 应用级禁用：整个 tab fallback（"Issue Tracker Integration is disabled"）——
						ForkPlusSettings.Default.ShowBugtrackerLinks = false;
						var dialog = NewSettingsWindow(repoControl);
						try
						{
							var tracker = dialog.IssueTrackerUserControl;
							// ContentContainer.ShowFallback 是叠加式（ItemFallbackUserControl 恒为视觉后代）
							// → 按标题精确定位
							FallbackUserControl appFallback = tracker.ContentContainer
								.GetVisualDescendants().OfType<FallbackUserControl>()
								.FirstOrDefault(f => f.FallbackTitle == Tr("Issue Tracker Integration is disabled"));
							Assert.True(appFallback != null, "应用级禁用应显示整 tab fallback");
							Assert.Equal(Tr("Enable Issue Tracker Integration in Fork preferences"), appFallback.FallbackMessage);
							ScreenshotHelper.Snap(dialog, "03-issue-tracker-app-disabled", ModuleDir);
						}
						finally
						{
							dialog.Close();
						}

						// —— 应用级启用 + 仓库级禁用：内容 fallback（"for '{repo}' is disabled"）——
						ForkPlusSettings.Default.ShowBugtrackerLinks = true;
						repoControl.GitModule.Settings.ShowBugtrackerLinks = false;
						var dialog2 = NewSettingsWindow(repoControl);
						try
						{
							var tracker2 = dialog2.IssueTrackerUserControl;
							string disabledMessage = TrFormat("Issue tracker integration for '{0}' is disabled",
								repoControl.GitModule.RepositoryName);
							FallbackUserControl repoFallback = tracker2.ContentContainer
								.GetVisualDescendants().OfType<FallbackUserControl>()
								.FirstOrDefault(f => f.FallbackMessage == disabledMessage);
							Assert.True(repoFallback != null, "仓库级禁用应显示内容 fallback");
							// IsEnabled 复选框本身可见（未被 fallback 替换——ContentContainer 只包内容区）
							Assert.True(tracker2.IsEnabledCheckbox.IsVisible);

							// —— 勾选启用 → RefreshContentFallback → ShowContent() 移除 overlay fallback ——
							UiClick.Toggle(tracker2.IsEnabledCheckbox, true);
							Assert.True(repoControl.GitModule.Settings.ShowBugtrackerLinks);
							FallbackUserControl stillFallback = tracker2.ContentContainer
								.GetVisualDescendants().OfType<FallbackUserControl>()
								.FirstOrDefault(f => f.FallbackMessage == disabledMessage);
							Assert.Null(stillFallback); // ShowContent() 清除叠加 fallback 子控件
							Assert.True(tracker2.BugTrackerRulesListBox.IsVisible);
						}
						finally
						{
							dialog2.Close();
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				RestoreAppSettings(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 4) IssueTracker：规则 CRUD 与持久化 ============================

		[Fact]
		public void IssueTracker_RulesCrudAndPersistence()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 2);
						repoControl.GitModule.Settings.ShowBugtrackerLinks = true; // 仓库级启用

						var dialog = NewSettingsWindow(repoControl);
						try
						{
							var tracker = dialog.IssueTrackerUserControl;

							// —— 空规则：ItemFallback 显示 ——
							Assert.True(tracker.ItemFallbackUserControl.IsVisible, "无规则时应显示条目空态");
							Assert.Empty(tracker.BugTrackerRulesListBox.Items.OfType<object>());

							// —— 添加 Sample GitHub 规则（MenuItem Click 路由）→ 选中 + 字段装配 ——
							ClickMenuItem(tracker.SampleGithubRuleMenuItem);
							var rules = tracker.BugTrackerRulesListBox.Items.OfType<BugtrackerRuleViewModel>().ToList();
							Assert.Single(rules);
							Assert.Equal("Sample GitHub Rule", rules[0].Name);
							Assert.False(tracker.ItemFallbackUserControl.IsVisible);
							Assert.Equal("Sample GitHub Rule", tracker.NameTextBox.Text);
							Assert.Equal("#(\\d+)", tracker.RegexTextBox.Text);
							Assert.Equal("https://github.com/username/repository/issues/$1", tracker.UrlTextBox.Text);
							Assert.True(tracker.LocalRadioButton.IsChecked == true, "示例规则默认 Local 级");
							Assert.Equal(".git/issuetracker", tracker.RuleLocationTextBlock.Text);

							// —— regex 校验状态：示例合法 → valid；改非法 → invalid ——
							Assert.Equal(Tr("valid"), tracker.RegexStatusTextBlock.Text);
							SetText(tracker.RegexTextBox, "([invalid");
							Assert.Equal(Tr("invalid"), tracker.RegexStatusTextBlock.Text);
							SetText(tracker.RegexTextBox, "#(\\d+)"); // 还原

							// —— 切 Shared → 位置文本 .issuetracker + 规则标记共享 ——
							ToggleRadio(tracker.SharedRadioButton, true);
							Assert.Equal(".issuetracker", tracker.RuleLocationTextBlock.Text);
							Assert.True(tracker.SharedRadioButton.IsChecked == true);

							// —— 关窗保存 → Shared 级写 <repo>/.issuetracker（INI 格式）——
							CloseAndSave(dialog);
							string sharedFile = Path.Combine(repo, ".issuetracker");
							Assert.True(File.Exists(sharedFile), "Shared 级规则应写入 <repo>/.issuetracker");
							string ini = File.ReadAllText(sharedFile);
							Assert.Contains("[issuetracker \"Sample GitHub Rule\"]", ini);
							Assert.Contains("regex = \"#(\\\\d+)\"", ini);
							Assert.Contains("url = \"https://github.com/username/repository/issues/$1\"", ini);
							Assert.False(File.Exists(Path.Combine(Path.Combine(repo, ".git"), "issuetracker")),
								"无 Local 级规则不应写 .git/issuetracker");
						}
						finally
						{
							if (dialog.IsVisible)
							{
								dialog.Close();
							}
						}

						// —— 重开：规则从文件往返装配 ——
						var dialog2 = NewSettingsWindow(repoControl);
						try
						{
							var tracker2 = dialog2.IssueTrackerUserControl;
							var reloaded = tracker2.BugTrackerRulesListBox.Items.OfType<BugtrackerRuleViewModel>().ToList();
							Assert.Single(reloaded);
							Assert.Equal("Sample GitHub Rule", reloaded[0].Name);
							Assert.Equal("#(\\d+)", tracker2.RegexTextBox.Text);
							Assert.True(tracker2.SharedRadioButton.IsChecked == true, "Shared 级应从文件恢复");
							ScreenshotHelper.Snap(dialog2, "04-issue-tracker-rule", ModuleDir);

							// —— 删除确认泵：确认 → 规则删除 + 空态 + 文件移除 ——
							var removeHandled = new bool[1];
							var removeError = new string[1];
							Dispatcher.UIThread.Post(delegate
							{
								try
								{
									MessageBoxWindow msgBox = global::ForkPlus.UI.WpfCompat.WpfApp.Windows
										.OfType<MessageBoxWindow>().FirstOrDefault();
									if (msgBox == null)
									{
										removeError[0] = "删除确认框未出现";
										return;
									}
									Button remove = UiClick.FindAll<Button>(msgBox)
										.FirstOrDefault(b => UiClick.ContentText(b) == Tr("Remove"));
									if (remove == null)
									{
										removeError[0] = "确认框中找不到 Remove 按钮";
										return;
									}
									remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
									removeHandled[0] = true;
								}
								catch (Exception ex)
								{
									removeError[0] = ex.ToString();
								}
							}, DispatcherPriority.Background);
							UiClick.Click(tracker2.RemoveRuleButton);
							Assert.True(removeHandled[0], "删除确认框处理器未执行：" + removeError[0]);
							Assert.Null(removeError[0]);
							Dispatcher.UIThread.RunJobs();

							Assert.Empty(tracker2.BugTrackerRulesListBox.Items.OfType<object>());
							Assert.True(tracker2.ItemFallbackUserControl.IsVisible, "删空后应显示条目空态");
							CloseAndSave(dialog2);
							Assert.False(File.Exists(Path.Combine(repo, ".issuetracker")), "空规则集应删除 .issuetracker 文件");
						}
						finally
						{
							if (dialog2.IsVisible)
							{
								dialog2.Close();
							}
						}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// ============================ 5) CustomCommands：本地模式 ============================

		[Fact]
		public void CustomCommands_LocalMode()
		{
			string repo = TestRepoFactory.CreateHistoryRewrite();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						WaitForLoaded(repoControl, 2);

						var dialog = NewSettingsWindow(repoControl);
						try
						{
							var commands = dialog.CustomCommandsUserControl;

							// —— 本地模式：Location 容器显示（对比全局模式折叠，模块 21 反向断言）——
							Assert.True(commands.LocationRadioButtonContainer.IsVisible, "本地模式应显示 Local/Shared 位置选择");
							Assert.True(commands.FallbackUserControl.IsVisible, "无本地命令时空态");

							// —— 添加修订命令（默认本地）→ 位置文本 .git/fork/custom-commands.json ——
							ClickMenuItem(commands.RevisionCustomCommandMenuItem);
							var items = commands.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
							Assert.Single(items);
							Assert.True(commands.LocalRadioButton.IsChecked == true, "新命令默认本地");
							Assert.Equal(".git/fork/custom-commands.json", commands.LocationTextBlock.Text);
							Assert.False(commands.OSComboBox.IsVisible, "本地命令无 OS 选择");

							// —— 再添加仓库命令并切 Shared → .fork/custom-commands.json + OS 下拉显示 ——
						ClickMenuItem(commands.RepositoryCustomCommandMenuItem);
						items = commands.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
						Assert.Equal(2, items.Count);
						ToggleRadio(commands.SharedRadioButton, true);
						Assert.True(commands.SharedRadioButton.IsChecked == true);
						Assert.Equal(".fork/custom-commands.json", commands.LocationTextBlock.Text);
						Assert.True(commands.OSComboBox.IsVisible, "共享命令有 OS 选择");

							// —— 关窗保存：本地 → .git/fork/、共享 → .fork/ 双文件 ——
							CloseAndSave(dialog);
							string localFile = Path.Combine(Path.Combine(repo, ".git"), "fork", "custom-commands.json");
							string sharedFile = Path.Combine(Path.Combine(repo, ".fork"), "custom-commands.json");
							Assert.True(File.Exists(localFile), "本地命令应写入 .git/fork/custom-commands.json");
							Assert.True(File.Exists(sharedFile), "共享命令应写入 .fork/custom-commands.json");
							List<string> localNames = ReadCommandNames(localFile);
							List<string> sharedNames = ReadCommandNames(sharedFile);
							Assert.Single(localNames);
							Assert.Single(sharedNames);
							Assert.Equal(Tr("Custom Command"), localNames[0]); // 第一条（本地）
							Assert.Equal(Tr("Custom Command") + "1", sharedNames[0]); // 第二条（共享）
						}
						finally
						{
							if (dialog.IsVisible)
							{
								dialog.Close();
							}
						}

						// —— 截图：Custom Commands tab（重开窗展示命令列表 + 位置面板）——
					var dialog2 = NewSettingsWindow(repoControl);
					try
					{
						SelectTab(dialog2, "Custom Commands");
						ScreenshotHelper.Snap(dialog2, "05-custom-commands-local", ModuleDir);
					}
					finally
					{
						dialog2.Close();
					}
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		/// <summary>从命令 JSON 文件解出命令名列表（按名排序后落盘——SetLocal/Global 的排序语义）。</summary>
		private static List<string> ReadCommandNames(string path)
		{
			JArray array = JsonConvert.DeserializeObject(File.ReadAllText(path)) as JArray;
			return array.Cast<JObject>()
				.Where(o => o["name"] != null)
				.Select(o => o["name"].Value<string>())
				.ToList();
		}
	}
}
