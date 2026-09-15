// 凭据记忆（Layer D）UI 专项测试：
// - 第一档（默认行为，无勾选框）：Username 询问预填已记住账号，提交后自动记住新账号；
// - "记住密码"（勾选）：password 非空即静默复用（2026-09-14 起旧第二/三档并入同一
//   静默语义——修复"勾了记住密码还不停询问"；弹窗内仍预填+预勾选，供改密场景）；
// - git-mm 单词提示（"username"/"password"）：明文框/密码框正确分流 + 密码框 ASCII 过滤
//   （修复"username 弹窗显示成密码框""密码框可输入中文"）。
// - CredentialsUserControl（偏好设置 > Credentials）：提前录入（Add → Upsert）、
//   行内编辑（Save）、"不再弹出"ToggleSwitch 即时生效（关=忘掉密码恢复弹窗）、
//   Remove、Ask Again for All Hosts（忘掉全部密码，账号保留）。
// SwapForTests 注入隔离 store，不污染用户数据目录。
// 模式与 E2e25 一致：生产构造器直构 + Show() 非模态 + Footer 按钮经视觉树定位。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.UI.UserControls.Preferences;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CredentialsRememberUiTests
	{
		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
		}

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static Button FindButton(Visual root, string content)
		{
			return UiClick.FindAll<Button>(root)
				.FirstOrDefault((Button b) => UiClick.ContentText(b) == content && b.IsVisible);
		}

		private SavedCredentialStore CreateIsolatedStore(out SavedCredentialStore previous)
		{
			// 会话级单词提示缓存是进程级静态：每用例前清空，避免串扰
			SavedCredentialStore.ClearSessionBareCredentialsForTests();
			string path = Path.Combine(Path.GetTempPath(), "fp-creds-ui-" + Guid.NewGuid().ToString("N") + ".json");
			var store = new SavedCredentialStore(path);
			previous = SavedCredentialStore.SwapForTests(store);
			return store;
		}

		// ============================ 第一档：Username 询问（自动记账号，无勾选框） ============================

		[Fact]
		public void AskPass_UsernamePrompt_PrefillsRememberedUsername_NoCheckboxes()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				store.RememberUsername("github.com", "octocat");
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Username for 'https://github.com':", "/tmp/myrepo");
					window.Show();
					RunJobs();

					// 装配：明文框预填已记住账号；第一档无任何勾选框（SSH 的 Remember 也不显示）
					Assert.True(window.InputTextBox.IsVisible);
					Assert.Equal("octocat", window.InputTextBox.Text);
					Assert.False(window.RememberCheckBox.IsVisible, "HTTP(S) Username 询问不应显示 Remember");
					Assert.False(window.RememberPasswordCheckBox.IsVisible);
					Assert.False(window.NeverAskCheckBox.IsVisible);

					// 改账号 → 提交 → 自动记住（默认行为，无勾选框参与）
					window.InputTextBox.Text = "newcat";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("newcat", window.Result);
					Assert.Equal("newcat", store.FindEntry("github.com").Username);
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_UsernamePrompt_RemembersUsernameByDefault()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Username for 'https://github.com':", "");
					window.Show();
					RunJobs();

					// 无存量记录：空框起步，输入提交后自动记住（第一档默认行为）
					Assert.Equal("", window.InputTextBox.Text);
					window.InputTextBox.Text = "octocat";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("octocat", window.Result);
					Assert.Equal("octocat", store.FindEntry("github.com").Username);
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		// ============================ 第二/三档：Password 询问 ============================

		[Fact]
		public void AskPass_HttpsPasswordPrompt_PrefillsRememberedPassword()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				store.RememberPassword("github.com", "octocat", "secret123");
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Password for 'https://octocat@github.com':", "");
					window.Show();
					RunJobs();

					// 装配：密码框预填已记住密码（改密场景；正常流程已由静默路径回填，
					// 不再弹窗）+ "记住密码"预勾选；SSH 的 Remember 不显示；
					// 第三档勾选框已隐藏（静默语义并入"记住密码"，2026-09-14）
					Assert.True(window.InputPasswordBox.IsVisible);
					Assert.Equal("secret123", window.InputPasswordBox.Text);
					Assert.True(window.RememberPasswordCheckBox.IsVisible);
					Assert.False(window.NeverAskCheckBox.IsVisible, "第三档勾选框已隐藏（静默语义并入记住密码）");
					Assert.True(window.RememberPasswordCheckBox.IsChecked.GetValueOrDefault(), "存量密码应预勾选记住密码");
					Assert.False(window.NeverAskCheckBox.IsChecked.GetValueOrDefault());
					Assert.False(window.RememberCheckBox.IsVisible);

					window.Close();
					RunJobs();
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_HttpsPasswordPrompt_RememberOnly_IsSilent()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Password for 'https://octocat@github.com':", "");
					window.Show();
					RunJobs();

					// 只勾"记住密码" → 提交
					window.RememberPasswordCheckBox.IsChecked = true;
					window.InputPasswordBox.Text = "secret123";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("secret123", window.Result);
				});
				// 修复（2026-09-14，"勾了记住密码还不停询问"）：密码落盘即静默回填
				// （旧"第二档"要求双条件，用户勾了记住密码仍每次弹窗）
				SavedCredentialStore.SavedCredential entry = store.FindEntry("github.com");
				Assert.NotNull(entry);
				Assert.Equal("octocat", entry.Username);
				Assert.Equal("secret123", entry.Password);
				Assert.False(entry.NeverAskAgain, "只勾记住密码不开不再弹出标记");
				Assert.True(store.TryGetSilentCredential("github.com", out _, out _), "记住密码应静默回填");
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_HttpsPasswordPrompt_RememberAndNeverAsk_StoresBoth()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Password for 'https://octocat@github.com':", "");
					window.Show();
					RunJobs();

					// 勾"记住密码 + 不再弹出"（控件已隐藏，仍可编程勾选——兼容偏好页
					// Upsert 落盘的 NeverAskAgain 形态） → 提交
					window.RememberPasswordCheckBox.IsChecked = true;
					window.NeverAskCheckBox.IsChecked = true;
					window.InputPasswordBox.Text = "secret123";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("secret123", window.Result);
				});
				// password + NeverAskAgain 皆备：静默回填 + 密码被 erase 后快速失败
				SavedCredentialStore.SavedCredential entry = store.FindEntry("github.com");
				Assert.NotNull(entry);
				Assert.Equal("secret123", entry.Password);
				Assert.True(entry.NeverAskAgain, "勾不再弹出应落标记");
				Assert.True(store.TryGetSilentCredential("github.com", out string username, out string password));
				Assert.Equal("octocat", username);
				Assert.Equal("secret123", password);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_HttpsPasswordPrompt_Unchecked_ClearsRememberedPassword()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				store.RememberPassword("github.com", "octocat", "secret123");
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("Password for 'https://octocat@github.com':", "");
					window.Show();
					RunJobs();
					Assert.True(window.RememberPasswordCheckBox.IsChecked.GetValueOrDefault(), "存量密码应预勾选");

					// 取消"记住密码"提交 → 停止记住密码（账号记忆保留）
					window.RememberPasswordCheckBox.IsChecked = false;
					window.InputPasswordBox.Text = "fresh-password";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("fresh-password", window.Result);
				});
				SavedCredentialStore.SavedCredential entry = store.FindEntry("github.com");
				Assert.NotNull(entry);
				Assert.False(entry.HasPassword, "取消勾选应清除已记住的密码");
				Assert.Equal("octocat", entry.Username);
				Assert.False(entry.NeverAskAgain);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		// ============================ git-mm 单词提示（2026-09-14 弹窗适配） ============================

		[Fact]
		public void AskPass_BareUsernamePrompt_ShowsUsernameBoxNotPasswordBox()
		{
			// 修复（2026-09-14，"Username 弹窗显示成密码框"）：git-mm 经 GIT_ASKPASS 询问时
			// 提示词是单词 "username"（无 URL），旧逻辑全部分支不匹配 → 落 else 兜底
			// （"密码："标签 + 密码框），用户把密码输进用户名框 → 认证失败循环弹窗
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("username", "");
					window.Show();
					RunJobs();

					// 单词 username：明文用户名框（不是密码框），无凭据记忆勾选框
					// （提示无 URL，无法关联 host 记忆）
					Assert.True(window.InputTextBox.IsVisible, "单词 username 询问应显示明文用户名框");
					Assert.False(window.InputPasswordBox.IsVisible, "单词 username 询问不应显示密码框");
					Assert.False(window.RememberCheckBox.IsVisible);
					Assert.False(window.RememberPasswordCheckBox.IsVisible);
					Assert.False(window.NeverAskCheckBox.IsVisible);

					window.InputTextBox.Text = "h00003968";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					// 提交取明文框内容（修复前误取密码框 → 回传空/错值）
					Assert.Equal("h00003968", window.Result);
				});
				// 提示无 URL：不关联 host，不写入凭据记忆；但写会话缓存
				// （同会话后续单词 username 静默复用，见 ShowAskPassWindowCommand）
				Assert.Empty(store.GetAll());
				Assert.True(SavedCredentialStore.TryGetSessionBareUsername(out string sessionUsername));
				Assert.Equal("h00003968", sessionUsername);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_BarePasswordPrompt_ShowsPasswordBox_And_FiltersNonAscii()
		{
			// 修复（2026-09-14，"密码框还能输入中文"）：git HTTPS 密码/token 均为可打印
			// ASCII，IME 汉字/全角输入一律过滤（TextChanged 兜底，覆盖键入与粘贴）
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new global::ForkPlus.UI.Dialogs.AskPassWindow("password", "");
					window.Show();
					RunJobs();

					Assert.True(window.InputPasswordBox.IsVisible, "单词 password 询问应显示密码框");
					Assert.False(window.InputTextBox.IsVisible);

					// 汉字混输：被剥离，仅保留可打印 ASCII
					window.InputPasswordBox.Text = "密码abc123";
					RunJobs();
					Assert.Equal("abc123", window.InputPasswordBox.Text);

					// 纯 ASCII：原样保留
					window.InputPasswordBox.Text = "p@ssw0rd!";
					RunJobs();
					Assert.Equal("p@ssw0rd!", window.InputPasswordBox.Text);

					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();
					Assert.Equal("p@ssw0rd!", window.Result);
				});
				Assert.Empty(store.GetAll());
				// 单词 password 提交 → 会话缓存（同会话后续静默复用）
				Assert.True(SavedCredentialStore.TryGetSessionBarePassword(out string sessionPassword));
				Assert.Equal("p@ssw0rd!", sessionPassword);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_GitMmUrlPrompts_CorrectBoxes_PasswordHasRememberOption()
		{
			// 图形化端到端回放（2026-09-15 生产日志 08:47，用户实测链路）：git-mm 无引号
			// 小写提示——username 必须是明文用户名框（修复前落兜底密码框）；password 必须
			// 有"记住密码"勾选（修复前无勾选、不落盘 → 反复弹）；host 记忆预填生效
			//（修复前解析不出 host）
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				string host = "codehub-git-codeartsx.rnd.yinwang.com";
				string manifestPath = "innersource/T4VB_G/TNC/build/manifest.git";
				store.RememberUsername(host, "h00003968");
				HeadlessAppBootstrap.Run(delegate
				{
					// git-mm "username for https://..."（无引号）
					var userWin = new global::ForkPlus.UI.Dialogs.AskPassWindow("username for https://" + host + "/" + manifestPath, "");
					userWin.Show();
					RunJobs();
					Assert.True(userWin.InputTextBox.IsVisible, "git-mm username 提示应显示明文用户名框");
					Assert.False(userWin.InputPasswordBox.IsVisible, "git-mm username 提示不应显示密码框");
					Assert.Equal("h00003968", userWin.InputTextBox.Text);
					userWin.InputTextBox.Text = "h00003968";
					RunJobs();
					UiClick.Click(FindButton(userWin, Tr("OK")));
					RunJobs();
					Assert.Equal("h00003968", userWin.Result);

					// git-mm "password for https://@..."（空 userinfo）
					var passWin = new global::ForkPlus.UI.Dialogs.AskPassWindow("password for https://@" + host + "/" + manifestPath, "");
					passWin.Show();
					RunJobs();
					Assert.True(passWin.InputPasswordBox.IsVisible, "git-mm password 提示应显示密码框");
					Assert.False(passWin.InputTextBox.IsVisible);
					Assert.True(passWin.RememberPasswordCheckBox.IsVisible, "git-mm password 提示应有记住密码勾选（修复前落兜底分支无勾选）");
					passWin.RememberPasswordCheckBox.IsChecked = true;
					passWin.InputPasswordBox.Text = "!a654190921a";
					RunJobs();
					UiClick.Click(FindButton(passWin, Tr("OK")));
					RunJobs();
					Assert.Equal("!a654190921a", passWin.Result);
				});
				// 密码已落盘 → 同 host 后续全链路静默（反复弹窗根因修复）
				SavedCredentialStore.SavedCredential entry = store.FindEntry(host);
				Assert.NotNull(entry);
				Assert.Equal("h00003968", entry.Username);
				Assert.Equal("!a654190921a", entry.Password);
				Assert.True(store.TryGetSilentCredential(host, out _, out _));
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		// UserControl 无 Show()：套宿主 Window 挂视觉树（模块 25 窗口直构模式的变体）
		private Window HostInWindow(UserControl control)
		{
			var host = new Window
			{
				Content = control,
				Width = 640,
				Height = 480
			};
			host.Show();
			return host;
		}

		// ============================ 偏好设置：Credentials 页 ============================

		[Fact]
		public void PreferencesCredentialsPage_AddUpsertsEntryWithNeverAsk()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new CredentialsUserControl();
					control.Initialize(null);
					Window host = HostInWindow(control);
					RunJobs();

					// 提前录入：host + 账号 + 密码 + "不再弹出"开关，Add 一次写入
					control.AddHostTextBox.Text = "github.com";
					control.AddUsernameTextBox.Text = "octocat";
					control.AddPasswordTextBox.Text = "secret123";
					control.AddNeverAskToggle.IsChecked = true;
					RunJobs();
					UiClick.Click(control.AddButton);
					RunJobs();

					host.Close();
					RunJobs();
				});
				// Upsert 落盘：第三档形态（静默命中）
				SavedCredentialStore.SavedCredential entry = store.FindEntry("github.com");
				Assert.NotNull(entry);
				Assert.Equal("octocat", entry.Username);
				Assert.Equal("secret123", entry.Password);
				Assert.True(entry.NeverAskAgain);
				Assert.True(store.TryGetSilentCredential("github.com", out string username, out string password));
				Assert.Equal("octocat", username);
				Assert.Equal("secret123", password);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void PreferencesCredentialsPage_RowEditSaveToggleRemove()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				store.RememberPassword("github.com", "octocat", "secret123");
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new CredentialsUserControl();
					control.Initialize(null);
					Window host = HostInWindow(control);
					RunJobs();

					// ===== 行内编辑：改账号 → Save（Upsert，密码与开关现状保留） =====
					TextBox usernameBox = UiClick.FindAll<TextBox>(control)
						.FirstOrDefault((TextBox t) => t.Text == "octocat");
					Assert.NotNull(usernameBox);
					usernameBox.Text = "newcat";
					RunJobs();
					Button save = UiClick.FindAll<Button>(control)
						.FirstOrDefault((Button b) => UiClick.ContentText(b) == Tr("Save"));
					Assert.NotNull(save);
					UiClick.Click(save);
					RunJobs();
					Assert.Equal("newcat", store.FindEntry("github.com").Username);
					Assert.True(store.FindEntry("github.com").HasPassword, "行内 Save 不应丢已记密码");

					// ===== "不再弹出"开关（ToggleSwitch）：即时生效 =====
					ToggleSwitch neverAsk = UiClick.FindAll<ToggleSwitch>(control)
						.FirstOrDefault((ToggleSwitch t) => (t.Tag as string) == "github.com");
					Assert.NotNull(neverAsk);
					neverAsk.IsChecked = true;
					RunJobs();
					Assert.True(store.FindEntry("github.com").NeverAskAgain, "开关打开应即时落盘");
					Assert.True(store.TryGetSilentCredential("github.com", out _, out _), "开开关后应命中第三档静默");

					neverAsk.IsChecked = false;
					RunJobs();
					Assert.False(store.FindEntry("github.com").NeverAskAgain, "开关关闭应即时落盘（恢复弹窗）");
					// 修复（2026-09-14，"关掉开关仍静默"）：关=忘掉密码并恢复弹窗
					// （静默语义并入"记住密码"后，仅清标记不再恢复弹窗）
					Assert.False(store.FindEntry("github.com").HasPassword, "开关关闭应忘掉已记密码");
					Assert.Equal("newcat", store.FindEntry("github.com").Username);

					// ===== Remove：整条删除 =====
					Button remove = UiClick.FindAll<Button>(control)
						.FirstOrDefault((Button b) => UiClick.ContentText(b) == Tr("Remove"));
					Assert.NotNull(remove);
					UiClick.Click(remove);
					RunJobs();
					Assert.Null(store.FindEntry("github.com"));

					host.Close();
					RunJobs();
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void PreferencesCredentialsPage_AskAgainForAllHosts()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				store.RememberPassword("a.example.com", "alice", "pw-a");
				store.SetNeverAsk("a.example.com", enabled: true);
				store.RememberUsername("b.example.com", "bob");
				store.SetNeverAsk("b.example.com", enabled: true);

				HeadlessAppBootstrap.Run(delegate
				{
					var control = new CredentialsUserControl();
					control.Initialize(null);
					// 与生产路径一致（PreferencesWindow.Initialize → ApplyLocalization）：
					// 语言词条补齐后，XAML 静态英文初始值（如 Ask Again 按钮）必须经
					// ApplyLocalization 本地化，Tr() 断言才能命中。
					control.ApplyLocalization();
					Window host = HostInWindow(control);
					RunJobs();

					// 列表装配：2 行（host 排序），行内 host 为可编辑输入框（2026-09-11 改为编辑框）
					var boxTexts = UiClick.FindAll<global::Avalonia.Controls.TextBox>(control)
						.Select((global::Avalonia.Controls.TextBox t) => t.Text)
						.Where((string t) => !string.IsNullOrEmpty(t))
						.ToList();
					Assert.Contains("a.example.com", boxTexts);
					Assert.Contains("b.example.com", boxTexts);

					// ===== Ask Again for All Hosts：批量清"不再弹出"（账号/密码保留） =====
					Button askAll = UiClick.FindAll<Button>(control)
						.FirstOrDefault((Button b) => UiClick.ContentText(b) == Tr("Ask Again for All Hosts"));
					Assert.NotNull(askAll);
					UiClick.Click(askAll);
					RunJobs();

					host.Close();
					RunJobs();
				});
				Assert.False(store.FindEntry("a.example.com").NeverAskAgain);
				// 修复（2026-09-14，"全局重开仍静默"）：忘掉全部密码（账号保留）——
				// 仅清标记在静默语义并入"记住密码"后不再恢复弹窗
				Assert.False(store.FindEntry("a.example.com").HasPassword, "全局重开应忘掉已记密码");
				Assert.Equal("alice", store.FindEntry("a.example.com").Username);
				Assert.False(store.FindEntry("b.example.com").NeverAskAgain);
				Assert.Equal("bob", store.FindEntry("b.example.com").Username);
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void PreferencesCredentialsPage_EmptyState()
		{
			SavedCredentialStore previous = null;
			SavedCredentialStore store = CreateIsolatedStore(out previous);
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var control = new CredentialsUserControl();
					control.Initialize(null);
					Window host = HostInWindow(control);
					RunJobs();

					var texts = UiClick.FindAll<global::Avalonia.Controls.TextBlock>(control)
						.Select((global::Avalonia.Controls.TextBlock t) => t.Text)
						.Where((string t) => !string.IsNullOrEmpty(t))
						.ToList();
					Assert.Contains(Tr("No saved credentials yet."), texts);

					host.Close();
					RunJobs();
				});
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void PreferencesWindow_HasCredentialsTab()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new global::ForkPlus.UI.Dialogs.PreferencesWindow();
				window.Show();
				RunJobs();

				// Tab 装配：Credentials 页存在且 Header 已本地化装配
				Assert.NotNull(window.CredentialsTabItem);
				Assert.Equal(Tr("Credentials"), window.CredentialsTabItem.Header?.ToString());

				window.Close();
				RunJobs();
			});
		}
	}
}
