using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class IntegrationUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private ForkPlusDialogWindow _parentWindow;

		public IntegrationUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
		{
			_parentWindow = parentWindow;
			ExternalMergeToolsUserControl.Initialize(parentWindow, ExternalToolManager.RevealAvailableMergeTools(includeNonExistent: true), ExternalToolManager.MergeToolDefinitions, "Available variables: $LOCAL, $REMOTE, $BASE, $MERGED");
			ExternalDiffToolsUserControl.Initialize(parentWindow, ExternalToolManager.RevealAvailableDiffTools(includeNonExistent: true), ExternalToolManager.DiffToolDefinitions, "Available variables: $LOCAL, $REMOTE");
			string[] array = new string[5]
			{
				ShellTool.DefaultType,
				ShellTool.WindowsTerminalType,
				ShellTool.CommandPromptType,
				ShellTool.PowerShellType,
				ShellTool.CustomType
			};
			ShellToolComboBox.ItemsSource = array;
			ShellToolComboBox.SelectedItem = IReadOnlyListExtensions.FirstItem(array, (string x) => x == ForkPlusSettings.Default.ShellTool.Type);
			ShellToolPathTextBox.Text = ForkPlusSettings.Default.ShellTool.ApplicationPath;
			ShellToolArgumentsTextBox.Text = ForkPlusSettings.Default.ShellTool.Arguments;
			ShowBugtrackerLinksCheckBox.IsChecked = ForkPlusSettings.Default.ShowBugtrackerLinks;
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			ExternalMergeToolsUserControl.ApplyLocalization();
			ExternalDiffToolsUserControl.ApplyLocalization();
		}

		public void Save()
		{
			ExternalToolManager.SaveMergeToolsSettings(ExternalMergeToolsUserControl.Result);
			ExternalToolManager.SaveDiffToolsSettings(ExternalDiffToolsUserControl.Result);
			SaveShellToolSettings();
			ForkPlusSettings.Default.ShowBugtrackerLinks = ShowBugtrackerLinksCheckBox.IsChecked.Value;
		}

		private void ShellToolComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			ShellTool shellTool = CreateShell(ShellToolComboBox.SelectedItem as string, null, null);
			ShellToolPathTextBox.Text = shellTool.ApplicationPath;
			ShellToolArgumentsTextBox.Text = shellTool.Arguments;
			ShellToolArgumentsTextBox.IsEnabled = shellTool.Type == ShellTool.CustomType;
		}

		private void BrowseShellTool_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = string.Empty;
			try
			{
				string text = ShellToolPathTextBox.Text;
				if (text != null && File.Exists(text))
				{
					initialDirectory = Path.GetDirectoryName(text);
				}
			}
			catch
			{
			}
			if (OpenDialog.SelectExecutableFile(_parentWindow, "Select shell", initialDirectory, out var filePath))
			{
				ShellToolPathTextBox.Text = filePath;
			}
		}

		private void SaveShellToolSettings()
		{
			string text = ShellToolPathTextBox.Text;
			string shellType = (string)ShellToolComboBox.SelectedItem;
			string text2 = ShellToolArgumentsTextBox.Text;
			ForkPlusSettings.Default.ShellTool = CreateShell(shellType, text, text2);
			NotificationCenter.Current.RaiseShellChanged(this);
		}

		private static ShellTool CreateShell(string shellType, string path, string arguments)
		{
			if (shellType == ShellTool.DefaultType)
			{
				return new ShellTool.Default();
			}
			if (shellType == ShellTool.WindowsTerminalType)
			{
				if (string.IsNullOrEmpty(path))
				{
					path = ShellTool.WindowsTerminal.TryFindInstance();
				}
				return new ShellTool.WindowsTerminal(path);
			}
			if (shellType == ShellTool.CommandPromptType)
			{
				if (string.IsNullOrEmpty(path))
				{
					path = ShellTool.CommandPrompt.TryFindInstance();
				}
				return new ShellTool.CommandPrompt(path);
			}
			if (shellType == ShellTool.PowerShellType)
			{
				if (string.IsNullOrEmpty(path))
				{
					path = ShellTool.PowerShell.TryFindInstance();
				}
				return new ShellTool.PowerShell(path);
			}
			return new ShellTool.Custom(path, arguments);
		}

	}
}
