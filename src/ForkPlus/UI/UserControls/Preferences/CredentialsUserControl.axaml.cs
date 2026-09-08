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
	/// 三档语义（与凭据弹窗一致，见 SavedCredentialStore 头注释）：
	/// 第一档记账号为默认行为（不可关）；本页支持：
	/// - 提前录入：host + 账号 + 密码 + "不再弹出"开关，Add 一次写入（Upsert）；
	/// - 单条编辑：行内账号/密码框 + Save；
	/// - "不再弹出"开关（ToggleSwitch）：即时生效，关掉即恢复弹窗（重新弹出的开关）；
	/// - 单条 Remove：整条删除（下次询问从零开始）；
	/// - "Ask Again for All Hosts"：全局恢复弹窗。
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
			DescriptionTextBlock.Text = PreferencesLocalization.Current("Saved HTTP(S) credentials. User names are remembered automatically; 'Remember password' pre-fills the password in the dialog; 'Never ask' uses it silently. Add credentials in advance here — the switch re-enables prompting at any time.");
			AddHostTextBox.Placeholder = PreferencesLocalization.Current("Host (e.g. github.com)");
			AddUsernameTextBox.Placeholder = PreferencesLocalization.Current("User name");
			AddPasswordTextBox.Placeholder = PreferencesLocalization.Current("Password");
			AddButton.Content = PreferencesLocalization.Current("Add");
			AskAllAgainButton.Content = PreferencesLocalization.Current("Ask Again for All Hosts");
			AddNeverAskToggle.OnContent = PreferencesLocalization.Current("Never ask");
			AddNeverAskToggle.OffContent = PreferencesLocalization.Current("Ask");
			ToolTip.SetTip(AddNeverAskToggle, PreferencesLocalization.Current("When enabled, this credential is used silently without prompting; turn it off to be asked again."));
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
			TextBlock host = new TextBlock
			{
				Text = entry.Host,
				FontSize = 13,
				FontWeight = FontWeight.Medium,
				VerticalAlignment = VerticalAlignment.Center
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
			RowContext context = new RowContext(entry.Host, username, password, neverAsk);
			save.Tag = context;
			remove.Tag = context;
			return grid;
		}

		/// <summary>行上下文：Save/Remove 处理器从控件 Tag 回捞行内编辑值。</summary>
		private class RowContext
		{
			public readonly string Host;

			public readonly PlaceholderTextBox UsernameBox;

			public readonly PlaceholderTextBox PasswordBox;

			public readonly ToggleSwitch NeverAskToggle;

			public RowContext(string host, PlaceholderTextBox usernameBox, PlaceholderTextBox passwordBox, ToggleSwitch neverAskToggle)
			{
				Host = host;
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
				// 开关即时生效：开=第三档静默，关=恢复弹窗（第二档预填/第一档预填账号）
				SavedCredentialStore.Current.SetNeverAsk(host, toggle.IsChecked.GetValueOrDefault());
			}
		}

		private void SaveButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is RowContext row)
			{
				// 行内编辑保存：账号/密码 + 开关现状一次写入（空密码=清密码，保留账号）
				SavedCredentialStore.Current.Upsert(row.Host, row.UsernameBox.Text, row.PasswordBox.Text, row.NeverAskToggle.IsChecked.GetValueOrDefault());
				LoadCredentials();
			}
		}

		private void RemoveButton_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is RowContext row)
			{
				SavedCredentialStore.Current.Remove(row.Host);
				LoadCredentials();
			}
		}

		private void AskAllAgainButton_Click(object sender, RoutedEventArgs e)
		{
			SavedCredentialStore.Current.ClearAllNeverAsk();
			LoadCredentials();
		}
	}
}
