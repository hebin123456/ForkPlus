// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Documents → using Avalonia.Controls.Documents（Inline/Run）
// - using System.Windows.Markup → 移除
// - SelectionChangedEventArgs → Avalonia.Controls.SelectionChangedEventArgs
// - TextChangedEventArgs → Avalonia.Controls.TextChangedEventArgs
// - FontWeights.Medium → FontWeight.Medium（Avalonia.Media 单数形式）
// - Inlines.Add(string)/AddRange(IEnumerable<Inline>) 保持原样（Avalonia InlineCollection 兼容）
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class EditCustomActionWindow : ForkPlusDialogWindow
	{
		private readonly CustomCommand _customCommand;

		private readonly CustomCommandAction _initialAction;

		private bool _initialized;

		public CustomCommandAction OutAction
		{
			get
			{
				object selectedItem = CustomCommandTypeComboBox.SelectedItem;
				if (selectedItem == ProcessComboBoxItem)
				{
					string text = ScriptPathTextBox.Text;
					string text2 = ArgumentsTextBox.Text;
					bool valueOrDefault = ShowOutputCheckBox.IsChecked.GetValueOrDefault();
					bool valueOrDefault2 = WaitForExitCheckBox.IsChecked.GetValueOrDefault();
					return new ProcessCustomCommandAction(text, text2, valueOrDefault, valueOrDefault2);
				}
				if (selectedItem == BashComboBoxItem)
				{
					string script = ShScriptTextBox.Text.Replace("\r\n", "\n");
					bool valueOrDefault3 = ShowOutputCheckBox.IsChecked.GetValueOrDefault();
					bool valueOrDefault4 = WaitForExitCheckBox.IsChecked.GetValueOrDefault();
					return new ShCustomCommandAction(script, valueOrDefault3, valueOrDefault4);
				}
				if (selectedItem == UrlComboBoxItem)
				{
					return new UrlCustomCommandAction(UrlTextBox.Text);
				}
				if (selectedItem == CancelComboBoxItem)
				{
					return new CancelCustomCommandAction();
				}
				throw new InvalidOperationException();
			}
		}

		protected override bool IsSubmitAllowed => base.IsSubmitAllowed;

		public EditCustomActionWindow(CustomCommand customCommand, CustomCommandAction action, bool showCancel)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Edit Action");
		base.DialogDescription = Translate("Edit custom command action");
		base.SubmitButtonTitle = Translate("Save");
			ShScriptTextBox.FontFamily = FontConstants.MonospaceFontFamily;
			_customCommand = customCommand;
			_initialAction = action;
			RefreshControls(customCommand.Target, action);
			if (showCancel)
			{
				CancelComboBoxItem.Show();
			}
			_initialized = true;
		}

		private void CustomCommandTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!_initialized)
			{
				return;
			}
			object selectedItem = CustomCommandTypeComboBox.SelectedItem;
			CustomCommandAction action;
			if (selectedItem == ProcessComboBoxItem)
			{
				action = (_initialAction as ProcessCustomCommandAction) ?? new ProcessCustomCommandAction("${git}", "", showOutput: true, waitForExit: true);
			}
			else if (selectedItem == BashComboBoxItem)
			{
				action = (_initialAction as ShCustomCommandAction) ?? new ShCustomCommandAction(ShCustomCommandAction.DefaultScript(_customCommand.Target), showOutput: true, waitForExit: true);
			}
			else if (selectedItem == UrlComboBoxItem)
			{
				action = (_initialAction as UrlCustomCommandAction) ?? new UrlCustomCommandAction("https://hebin.me");
			}
			else
			{
				if (selectedItem != CancelComboBoxItem)
				{
					throw new InvalidOperationException();
				}
				action = (_initialAction as CancelCustomCommandAction) ?? new CancelCustomCommandAction();
			}
			RefreshControls(_customCommand.Target, action);
		}

		private void WaitForExitCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshShowOutputCheckBox();
		}

		private void ScriptPathTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshStatus();
		}

		private void ScriptPathButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = RepositoryManager.Instance.DefaultSourceDir();
			if (OpenDialog.SelectFile(this, "Select File", initialDirectory, "Executable files", "*.bat; *.exe; *.cmd", out var filePath))
			{
				ScriptPathTextBox.Text = filePath;
			}
		}

		private void RefreshControls(CustomCommandTarget target, CustomCommandAction action)
		{
			Inline[] inlines = new Inline[0];
			if (action is ProcessCustomCommandAction processCustomCommandAction)
			{
				if (!_initialized)
				{
					CustomCommandTypeComboBox.SelectedItem = ProcessComboBoxItem;
				}
				inlines = target.CreateVariablesList(showInternalGitPaths: true, showLocalPaths: true);
				ScriptPathTextBox.Text = processCustomCommandAction.Path;
				ArgumentsTextBox.Text = processCustomCommandAction.Parameters;
				ShowOutputCheckBox.IsChecked = processCustomCommandAction.ShowOutput;
				WaitForExitCheckBox.IsChecked = processCustomCommandAction.WaitForExit;
				UrlTextBlock.Collapse();
				UrlTextBox.Collapse();
				ProcessCustomCommandContainer.Show();
				ShScriptTextBlock.Collapse();
				ShScriptTextBox.Collapse();
				WaitForExitCheckBox.Show();
				ShowOutputCheckBox.Show();
			}
			else if (action is ShCustomCommandAction shCustomCommandAction)
			{
				if (!_initialized)
				{
					CustomCommandTypeComboBox.SelectedItem = BashComboBoxItem;
				}
				inlines = target.CreateVariablesList(showInternalGitPaths: false, showLocalPaths: true);
				ShScriptTextBox.Text = shCustomCommandAction.Script;
				ShowOutputCheckBox.IsChecked = shCustomCommandAction.ShowOutput;
				WaitForExitCheckBox.IsChecked = shCustomCommandAction.WaitForExit;
				UrlTextBlock.Collapse();
				UrlTextBox.Collapse();
				ProcessCustomCommandContainer.Collapse();
				ShScriptTextBlock.Show();
				ShScriptTextBox.Show();
				WaitForExitCheckBox.Show();
				ShowOutputCheckBox.Show();
			}
			else if (action is UrlCustomCommandAction urlCustomCommandAction)
			{
				if (!_initialized)
				{
					CustomCommandTypeComboBox.SelectedItem = UrlComboBoxItem;
				}
				inlines = target.CreateVariablesList();
				UrlTextBox.Text = urlCustomCommandAction.Url;
				UrlTextBlock.Show();
				UrlTextBox.Show();
				ProcessCustomCommandContainer.Collapse();
				ShScriptTextBlock.Collapse();
				ShScriptTextBox.Collapse();
				WaitForExitCheckBox.Collapse();
				ShowOutputCheckBox.Collapse();
			}
			else
			{
				if (!(action is CancelCustomCommandAction))
				{
					throw new InvalidOperationException();
				}
				if (!_initialized)
				{
					CustomCommandTypeComboBox.SelectedItem = CancelComboBoxItem;
				}
				UrlTextBlock.Collapse();
				UrlTextBox.Collapse();
				ProcessCustomCommandContainer.Collapse();
				ShScriptTextBlock.Collapse();
				ShScriptTextBox.Collapse();
				WaitForExitCheckBox.Collapse();
				ShowOutputCheckBox.Collapse();
			}
			UpdateDescription(inlines);
		}

		private void UpdateDescription(Inline[] inlines)
		{
			DescriptionTextBlock.Inlines.Clear();
			if (inlines.Length == 0)
			{
				return;
			}
			DescriptionTextBlock.Inlines.Add(new Run(Translate("Available variables:"))
			{
				FontSize = 13.0,
				// 阶段 4.5：WPF FontWeights.Medium → Avalonia FontWeight.Medium（单数）。
				FontWeight = FontWeight.Medium
			});
			DescriptionTextBlock.Inlines.Add(Environment.NewLine);
			DescriptionTextBlock.Inlines.Add(Environment.NewLine);
			List<Inline> list = new List<Inline>(4);
			CustomCommandUI uI = _customCommand.UI;
			if (uI != null)
			{
				int num = 1;
				CustomCommandUI.Control[] controls = uI.Controls;
				foreach (CustomCommandUI.Control control in controls)
				{
					if (control is CustomCommandUI.Control.GenericTextBox genericTextBox)
					{
						list.Add(new Run($"${num} - '{genericTextBox.Title}' {Translate("control variables")}:\n"));
						list.Add(new Run($"    ${num}{{text}}\t {Translate("string value")}\n"));
						list.Add(new Run("\n"));
					}
					else if (control is CustomCommandUI.Control.PathTextBox pathTextBox)
					{
						list.Add(new Run($"${num} - '{pathTextBox.Title}' {Translate("control variables")}:\n"));
						list.Add(new Run($"    ${num}{{path}}\t {Translate("selected path")}\n"));
						list.Add(new Run($"    ${num}{{path:name}}\t {Translate("last path component")}\n"));
						list.Add(new Run("\n"));
					}
					else if (control is CustomCommandUI.Control.Dropdown dropdown)
					{
						list.Add(new Run($"${num} - '{dropdown.Title}' {Translate("control variables")}:\n"));
						list.Add(new Run($"    ${num}{{sha}}\t {Translate("commit sha")}\n"));
						list.Add(new Run($"    ${num}{{sha:abbr}}\t {Translate("abbreviated commit sha")}\n"));
						list.Add(new Run($"    ${num}{{ref}}\t\t {Translate("branch name")}\n"));
						list.Add(new Run($"    ${num}{{ref:short}}\t {Translate("branch w/o remote prefix")}\n"));
						list.Add(new Run($"    ${num}{{ref:full}}\t {Translate("branch full reference")}\n"));
						list.Add(new Run("\n"));
					}
					else if (control is CustomCommandUI.Control.CheckBox checkBox)
					{
						list.Add(new Run($"${num} - '{checkBox.Title}' {Translate("control variables")}:\n"));
						list.Add(new Run($"    ${num}{{value}}\t {Translate("string value")}\n"));
						list.Add(new Run("\n"));
					}
					num++;
				}
			}
			DescriptionTextBlock.Inlines.AddRange(list);
			DescriptionTextBlock.Inlines.AddRange(inlines);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void RefreshShowOutputCheckBox()
		{
			if (!WaitForExitCheckBox.IsChecked.GetValueOrDefault())
			{
				ShowOutputCheckBox.IsChecked = false;
				ShowOutputCheckBox.IsEnabled = false;
			}
			else
			{
				ShowOutputCheckBox.IsEnabled = true;
			}
		}

		private void RefreshStatus()
		{
			string text = ScriptPathTextBox.Text;
			text = Environment.ExpandEnvironmentVariables(text);
			try
			{
				if (text != "${git}" && text != "$git" && text != "${sh}" && text != "$sh" && !File.Exists(text))
				{
					SetStatus(ForkPlusDialogStatus.Warning, "Script path not found");
					return;
				}
			}
			catch
			{
				SetStatus(ForkPlusDialogStatus.Warning, "Script path not found");
				return;
			}
			SetStatus(ForkPlusDialogStatus.None, string.Empty);
		}

	}
}
