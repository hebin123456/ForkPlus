using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	/// <summary>
	/// 偏好设置 &gt; Credentials：凭据记忆（Layer D）管理页。
	///
	/// 记忆语义（与凭据弹窗一致，见 SavedCredentialStore 头注释；2026-09-14 起
	/// "记住密码"即静默复用，不再每次弹窗确认）：
	/// 第一档记账号为默认行为（不可关）；本页支持：
	/// - 提前录入：host + 账号 + 密码 + "不再弹出"开关，Add 一次写入（Upsert）；
	/// - 单条编辑：行内账号/密码框 + Save；
	/// - "不再弹出"开关（ToggleSwitch）：即时生效——开=静默（凭据缺失快速失败）；
	///   关=忘掉该 host 密码并恢复弹窗（修复（2026-09-14，"关掉开关仍静默"：
	///   静默语义并入"记住密码"后，仅清标记不再恢复弹窗，必须连密码一起忘掉））；
	/// - 单条 Remove：整条删除（下次询问从零开始）；
	/// - "Ask Again for All Hosts"：忘掉全部密码、恢复弹窗（账号记忆保留）。
	/// 凭据操作即时落盘（弹窗提交/认证 erase 联动都直接写 store），无需整页 Save。
	/// </summary>
	public partial class CredentialsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private ForkPlusDialogWindow _parentWindow;

		public CredentialsUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
		{
			_parentWindow = parentWindow;
			LoadCredentials();
		}

		public void ApplyLocalization()
		{
			// 动态行文本经 PreferencesLocalization.Current 构建，语言切换时重建
			// 修复（2026-09-14，文案与新静默语义对齐）：旧文案"pre-fills the password
			// in the dialog"描述的是已废除的"第二档弹窗预填"行为，按现行语义改写
			DescriptionTextBlock.Text = PreferencesLocalization.Current("Saved HTTP(S) credentials. User names are remembered automatically; 'Remember password' is reused silently without prompting. Add credentials in advance here — turn off a switch to forget its password and be asked again.");
			AddHostTextBox.Placeholder = PreferencesLocalization.Current("Host (e.g. github.com)");
			AddUsernameTextBox.Placeholder = PreferencesLocalization.Current("User name");
			AddPasswordTextBox.Placeholder = PreferencesLocalization.Current("Password");
			AddButton.Content = PreferencesLocalization.Current("Add");
			AskAllAgainButton.Content = PreferencesLocalization.Current("Ask Again for All Hosts");
			AddNeverAskToggle.OnContent = PreferencesLocalization.Current("Never ask");
			AddNeverAskToggle.OffContent = PreferencesLocalization.Current("Ask");
			ToolTip.SetTip(AddNeverAskToggle, PreferencesLocalization.Current("When enabled, this credential is used silently without prompting; turn it off to forget the password and be asked again."));
			LoadCredentials();
		}

		private void LoadCredentials()
		{
			CredentialsListPanel.Children.Clear();
			List<SavedCredentialStore.SavedCredential> all = SavedCredentialStore.Current.GetAll();
			if (all.Count == 0)
			{
				CredentialsListPanel.Children.Add(new TextBlock
				{
					Text = PreferencesLocalization.Current("No saved credentials yet."),
					FontSize = 13,
					Opacity = 0.7,
					Margin = new Thickness(0, 4, 0, 4)
				});
				return;
			}
			CredentialsListPanel.Children.Add(BuildHeaderRow());
			foreach (SavedCredentialStore.SavedCredential entry in all)
			{
				CredentialsListPanel.Children.Add(BuildCredentialRow(entry));
			}
		}

		private Control BuildHeaderRow()
		{
			Grid grid = NewRowGrid();
			AddHeaderCell(grid, 0, "Host");
			AddHeaderCell(grid, 1, "User name");
			AddHeaderCell(grid, 2, "Password");
			AddHeaderCell(grid, 3, "Never ask");
			return grid;
		}

		private void AddHeaderCell(Grid grid, int column, string text)
		{
			var header = new TextBlock
			{
				Text = PreferencesLocalization.Current(text),
				FontSize = 11,
				Opacity = 0.6,
				Margin = new Thickness(0, 2, 8, 2)
			};
			Grid.SetColumn(header, column);
			grid.Children.Add(header);
		}

		private Grid NewRowGrid()
		{
			Grid grid = new Grid
			{
				Margin = new Thickness(0, 3, 0, 3)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.2, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1, GridUnitType.Star)
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			grid.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			return grid;
		}

		private Control BuildCredentialRow(SavedCredentialStore.SavedCredential entry)
		{
			Grid grid = NewRowGrid();
			// 修复（2026-09-11，"主机没有编辑框，应该改成编辑框"）：宿主列由只读 TextBlock
			// 改为可编辑输入框，允许行内直接修改 host（改完 Save 即按新 host 落盘）。
			PlaceholderTextBox host = new PlaceholderTextBox
			{
				Text = entry.Host,
				Placeholder = PreferencesLocalization.Current("Host (e.g. github.com)"),
				FontSize = 13,
				Margin = new Thickness(0, 0, 6, 0),
				VerticalContentAlignment = VerticalAlignment.Center
			};
			Grid.SetColumn(host, 0);
			PlaceholderTextBox username = new PlaceholderTextBox
			{
				Text = entry.Username ?? "",
				Placeholder = PreferencesLocalization.Current("User name"),
				FontSize = 13,
				Margin = new Thickness(0, 0, 8, 0)
			};
			Grid.SetColumn(username, 1);
			PlaceholderTextBox password = new PlaceholderTextBox
			{
				Text = entry.Password ?? "",
				Placeholder = PreferencesLocalization.Current("Password"),
				PasswordChar = '●',
				FontSize = 13,
				Margin = new Thickness(0, 0, 8, 0)
			};
			Grid.SetColumn(password, 2);
			// "不再弹出"开关（即时生效；先赋 IsChecked 再订阅事件，避免装配期触发 handler）
			ToggleSwitch neverAsk = new ToggleSwitch
			{
				IsChecked = entry.NeverAskAgain,
				Tag = entry.Host,
				OnContent = PreferencesLocalization.Current("Never ask"),
				OffContent = PreferencesLocalization.Current("Ask"),
				Margin = new Thickness(0, 0, 8, 0)
			};
			neverAsk.IsCheckedChanged += NeverAskToggle_Changed;
			Grid.SetColumn(neverAsk, 3);
			Button save = new Button
			{
				Content = PreferencesLocalization.Current("Save"),
				Margin = new Thickness(0, 0, 8, 0)
			};
			save.Click += SaveButton_Click;
			Grid.SetColumn(save, 4);
			Button remove = new Button
			{
				Content = PreferencesLocalization.Current("Remove")
			};
			remove.Click += RemoveButton_Click;
			Grid.SetColumn(remove, 5);
			grid.Children.Add(host);
			grid.Children.Add(username);
			grid.Children.Add(password);
			grid.Children.Add(neverAsk);
			grid.Children.Add(save);
			grid.Children.Add(remove);
			// Save/Remove 读取行内编辑值：Tag 携带行上下文（host + 编辑框 + 开关）
			RowContext context = new RowContext(entry.Host, host, username, password, neverAsk);
			save.Tag = context;
			remove.Tag = context;
			return grid;
		}

		/// <summary>行上下文：Save/Remove 处理器从控件 Tag 回捞行内编辑值。</summary>
		private class RowContext
		{
			public readonly string OriginalHost;

			public readonly PlaceholderTextBox HostBox;

			public readonly PlaceholderTextBox UsernameBox;

			public readonly PlaceholderTextBox PasswordBox;

			public readonly ToggleSwitch NeverAskToggle;

			public RowContext(string originalHost, PlaceholderTextBox hostBox, PlaceholderTextBox usernameBox, PlaceholderTextBox passwordBox, ToggleSwitch neverAskToggle)
			{
				OriginalHost = originalHost;
				HostBox = hostBox;
				UsernameBox = usernameBox;
				PasswordBox = passwordBox;
				NeverAskToggle = neverAskToggle;
			}
		}

		// ============================ 交互 ============================

		private void AddButton_Click(object sender, RoutedEventArgs e)
		{
			string host = AddHostTextBox.Text?.Trim();
			if (string.IsNullOrEmpty(host))
			{
				return;
			}
			// 提前录入：账号 + 密码 + "不再弹出"一次落盘（Upsert；空密码=只记账号）
			SavedCredentialStore.Current.Upsert(host, AddUsernameTextBox.Text, AddPasswordTextBox.Text, AddNeverAskToggle.IsChecked.GetValueOrDefault());
			AddHostTextBox.Text = "";
			AddUsernameTextBox.Text = "";
			AddPasswordTextBox.Text = "";
			AddNeverAskToggle.IsChecked = false;
			LoadCredentials();
		}

		private void NeverAskToggle_Changed(object sender, RoutedEventArgs e)
		{
			if (sender is ToggleSwitch toggle && toggle.Tag is string host)
			{
				if (toggle.IsChecked.GetValueOrDefault())
				{
					// 开=静默（凭据缺失时快速失败）
					SavedCredentialStore.Current.SetNeverAsk(host, true);
				}
				else
				{
					// 修复（2026-09-14，"关掉开关仍静默"）：静默语义并入"记住密码"后，
					// 仅清 NeverAskAgain 不再恢复弹窗——必须连密码一起忘掉才真正重新询问
					// （账号记忆保留，弹窗仍会预填账号）
					SavedCredentialStore.Current.ForgetPassword(host);
					SavedCredentialStore.Current.SetNeverAsk(host, false);
				}
			}
		}

		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is RowContext row)
			{
				// 行内编辑保存：host 可改（改 host 等于重建条目：删旧 host 残留 + 按新 host 落盘）
				string newHost = row.HostBox.Text?.Trim();
				if (!string.IsNullOrEmpty(newHost)
					&& !string.Equals(newHost, row.OriginalHost, StringComparison.OrdinalIgnoreCase))
				{
					SavedCredentialStore.Current.Remove(row.OriginalHost);
				}
				// 账号/密码 + 开关现状一次写入（空密码=清密码，保留账号）
				SavedCredentialStore.Current.Upsert(newHost, row.UsernameBox.Text, row.PasswordBox.Text, row.NeverAskToggle.IsChecked.GetValueOrDefault());
				LoadCredentials();
			}
		}

		private void RemoveButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is RowContext row)
			{
				string host = row.HostBox.Text?.Trim();
				if (string.IsNullOrEmpty(host))
				{
					host = row.OriginalHost;
				}
				SavedCredentialStore.Current.Remove(host);
				LoadCredentials();
			}
		}

		private void AskAllAgainButton_Click(object sender, RoutedEventArgs e)
		{
			// 修复（2026-09-14，"全局重开仍静默"）：忘掉全部密码（+清标记），保留账号
			// 记忆——仅清标记（ClearAllNeverAsk）在静默语义并入"记住密码"后不再恢复弹窗
			SavedCredentialStore.Current.ForgetAllPasswords();
			LoadCredentials();
		}
	}
}
