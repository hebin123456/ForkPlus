// E2E 模块21（2026-09-06）：偏好设置窗口（PreferencesWindow，7 tab，9 用例）。
// 覆盖：General（开关生产点击序 → ForkPlusSettings 写回 + RevisionSortOrder 单选 + 语言
// ComboBox 装配与切换重本地化）/Commit（拼写检查 ComboBox 三态 + 长度指示/分页线/正则
// TextChanged 解析与非数字回退）/Git（ENV git 实例下拉选中且禁用（测试环境 forkgitinstance
// env var 探测链）+ git-mm/git-ai 空环境装配 + 全局身份 TextBox → LostFocus →
// SetGlobalUserIdentityGitCommand 真实写 git config --global + AI 归属/verbose 开关）/
// Integration（Shell ComboBox 5 项装配 + 切换联动路径/参数框 + ShowBugtrackerLinks +
// OnSubmit → Save 全链路）/CustomCommands（添加菜单（修订/仓库两类）+ 自动命名计数 +
// Target 切换联动 + Submit 落盘 custom-commands.json + 二次构造重载（持久化往返）+
// 删除（MessageBox 模态确认泵：确认删/取消留））/AI Review（ServiceUrl 归一化
// （/v1|/v1/chat/completions|尾斜杠剥离）+ 重试/超时解析回退与下限 + 自定义技能
// 添加/同名更新/自动命名/删除 + 行号联动 + AiDevSkillList JSON 持久化）/
// ImportExport（headless 存储提供器 Noop → 导出/导入双取消路径：无 zip、无重启、
// 状态条不变）。
//
// 模式：PreferencesWindow 直构（生产入口 ShowPreferencesWindowCommand 即 new + ShowDialog，
// 构造器内 Initialize() 全量初始化 7 个 tab——所有控件经 x:Name 字段可达，无需切 tab 即可
// 断言；切 tab 仅为了截图与"内容进视觉树"（AI 技能输入框是代码构建的私有 TextBox，
// 经 CustomSkillInputBorder 视觉子树定位）。截图走 2026-09-06 口径：主窗口 1920×1080，弹窗按自然比例。
//
// 设置污染防护（模块 7/12/14/19 教训沿用并扩展）：
//   ① ForkPlusSettings 内存快照 + finally 恢复 + Save()——偏好设置的每个 tab 都直写设置，
//      且多处处理器自带 Save()（UndoRedo/AiAttribution/GitAiInstance 选择等）；
//   ② custom-commands.json 整文件备份 + finally 恢复（Submit 必经
//      CustomCommandsUserControl.Save() → SetGlobalCustomCommands 落盘；文件原先不存在时
//      删除重建的 marker 文件而非留残留）；
//   ③ CustomCommandManager 单例 _current 反射置 null——finally 只还原磁盘文件不清内存缓存
//      会让同进程后续用例（含模块 22+ 仓库设置）读到测试残留命令；
//   ④ git config --global user.name/email 快照恢复（Git tab 身份测试真实写全局配置）。
//
// 探针口径（与既有模块一致）：
//   - CheckBox 用 IsCheckedChanged 绑定 → UiClick.Toggle（赋值即触发）；
//   - RadioButton 用 Click 绑定（RevisionSortOrder_Changed）→ 赋 IsChecked + 补发 Click
//     （模块 10 UiClick.Toggle 同款生产点击序，RadioButton 非 CheckBox 类型故手工展开）；
//   - TextBox.Text 赋值后必须 RunJobs（Avalonia 12 TextChanged 异步派发，模块 18 教训）；
//   - 模态 MessageBox 确认泵 = Dispatcher.Post(Background) 内找窗点按钮（模块 5/13 同款）；
//   - ForkPlusDialogFooter.SubmitButton.Click → Submit 事件 → OnSubmit（OnSubmit 里
//     Integration/AI/CustomCommands Save + ForkPlusSettings.Save 后关窗——关窗后控件对象
//     仍存活可断言，但断言设置值即可）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e21PreferencesTests
	{
		private const string ModuleDir = "21-preferences";

		// ============================ 共享助手 ============================

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		/// <summary>
		/// 外部工具（git-mm / git-ai）实例下拉的环境自适应不变量断言：
		/// 无候选（effectivePath 为 null，工具未安装）→ 恰 2 项（分隔符 + 添加自定义）且无选中；
		/// 有候选 → ≥3 项（候选项 + 分隔符 + 添加自定义）且选中项路径与当前生效路径一致
		/// （与 GitUserControl 选中逻辑同口径——按 OrdinalIgnoreCase 匹配，此处直接等值断言）。
		/// </summary>
		private static void AssertExternalToolCombo(global::Avalonia.Controls.ComboBox combo, string effectivePath)
		{
			int count = combo.ItemsSource.Cast<object>().Count();
			if (effectivePath == null)
			{
				Assert.Equal(2, count);
				Assert.Null(combo.SelectedItem);
				return;
			}
			Assert.True(count >= 3, "外部工具已安装时下拉应含候选项（候选 + 分隔符 + 添加自定义），实际项数: " + count);
			var item = combo.SelectedItem as GitUserControl.GitInstanceItem;
			Assert.NotNull(item);
			Assert.Equal(effectivePath, item.GitPath);
		}

		private static PreferencesWindow NewPrefsWindow()
		{
			var window = new PreferencesWindow();
			window.Show();
			Dispatcher.UIThread.RunJobs(); // footer 装配 + Loaded
			return window;
		}

		/// <summary>RadioButton 生产点击序：赋 IsChecked + 补发 Click（Click 绑定的处理器
		/// 在程序化赋值时不触发——WPF/Avalonia 同语义；模块 10 Toggle 教训的 RadioButton 展开）。</summary>
		private static void ClickRadioButton(global::Avalonia.Controls.Primitives.ToggleButton radio, bool isChecked)
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

		/// <summary>点击 MenuItem（RaiseEvent 路由事件，走 Click="..." 绑定的处理器管线）。</summary>
		private static void ClickMenuItem(MenuItem menuItem)
		{
			menuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
			Dispatcher.UIThread.RunJobs();
		}

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static string TrFormat(string text, params object[] args)
		{
			return E2eMainWindowHarness.TrFormat(text, args);
		}

		// ---------- 设置/文件快照（污染防护①②③） ----------

		private sealed class PrefsSnapshot
		{
			public Dictionary<string, object> Values = new Dictionary<string, object>();
			public bool CustomCommandsFileExisted;
			public string CustomCommandsContent;
		}

		/// <summary>全属性反射快照。按名单快照漏一个属性就跨进程污染（2026-09-06 实证：
		/// CodeEditorFontSize 被用例改成 10 后经处理器 Save() 落盘 settings.json，下一个
		/// dotnet test 进程加载到残留值——首轮通过、次轮同用例失败）。偏好设置的每个 tab
		/// 都直写设置且多处自带 Save()，名单方式无法穷举，故全量捕获。</summary>
		private static PrefsSnapshot SnapshotPrefs()
		{
			var snap = new PrefsSnapshot();
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
						// 个别 getter 抛异常（设计器态/懒加载）时跳过该属性
					}
				}
			}
			string path = CustomCommandManager.GlobalPath();
			snap.CustomCommandsFileExisted = File.Exists(path);
			snap.CustomCommandsContent = snap.CustomCommandsFileExisted ? File.ReadAllText(path) : null;
			return snap;
		}

		private static void RestorePrefs(PrefsSnapshot snap)
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
				Console.WriteLine("[E2e21] 设置恢复失败: " + ex.Message);
			}
			try
			{
				string path = CustomCommandManager.GlobalPath();
				if (snap.CustomCommandsFileExisted)
				{
					File.WriteAllText(path, snap.CustomCommandsContent);
				}
				else if (File.Exists(path))
				{
					File.Delete(path);
				}
				// 单例缓存与磁盘文件同步重置：置 null 强制下次访问按恢复后的文件重载
				FieldInfo field = typeof(CustomCommandManager).GetField("_current",
					BindingFlags.NonPublic | BindingFlags.Static);
				field?.SetValue(null, null);
			}
			catch (Exception ex)
			{
				Console.WriteLine("[E2e21] custom-commands.json 恢复失败: " + ex.Message);
			}
		}

		/// <summary>从磁盘 custom-commands.json 解出命令名列表（Save 后的文件断言）。</summary>
		private static List<string> ReadCustomCommandNames()
		{
			string path = CustomCommandManager.GlobalPath();
			Assert.True(File.Exists(path), "custom-commands.json 应存在（Submit 后落盘）");
			JArray array = JsonConvert.DeserializeObject(File.ReadAllText(path)) as JArray;
			return array.Cast<JObject>()
				.Where(o => o["name"] != null)
				.Select(o => o["name"].Value<string>())
				.ToList();
		}

		// ============================ 1) General：结构与开关 ============================

		[Fact]
		public void PreferencesWindow_TabsAndGeneralToggles()
		{
			var snap = SnapshotPrefs();
			try
			{
				// 确定性初态
			ForkPlusSettings.Default.FetchRemotesAutomatically = false;
			ForkPlusSettings.Default.PushAutomaticallyOnCommit = false;
			ForkPlusSettings.Default.UndoRedoEnabled = false;
			ForkPlusSettings.Default.RevisionSortOrder = RevisionSortOrder.Date;
			ForkPlusSettings.Default.DisableSyntaxHighlighting = false;
			ForkPlusSettings.Default.CodeEditorFontSize = 13.0;

				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						// —— 8 tab 结构 + 本地化标题/按钮（2026-09-07 Credentials 页：凭据记忆管理） ——
					Assert.Equal(8, window.PreferencesTabControl.Items.Count);
					Assert.Equal(Tr("General"), window.GeneralTabItem.Header);
					Assert.Equal(Tr("Commit"), window.CommitTabItem.Header);
					Assert.Equal(Tr("AI Enhancement"), window.AiReviewTabItem.Header);
					Assert.Equal(Tr("Git"), window.GitTabItem.Header);
					Assert.Equal(Tr("Credentials"), window.CredentialsTabItem.Header);
					Assert.Equal(Tr("Integration"), window.IntegrationTabItem.Header);
					Assert.Equal(Tr("Custom Commands"), window.CustomCommandsTab.Header);
					Assert.Equal(Tr("Import/Export"), window.ImportExportTab.Header);
						Assert.Equal(Tr("Preferences"), window.Title);
						Assert.Equal(Tr("Close"), FooterOf(window).SubmitButton.Content as string);

						// —— 初值装配（Initialize 读设置 → 控件）——
						var general = window.GeneralUserControl;
						Assert.False(general.FetchRemotesAutomaticallyCheckBox.IsChecked == true);
						Assert.True(general.DateSortOrderRadioButton.IsChecked == true, "初值 Date 应选中日期排序单选");
						Assert.False(general.TopologicalSortOrderRadioButton.IsChecked == true);
						Assert.Equal("13", general.CodeEditorFontSizeTextBox.Text); // CodeEditorFontSize 默认 13

						// —— 开关生产点击序 → 设置写回 ——
						UiClick.Toggle(general.FetchRemotesAutomaticallyCheckBox, true);
						Assert.True(ForkPlusSettings.Default.FetchRemotesAutomatically, "勾选应写回设置");
						UiClick.Toggle(general.PushAutomaticallyOnCommitCheckBox, true);
						Assert.True(ForkPlusSettings.Default.PushAutomaticallyOnCommit);
						UiClick.Toggle(general.UndoRedoEnabledCheckBox, true);
						Assert.True(ForkPlusSettings.Default.UndoRedoEnabled);
						UiClick.Toggle(general.DisableSyntaxHighlightingCheckBox, true);
						Assert.True(ForkPlusSettings.Default.DisableSyntaxHighlighting);
						// 关闭路径（Unchecked 同管线）
						UiClick.Toggle(general.FetchRemotesAutomaticallyCheckBox, false);
						Assert.False(ForkPlusSettings.Default.FetchRemotesAutomatically);

						// —— 排序单选（Click 绑定：生产点击序 = IsChecked + Click）——
						ClickRadioButton(general.TopologicalSortOrderRadioButton, true);
						Assert.Equal(RevisionSortOrder.Topo, ForkPlusSettings.Default.RevisionSortOrder);
						ClickRadioButton(general.DateSortOrderRadioButton, true);
						Assert.Equal(RevisionSortOrder.Date, ForkPlusSettings.Default.RevisionSortOrder);

						// —— Space 字符 ComboBox 装配 + 选择写回 ——
						Assert.True(general.SpaceCharacterComboBox.Items.Count > 0, "空格替换字符下拉应装配");
						object secondItem = general.SpaceCharacterComboBox.Items.OfType<object>().Skip(1).FirstOrDefault();
						general.SpaceCharacterComboBox.SelectedItem = secondItem;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(secondItem, ForkPlusSettings.Default.ReferenceSpaceCharacterReplacement);

						// —— 字号边界：超出上限夹到 40 ——
						SetText(general.CodeEditorFontSizeTextBox, "99");
						Assert.Equal(40.0, ForkPlusSettings.Default.CodeEditorFontSize);
						SetText(general.CodeEditorFontSizeTextBox, "3");
						Assert.Equal(10.0, ForkPlusSettings.Default.CodeEditorFontSize); // 下限 10

						ScreenshotHelper.Snap(window, "01-general-tab", ModuleDir);
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ 2) General：语言切换 ============================

		[Fact]
		public void GeneralSettings_LanguageSwitchRelocalizes()
		{
			string originalLanguage = ForkPlusSettings.Default.UiLanguage;
			try
			{
				ForkPlusSettings.Default.UiLanguage = "en"; // 确定性英文起点
				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var general = window.GeneralUserControl;

						// —— 语言下拉装配：内置 8 语言（DisplayName + Tag=code）——
						Assert.True(general.LanguageComboBox.Items.Count >= 8,
							"语言下拉应至少含 8 个内置语言（实际 " + general.LanguageComboBox.Items.Count + "）");
						var items = general.LanguageComboBox.Items.Cast<ComboBoxItem>().ToList();
						Assert.Contains(items, i => (i.Tag as string) == "zh-Hans" && (i.Content as string) == "简体中文");
						Assert.Contains(items, i => (i.Tag as string) == "en" && (i.Content as string) == "English");
						// 当前语言选中
						Assert.Equal("en", (general.LanguageComboBox.SelectedItem as ComboBoxItem)?.Tag as string);
						// 初始英文标题
						Assert.Equal("General", window.GeneralTabItem.Header);
						Assert.Equal("Git", window.GitTabItem.Header);

						// —— 切到简体中文：SelectionChanged → UiLanguage 写回 + ApplyLocalization 重本地化 ——
						ComboBoxItem zhItem = items.First(i => (i.Tag as string) == "zh-Hans");
						general.LanguageComboBox.SelectedItem = zhItem;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("zh-Hans", ForkPlusSettings.Default.UiLanguage);
						// 断言用生产查找口径（PreferencesLocalization.Translate），不硬编码译文
						string zhGeneral = PreferencesLocalization.Translate("General", "zh-Hans");
						string zhGit = PreferencesLocalization.Translate("Git", "zh-Hans");
						Assert.NotEqual("General", zhGeneral); // zh-Hans 译文应存在且不同
						Assert.Equal(zhGeneral, window.GeneralTabItem.Header);
						Assert.Equal(zhGit, window.GitTabItem.Header);
						// 关闭按钮标题也随 ApplyLocalization 重本地化
						Assert.Equal(PreferencesLocalization.Translate("Close", "zh-Hans"),
							FooterOf(window).SubmitButton.Content as string);

						// —— 再切回英文：往返 ——
						ComboBoxItem enItem = items.First(i => (i.Tag as string) == "en");
						general.LanguageComboBox.SelectedItem = enItem;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal("en", ForkPlusSettings.Default.UiLanguage);
						Assert.Equal("General", window.GeneralTabItem.Header);

						ScreenshotHelper.Snap(window, "02-language-combo", ModuleDir);
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.UiLanguage = originalLanguage;
				ForkPlusSettings.Default.Save();
			}
		}

		// ============================ 3) Commit：解析与持久化 ============================

		[Fact]
		public void CommitSettings_ParsingAndPersistence()
		{
			var snap = SnapshotPrefs();
			try
			{
				ForkPlusSettings.Default.CommitSpellCheckingMode = CommitSpellCheckingMode.Disable;
				ForkPlusSettings.Default.CommitSubjectLowLimit = 50;
				ForkPlusSettings.Default.CommitSubjectHighLimit = 70;
				ForkPlusSettings.Default.PageGuideLinePosition = 72;
				ForkPlusSettings.Default.CommitMessageRegex = "";

				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var commit = window.CommitUserControl;

						// —— 初值装配 ——
						Assert.Equal(window.CommitUserControl.DisableComboBoxItem, commit.CommintSpellCheckingComboBox.SelectedItem);
						Assert.Equal("50", commit.CommitSubjectLowLimitTextBox.Text);
						Assert.Equal("70", commit.CommitSubjectHighLimitTextBox.Text);
						Assert.Equal("72", commit.PageGuideLinePositionTextBox.Text);

						// —— 拼写检查三态切换（SelectionChanged 生产管线）——
						commit.CommintSpellCheckingComboBox.SelectedItem = commit.EnglishComboBoxItem;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(CommitSpellCheckingMode.English, ForkPlusSettings.Default.CommitSpellCheckingMode);
						commit.CommintSpellCheckingComboBox.SelectedItem = commit.SystemComboBoxItem;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(CommitSpellCheckingMode.System, ForkPlusSettings.Default.CommitSpellCheckingMode);

						// —— 长度指示：有效值 + 非数字回退（50/70）——
						SetText(commit.CommitSubjectLowLimitTextBox, "42");
						Assert.Equal(42, ForkPlusSettings.Default.CommitSubjectLowLimit);
						SetText(commit.CommitSubjectHighLimitTextBox, "99");
						Assert.Equal(99, ForkPlusSettings.Default.CommitSubjectHighLimit);
						SetText(commit.CommitSubjectLowLimitTextBox, "abc");
						Assert.Equal(50, ForkPlusSettings.Default.CommitSubjectLowLimit);
						SetText(commit.CommitSubjectHighLimitTextBox, "xyz");
						Assert.Equal(70, ForkPlusSettings.Default.CommitSubjectHighLimit);

						// —— 分页线：有效值 + 非数字回退 72 ——
						SetText(commit.PageGuideLinePositionTextBox, "100");
						Assert.Equal(100, ForkPlusSettings.Default.PageGuideLinePosition);
						SetText(commit.PageGuideLinePositionTextBox, "junk");
						Assert.Equal(72, ForkPlusSettings.Default.PageGuideLinePosition);

						// —— 提交消息正则 ——
						SetText(commit.CommitMessageRegexTextBox, "^(fix|feat): .+");
						Assert.Equal("^(fix|feat): .+", ForkPlusSettings.Default.CommitMessageRegex);

						// 切到 Commit tab 截图（TabControl_SelectionChanged → 状态清空本地化应用）
						window.PreferencesTabControl.SelectedItem = window.CommitTabItem;
						Dispatcher.UIThread.RunJobs();
						ScreenshotHelper.Snap(window, "03-commit-tab", ModuleDir);
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ 4) Git：实例与身份 ============================

		[Fact]
		public void GitSettings_InstanceComboAndIdentity()
		{
			var snap = SnapshotPrefs();
			string originalName = TryGetGlobalGitConfig("user.name");
			string originalEmail = TryGetGlobalGitConfig("user.email");
			try
			{
				ForkPlusSettings.Default.VerboseGitOutput = false;
				ForkPlusSettings.Default.AiAttributionEnabled = true;
				ForkPlusSettings.Default.AiCheckpointReportingEnabled = true;

				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var git = window.GitUserControl;

					// —— git 实例下拉：环境自适应（生产两分支）——
					//   设了 forkgitinstance env var（CI 无内置 git 时 HeadlessAppBootstrap 兜底注入）→
					//   ENV 项选中且下拉禁用；沙箱装有内置 git（App.EnvironmentGitInstancePath == null，
					//   2026-09-06 诊断实证：/root/.local/share/ForkPlus/gitInstance/2.50.1/bin/git）→
					//   Fork 内置实例（Local）选中且可选。
					var envItem = git.GitInstanceComboBox.SelectedItem as GitUserControl.GitInstanceItem;
					Assert.NotNull(envItem);
					if (global::ForkPlus.App.EnvironmentGitInstancePath != null)
					{
						Assert.Equal(GitUserControl.GitInstanceType.Environment, envItem.GitInstanceType);
						Assert.Equal(global::ForkPlus.App.EnvironmentGitInstancePath, envItem.GitPath);
						Assert.False(git.GitInstanceComboBox.IsEnabled, "ENV 实例存在时下拉应禁用（生产行为）");
						Assert.Contains("ENV git instance", envItem.FileName);
					}
					else
					{
						Assert.Equal(GitUserControl.GitInstanceType.Local, envItem.GitInstanceType);
						Assert.Equal(global::ForkPlus.App.ForkGitInstancePath, envItem.GitPath);
						Assert.Contains("Fork git instance", envItem.FileName);
					}

						// —— git-mm / git-ai：环境自适应（2026-09-07 git-ai 可见性修复扩大探测面）——
						// 沙箱无对应可执行 → 仅分隔符 + 添加自定义项，无选中；装有（PATH / git 同
						// 目录 / 系统位置探测可见）→ 多出候选项且选中项匹配当前生效路径。原断言
						// 硬编码 2/Null，在装了 git-ai 的环境被打破（git-mm 同款"断言前提被环境
						// 打破"，2026-09-07 修复）。
						AssertExternalToolCombo(git.GitMmInstanceComboBox, global::ForkPlus.App.GitMmPath);
						AssertExternalToolCombo(git.GitAiInstanceComboBox, global::ForkPlus.App.GitAiResolvedPath);

						// —— 全局身份：初值来自 git config（快照时全局为空则空串）——
						Assert.Equal(originalName ?? "", git.UserNameTextBox.Text);
						Assert.Equal(originalEmail ?? "", git.EmailTextBox.Text);

						// 修改 + LostFocus（生产提交管线）→ SetGlobalUserIdentity 真实写全局配置。
					// 串行化：处理器是 async void（Task.Run 内跑 git config --global），两个
					// LostFocus 背靠背会并发写 .gitconfig 触发 "could not lock config file"
					// 错误弹窗（2026-09-06 实证，看门狗捕获根因文本）。等第一次落盘完成再发第二个。
					SetText(git.UserNameTextBox, "E2e Test User");
					SetText(git.EmailTextBox, "e2e-test@example.com");
					git.UserNameTextBox.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
					Assert.True(UiClick.WaitFor(delegate
					{
						return TryGetGlobalGitConfig("user.name") == "E2e Test User";
					}), "Name LostFocus 应触发 SetGlobalUserIdentity 写全局 git 配置（15s 超时）");
					git.EmailTextBox.RaiseEvent(new RoutedEventArgs(InputElement.LostFocusEvent));
					Assert.True(UiClick.WaitFor(delegate
					{
						return TryGetGlobalGitConfig("user.email") == "e2e-test@example.com";
					}), "Email LostFocus 应触发 SetGlobalUserIdentity 写全局 git 配置（15s 超时）");

						// —— 开关写回（AiAttribution/AiCheckpoint 处理器自带 Save）——
						UiClick.Toggle(git.VerboseGitOutputCheckBox, true);
						Assert.True(ForkPlusSettings.Default.VerboseGitOutput);
						UiClick.Toggle(git.AiAttributionCheckBox, false);
						Assert.False(ForkPlusSettings.Default.AiAttributionEnabled);
						UiClick.Toggle(git.AiCheckpointReportingCheckBox, false);
						Assert.False(ForkPlusSettings.Default.AiCheckpointReportingEnabled);

						window.PreferencesTabControl.SelectedItem = window.GitTabItem;
						Dispatcher.UIThread.RunJobs();
						ScreenshotHelper.Snap(window, "04-git-tab", ModuleDir);
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				RestoreGlobalGitConfig("user.name", originalName);
				RestoreGlobalGitConfig("user.email", originalEmail);
				RestorePrefs(snap);
			}
		}

		private static string TryGetGlobalGitConfig(string key)
		{
			try
			{
				string output = TestRepoFactory.GitOutput(null, "config --global --get " + key);
				string value = output.Trim();
				return value.Length == 0 ? null : value;
			}
			catch
			{
				return null; // 未设置（--get 非零退出）
			}
		}

		private static void RestoreGlobalGitConfig(string key, string value)
		{
			try
			{
				TestRepoFactory.GitOutput(null, "config --global --unset " + key);
			}
			catch
			{
			}
			if (!string.IsNullOrEmpty(value))
			{
				TestRepoFactory.GitOutput(null, "config --global " + key + " \"" + value + "\"");
			}
		}

		// ============================ 5) Integration：Shell 与保存链路 ============================

		[Fact]
		public void IntegrationSettings_ShellComboAndSave()
		{
			var snap = SnapshotPrefs();
			object originalShellTool = typeof(ForkPlusSettings).GetProperty("ShellTool").GetValue(ForkPlusSettings.Default);
			try
			{
				ForkPlusSettings.Default.ShowBugtrackerLinks = false;

				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var integration = window.IntegrationUserControl;

						// —— Shell 下拉装配（Default/WindowsTerminal/CommandPrompt/PowerShell/Custom）——
						var shellItems = integration.ShellToolComboBox.ItemsSource.Cast<string>().ToArray();
						Assert.Equal(5, shellItems.Length);
						Assert.Contains(global::ForkPlus.UI.ShellTool.DefaultType, shellItems);
						Assert.Contains(global::ForkPlus.UI.ShellTool.CustomType, shellItems);
						Assert.Equal(shellItems[0], integration.ShellToolComboBox.SelectedItem);

						// —— 切到 PowerShell：路径探测（沙箱无 powershell.exe → null）+ 参数框禁用 ——
						integration.ShellToolComboBox.SelectedItem = global::ForkPlus.UI.ShellTool.PowerShellType;
						Dispatcher.UIThread.RunJobs();
						Assert.Null(integration.ShellToolPathTextBox.Text); // TryFindInstance 在 Unix 沙箱找不到
						Assert.False(integration.ShellToolArgumentsTextBox.IsEnabled, "非 Custom shell 参数框应禁用");

						// —— 切到 Custom：参数框启用 ——
						integration.ShellToolComboBox.SelectedItem = global::ForkPlus.UI.ShellTool.CustomType;
						Dispatcher.UIThread.RunJobs();
						Assert.True(integration.ShellToolArgumentsTextBox.IsEnabled, "Custom shell 参数框应启用");
						SetText(integration.ShellToolPathTextBox, "/bin/myshell");
						SetText(integration.ShellToolArgumentsTextBox, "-l");

						// —— bugtracker 开关 ——
						UiClick.Toggle(integration.ShowBugtrackerLinksCheckBox, true);

						// —— Submit 生产链路：footer.SubmitButton.Click → OnSubmit →
						//    IntegrationUserControl.Save()（Shell 设置 + ShowBugtrackerLinks）——
						UiClick.Click(FooterOf(window).SubmitButton);
						Assert.False(window.IsVisible, "Submit 后偏好窗口应关闭");
						Assert.True(ForkPlusSettings.Default.ShowBugtrackerLinks, "Save 应写回 ShowBugtrackerLinks");
						var savedShell = (global::ForkPlus.UI.ShellTool)typeof(ForkPlusSettings)
							.GetProperty("ShellTool").GetValue(ForkPlusSettings.Default);
						Assert.Equal(global::ForkPlus.UI.ShellTool.CustomType, savedShell.Type);
						Assert.Equal("/bin/myshell", savedShell.ApplicationPath);
						Assert.Equal("-l", savedShell.Arguments);

						// 切 Integration tab 截图（重新构造一个窗口仅用于截图展示）
						var shotWindow = NewPrefsWindow();
						try
						{
							shotWindow.PreferencesTabControl.SelectedItem = shotWindow.IntegrationTabItem;
							Dispatcher.UIThread.RunJobs();
							ScreenshotHelper.Snap(shotWindow, "05-integration-tab", ModuleDir);
						}
						finally
						{
							shotWindow.Close();
						}
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				typeof(ForkPlusSettings).GetProperty("ShellTool").SetValue(ForkPlusSettings.Default, originalShellTool);
				RestorePrefs(snap);
			}
		}

		// ============================ 6) CustomCommands：添加/保存/持久化往返 ============================

		[Fact]
		public void CustomCommands_AddSaveAndPersistRoundtrip()
		{
			var snap = SnapshotPrefs();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var commands = window.CustomCommandsUserControl;

						// —— 空态：Fallback 显示（环境无既有全局命令；快照恢复保证）——
						Assert.True(commands.FallbackUserControl.IsVisible, "无命令时应显示空态说明");
						Assert.Empty(commands.CustomCommandsListBox.ItemsSource.Cast<object>());

						// —— 添加"提交"自定义命令（默认 ShCustomCommandAction git show ${sha}）——
						ClickMenuItem(commands.RevisionCustomCommandMenuItem);
						var items = commands.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
						Assert.Single(items);
						string expectedFirstName = Tr("Custom Command");
						Assert.Equal(expectedFirstName, items[0].Name);
						Assert.Equal(CustomCommandTarget.Revision, items[0].Target);
						Assert.Equal(ActionType.Action, items[0].ActionType);
						// 选中联动：Target 下拉 + Process 单选
						Assert.Equal(commands.CommitComboBoxItem, commands.TargetsComboBox.SelectedItem);
						Assert.True(commands.ProcessRadioButton.IsChecked == true);
						Assert.False(commands.FallbackUserControl.IsVisible, "有命令选中时空态应隐藏");
						Assert.False(commands.ReferenceTargetsContainer.IsVisible, "非 Branch 目标引用容器应隐藏");

						// —— 再次添加：自动命名计数（Custom Command1 / 2...）——
						ClickMenuItem(commands.RevisionCustomCommandMenuItem);
						ClickMenuItem(commands.RepositoryCustomCommandMenuItem);
						items = commands.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
						Assert.Equal(3, items.Count);
						Assert.Equal(expectedFirstName + "1", items[1].Name);
						Assert.Equal(expectedFirstName + "2", items[2].Name);
						Assert.Equal(CustomCommandTarget.Repository, items[2].Target);
						// 仓库命令默认 UrlCustomCommandAction → ActionType 未设动作（ActionType.Action 默认）
						Assert.Equal(CustomCommandTarget.Repository, items[2].CustomCommand.Target);

						// —— 选中第 1 条切目标为 Branch：引用目标容器显示 ——
						commands.CustomCommandsListBox.SelectedItem = items[0];
						Dispatcher.UIThread.RunJobs();
						commands.TargetsComboBox.SelectedItem = commands.BranchComboBoxItem;
						Dispatcher.UIThread.RunJobs();
						Assert.Equal(CustomCommandTarget.Reference, items[0].Target);
						Assert.True(commands.ReferenceTargetsContainer.IsVisible, "Branch 目标引用容器应显示");

						// —— Submit → Save() → custom-commands.json 落盘（按名排序）——
						UiClick.Click(FooterOf(window).SubmitButton);
						List<string> names = ReadCustomCommandNames();
						Assert.Equal(3, names.Count);
						Assert.Equal(new[] { expectedFirstName, expectedFirstName + "1", expectedFirstName + "2" }, names);

						// —— 持久化往返：新窗口（同一 CustomCommandManager 缓存已同步）重载 3 条 ——
						var window2 = NewPrefsWindow();
						try
						{
							var commands2 = window2.CustomCommandsUserControl;
							var reloaded = commands2.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
							Assert.Equal(3, reloaded.Count);
							Assert.Equal(expectedFirstName, reloaded[0].Name);
							Assert.Equal(CustomCommandTarget.Reference, reloaded[0].Target);
							Assert.Equal(CustomCommandTarget.Repository, reloaded[2].Target);
						}
						finally
						{
							window2.Close();
						}

						// 截图（第三个窗口展示命令列表 + 编辑面板）
						var shotWindow = NewPrefsWindow();
						try
						{
							shotWindow.PreferencesTabControl.SelectedItem = shotWindow.CustomCommandsTab;
							Dispatcher.UIThread.RunJobs();
							ScreenshotHelper.Snap(shotWindow, "06-custom-commands", ModuleDir);
						}
						finally
						{
							shotWindow.Close();
						}
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ 7) CustomCommands：删除确认 ============================

		[Fact]
		public void CustomCommands_RemoveWithConfirmation()
		{
			var snap = SnapshotPrefs();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var commands = window.CustomCommandsUserControl;
						ClickMenuItem(commands.RevisionCustomCommandMenuItem);
						ClickMenuItem(commands.RepositoryCustomCommandMenuItem);
						var items = commands.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
						Assert.Equal(2, items.Count);

						// —— 删除（模态确认泵：Post Background 内点"Remove"）——
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
								string removeTitle = Tr("Remove");
								Button remove = UiClick.FindAll<Button>(msgBox)
									.FirstOrDefault(b => UiClick.ContentText(b) == removeTitle);
								if (remove == null)
								{
									removeError[0] = "确认框中找不到按钮 " + removeTitle;
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
						UiClick.Click(commands.RemoveCustomCommandButton);

						Assert.True(removeHandled[0], "删除确认框处理器未执行：" + removeError[0]);
						Assert.Null(removeError[0]);
						Dispatcher.UIThread.RunJobs();
					var afterRemove = commands.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
					Assert.Single(afterRemove);
					Assert.Equal(Tr("Custom Command"), afterRemove[0].Name); // 删的是选中态（最后添加的第二条），剩第一条

						// —— 取消路径：点"Cancel" → 命令保留 ——
						commands.CustomCommandsListBox.SelectedItem = afterRemove[0];
						Dispatcher.UIThread.RunJobs();
						var cancelHandled = new bool[1];
						var cancelError = new string[1];
						Dispatcher.UIThread.Post(delegate
						{
							try
							{
								MessageBoxWindow msgBox = global::ForkPlus.UI.WpfCompat.WpfApp.Windows
									.OfType<MessageBoxWindow>().FirstOrDefault();
								if (msgBox == null)
								{
									cancelError[0] = "删除确认框未出现";
									return;
								}
								string cancelTitle = Tr("Cancel");
								Button cancel = UiClick.FindAll<Button>(msgBox)
									.FirstOrDefault(b => UiClick.ContentText(b) == cancelTitle);
								if (cancel == null)
								{
									cancelError[0] = "确认框中找不到按钮 " + cancelTitle;
									return;
								}
								cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
								cancelHandled[0] = true;
							}
							catch (Exception ex)
							{
								cancelError[0] = ex.ToString();
							}
						}, DispatcherPriority.Background);
						UiClick.Click(commands.RemoveCustomCommandButton);

						Assert.True(cancelHandled[0], "取消确认框处理器未执行：" + cancelError[0]);
						Assert.Null(cancelError[0]);
						Dispatcher.UIThread.RunJobs();
						var afterCancel = commands.CustomCommandsListBox.ItemsSource.Cast<CustomCommandViewModel>().ToList();
						Assert.Single(afterCancel); // 取消 → 未删除

						// —— 全局模式无 Local/Shared 单选（InitializeGlobal 折叠）——
						Assert.False(commands.LocationRadioButtonContainer.IsVisible);
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ 8) AI Review：归一化/校验/技能 ============================

		[Fact]
		public void AiReviewSettings_UrlNormalizationAndSkills()
		{
			var snap = SnapshotPrefs();
			try
			{
				// 确定性初态（AutoFetch=false 防构造期/输入期触发网络 RefreshModels）
				ForkPlusSettings.Default.AiReviewAutoFetchModels = false;
				ForkPlusSettings.Default.AiReviewServiceUrl = "";
				ForkPlusSettings.Default.AiReviewApiKey = "";
				ForkPlusSettings.Default.AiReviewRetryCount = 3;
				ForkPlusSettings.Default.AiReviewTimeoutSeconds = 300;
				ForkPlusSettings.Default.AiDevSkillList = "";
				ForkPlusSettings.Default.AiReviewSelectedModel = "";

				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var ai = window.AiReviewPreferencesUserControl;

						// —— ServiceUrl 归一化：/v1 与 /v1/chat/completions 与尾斜杠剥离 ——
						SetText(ai.ServiceUrlTextBox, "https://api.example.com/v1");
						Assert.Equal("https://api.example.com", ForkPlusSettings.Default.AiReviewServiceUrl);
						SetText(ai.ServiceUrlTextBox, "https://api.example.com/v1/chat/completions");
						Assert.Equal("https://api.example.com", ForkPlusSettings.Default.AiReviewServiceUrl);
						SetText(ai.ServiceUrlTextBox, "https://api.example.com/");
						Assert.Equal("https://api.example.com", ForkPlusSettings.Default.AiReviewServiceUrl);
						SetText(ai.ServiceUrlTextBox, "https://api.example.com/v2");
						Assert.Equal("https://api.example.com/v2", ForkPlusSettings.Default.AiReviewServiceUrl);

						// —— 重试/超时解析：有效值、非数字回退、下限 ——
						SetText(ai.RetryCountTextBox, "7");
						Assert.Equal(7, ForkPlusSettings.Default.AiReviewRetryCount);
						SetText(ai.RetryCountTextBox, "abc");
						Assert.Equal(3, ForkPlusSettings.Default.AiReviewRetryCount); // 回退 3
						SetText(ai.TimeoutTextBox, "15");
						Assert.Equal(15, ForkPlusSettings.Default.AiReviewTimeoutSeconds);
						SetText(ai.TimeoutTextBox, "abc");
						Assert.Equal(300, ForkPlusSettings.Default.AiReviewTimeoutSeconds); // 回退 300
						SetText(ai.TimeoutTextBox, "5");
						Assert.Equal(10, ForkPlusSettings.Default.AiReviewTimeoutSeconds); // 下限 10

						// —— 切到 AI tab：技能输入框（代码构建的私有 TextBox）进视觉树 ——
						window.PreferencesTabControl.SelectedItem = window.AiReviewTabItem;
						Dispatcher.UIThread.RunJobs();
						TextBox skillInput = ai.CustomSkillInputBorder.GetVisualDescendants()
						.OfType<TextBox>().FirstOrDefault();
					Assert.True(skillInput != null, "技能内容输入框应在 AI tab 视觉树内");
						TextBlock lineNumbers = ai.CustomSkillInputBorder.GetVisualDescendants()
							.OfType<TextBlock>().FirstOrDefault();
						Assert.NotNull(lineNumbers);

						// —— 行号联动（多行内容）——
						SetText(skillInput, "line one\nline two");
						Assert.Equal("1" + Environment.NewLine + "2" + Environment.NewLine, lineNumbers.Text);

						// —— 添加技能（显式名）——
						SetText(ai.SkillNameTextBox, "My Skill");
						UiClick.Click(ai.AddCustomSkillButton);
						Assert.Single(ai.SkillListBox.Items);
						Assert.Equal("", skillInput.Text); // 添加后清空
						Assert.Equal("", ai.SkillNameTextBox.Text);
						string saved = ForkPlusSettings.Default.AiDevSkillList;
						Assert.Contains("My Skill", saved);
						Assert.Contains("line one", saved);

						// —— 同名再添加 → 更新而非新增 ——
						SetText(skillInput, "updated content");
						SetText(ai.SkillNameTextBox, "My Skill");
						UiClick.Click(ai.AddCustomSkillButton);
						Assert.Single(ai.SkillListBox.Items);
						Assert.Contains("updated content", ForkPlusSettings.Default.AiDevSkillList);

						// —— 空名自动取内容首行 ——
						SetText(skillInput, "auto-name-first-line\nsecond");
						UiClick.Click(ai.AddCustomSkillButton);
						Assert.Equal(2, ai.SkillListBox.Items.Count);
						Assert.Contains("auto-name-first-line", ForkPlusSettings.Default.AiDevSkillList);

						// —— 空内容不添加 ——
						int countBefore = ai.SkillListBox.Items.Count;
						SetText(ai.SkillNameTextBox, "Empty Skill");
						UiClick.Click(ai.AddCustomSkillButton);
						Assert.Equal(countBefore, ai.SkillListBox.Items.Count);

						ScreenshotHelper.Snap(window, "07-ai-review-tab", ModuleDir);

						// —— 删除技能（列表项内 × 按钮，Tag=AiSkillEntry）——
						Button removeButton = UiClick.FindAll<Button>(ai.SkillListBox)
							.FirstOrDefault(b => b.Tag is AiSkillEntry);
						Assert.NotNull(removeButton);
						UiClick.Click(removeButton);
						Assert.Equal(1, ai.SkillListBox.Items.Count);

						// —— JSON 形态（AiDevSkillList 为技能数组序列化）——
						JArray skills = JArray.Parse(ForkPlusSettings.Default.AiDevSkillList);
						Assert.Single(skills);
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}

		// ============================ 9) Import/Export：取消路径 ============================

		[Fact]
		public void ImportExport_CancelPaths()
		{
			var snap = SnapshotPrefs();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = NewPrefsWindow();
					try
					{
						var importExport = window.ImportExportUserControl;
						window.PreferencesTabControl.SelectedItem = window.ImportExportTab;
						Dispatcher.UIThread.RunJobs();

						// 导出前置条件成立（ForkDirectory 下 settings.json 由测试套件常态落盘）
						string forkDir = global::ForkPlus.App.ForkDirectoryPath;
						Assert.True(File.Exists(Path.Combine(forkDir, "settings.json")), "settings.json 应存在");
						string[] zipsBefore = Directory.Exists(forkDir)
							? Directory.GetFiles(forkDir, "*.zip") : Array.Empty<string>();

						// —— 导出取消路径：headless 存储提供器 Noop（或 ActiveWindow null）→
						//    SaveFileDialog 返回 false/null → 早退：无 zip、状态条不变 ——
						Assert.Equal("", importExport.StatusText.Text);
						UiClick.Click(importExport.ExportButton);
						string[] zipsAfter = Directory.GetFiles(forkDir, "*.zip");
						Assert.Equal(zipsBefore.Length, zipsAfter.Length);
						Assert.Equal("", importExport.StatusText.Text); // 未进入 Exported to: 分支

						// —— 导入取消路径：OpenFileDialog 早退 → 无确认框、无重启、状态条不变 ——
						UiClick.Click(importExport.ImportButton);
						Assert.Equal("", importExport.StatusText.Text);
						Assert.False(global::ForkPlus.UI.WpfCompat.WpfApp.Windows
							.OfType<MessageBoxWindow>().Any(w => w.IsVisible), "取消选文件不应出现确认框");

						ScreenshotHelper.Snap(window, "08-import-export-tab", ModuleDir);
					}
					finally
					{
						window.Close();
					}
				});
			}
			finally
			{
				RestorePrefs(snap);
			}
		}
	}
}
