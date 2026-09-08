// 凭据记忆（Layer D）UI 专项测试（三档语义）：
// - 第一档（默认行为，无勾选框）：Username 询问预填已记住账号，提交后自动记住新账号；
// - 第二档"记住密码"（勾选）：Password 询问预填已记住密码且预勾选，提交后 store 有密码
//   但不开"不再弹出"——下次弹窗仍出现、密码框自动填充；
// - 第三档"记住密码 + 不再弹出"（勾选）：提交后 password + NeverAskAgain 落盘，
//   全链路静默回填（Command 静默路径在 SavedCredentialStoreTests 覆盖）。
// - CredentialsUserControl（偏好设置 > Credentials）：提前录入（Add → Upsert）、
//   行内编辑（Save）、"不再弹出"ToggleSwitch 即时生效、Remove、Ask Again for All Hosts。
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

					// 装配：密码框预填已记住密码（第二档"下次弹出来自动填充密码"）+
					// "记住密码"预勾选；SSH 的 Remember 不显示
					Assert.True(window.InputPasswordBox.IsVisible);
					Assert.Equal("secret123", window.InputPasswordBox.Text);
					Assert.True(window.RememberPasswordCheckBox.IsVisible);
					Assert.True(window.NeverAskCheckBox.IsVisible);
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
		public void AskPass_HttpsPasswordPrompt_RememberOnly_IsSecondTier()
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

					// 只勾"记住密码"（不开"不再弹出"）→ 提交
					window.RememberPasswordCheckBox.IsChecked = true;
					window.InputPasswordBox.Text = "secret123";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("secret123", window.Result);
				});
				// 第二档：密码落盘，但不开"不再弹出"——下次弹窗仍出现（密码框预填）
				SavedCredentialStore.SavedCredential entry = store.FindEntry("github.com");
				Assert.NotNull(entry);
				Assert.Equal("octocat", entry.Username);
				Assert.Equal("secret123", entry.Password);
				Assert.False(entry.NeverAskAgain, "只勾记住密码=第二档，不应开不再弹出");
				Assert.False(store.TryGetSilentCredential("github.com", out _, out _), "第二档不应静默回填");
			}
			finally
			{
				SavedCredentialStore.SwapForTests(previous);
			}
		}

		[Fact]
		public void AskPass_HttpsPasswordPrompt_RememberAndNeverAsk_IsThirdTier()
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

					// 勾"记住密码 + 不再弹出" → 提交
					window.RememberPasswordCheckBox.IsChecked = true;
					window.NeverAskCheckBox.IsChecked = true;
					window.InputPasswordBox.Text = "secret123";
					RunJobs();
					UiClick.Click(FindButton(window, Tr("OK")));
					RunJobs();

					Assert.Equal("secret123", window.Result);
				});
				// 第三档：password + NeverAskAgain 皆备——credential get / askpass 全链路静默回填
				SavedCredentialStore.SavedCredential entry = store.FindEntry("github.com");
				Assert.NotNull(entry);
				Assert.Equal("secret123", entry.Password);
				Assert.True(entry.NeverAskAgain, "勾不再弹出=第三档");
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

					// 列表装配：2 行（host 排序），行内值可编辑
					var texts = UiClick.FindAll<global::Avalonia.Controls.TextBlock>(control)
						.Select((global::Avalonia.Controls.TextBlock t) => t.Text)
						.Where((string t) => !string.IsNullOrEmpty(t))
						.ToList();
					Assert.Contains("a.example.com", texts);
					Assert.Contains("b.example.com", texts);

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
				Assert.True(store.FindEntry("a.example.com").HasPassword, "全局重开不应丢已记密码");
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
