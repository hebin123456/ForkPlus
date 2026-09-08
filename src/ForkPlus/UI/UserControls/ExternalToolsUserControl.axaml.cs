using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls
{
	public partial class ExternalToolsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private ToolDefinition[] _toolDefinitions;

		private ForkPlusDialogWindow _parentWindow;

		private ObservableCollection<ExternalToolViewModel> _toolViewModels;

		public ExternalTool[] Result => _toolViewModels.Map((ExternalToolViewModel x) => x.ExternalTool);

		public ExternalToolsUserControl()
		{
			InitializeComponent();
		}

		public void ApplyLocalization()
		{
			Preferences.PreferencesLocalization.Apply(this, Settings.ForkPlusSettings.Default.UiLanguage);
			if (_toolViewModels == null)
			{
				return;
			}
			foreach (ExternalToolViewModel toolViewModel in _toolViewModels)
			{
				toolViewModel.ApplyLocalization();
			}
		}

		public void Initialize(ForkPlusDialogWindow parentWindow, ExternalTool[] externalTools, ToolDefinition[] toolDefinitions, string argumentsHint)
		{
			_parentWindow = parentWindow;
			_toolDefinitions = toolDefinitions;
			_toolViewModels = CreateExternalToolViewModels(externalTools);
			ToolsListBox.ItemsSource = _toolViewModels;
			ArgumentsHintTextBlock.Tag = argumentsHint;
			ExternalToolViewModel externalToolViewModel = _toolViewModels.FirstItem();
			if (externalToolViewModel != null)
			{
				ToolsListBox.SelectedItem = externalToolViewModel;
				ToolsListBox.Focus();
			}
			else
			{
				ToolsFallback.Show();
			}
		}

		private static ObservableCollection<ExternalToolViewModel> CreateExternalToolViewModels(ExternalTool[] tools)
		{
			ExternalToolViewModel[] array = tools.Map((ExternalTool x) => new ExternalToolViewModel(x));
			Array.Sort(array, (ExternalToolViewModel x, ExternalToolViewModel y) => -1 * x.IsAvailable.CompareTo(y.IsAvailable));
			return new ObservableCollection<ExternalToolViewModel>(array);
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			string initialDirectory = string.Empty;
			try
			{
				string text = ToolPathTextBox.Text;
				if (text != null && File.Exists(text))
				{
					initialDirectory = Path.GetDirectoryName(text);
				}
			}
			catch
			{
			}
			// Bug 修复（2026-09-07，git-mm 同款）：*.exe;*.cmd 过滤器在 Linux/macOS 上滤掉无扩展名
			// 可执行文件（terminal/editor 本体）——Unix 端退化为无过滤器。
			if (OpenDialog.SelectFile(_parentWindow, "Select external tool", initialDirectory, "Applications", OpenDialog.ExecutableFilterOrNullOnUnix("*.exe; *.cmd"), out var filePath))
			{
				ToolPathTextBox.Text = filePath;
			}
		}

		private void AddToolButton_Click(object sender, RoutedEventArgs e)
		{
			string name = PreferencesLocalization.Current("Custom");
			int num = _toolViewModels.Count((ExternalToolViewModel x) => x.Name.ToLower().StartsWith(name.ToLower()));
			if (num > 0)
			{
				name += $"{num}";
			}
			ExternalToolViewModel externalToolViewModel = new ExternalToolViewModel(name);
			_toolViewModels.Add(externalToolViewModel);
			ToolsListBox.SelectedItem = externalToolViewModel;
			ToolsListBox.Focus();
			ToolsListBox.ScrollIntoView(externalToolViewModel);
		}

		private void RemoveToolButton_Click(object sender, RoutedEventArgs e)
		{
			if (ToolsListBox.SelectedItem is ExternalToolViewModel item && new MessageBoxWindow("Do you want to remove the selected external tool?", "You can't undo this action", "Remove", "Cancel", showCancelButton: true, 550.0)
				.SetOwnerAndCenter(_parentWindow).ShowDialog().GetValueOrDefault()) // Migration note：WPF { Owner=.., WindowStartupLocation=CenterOwner } → 链式扩展。
			{
				int num = _toolViewModels.IndexOf(item) - 1;
				_toolViewModels.Remove(item);
				ExternalToolViewModel selectedItem = ((num != -1) ? _toolViewModels[num] : _toolViewModels.FirstItem());
				ToolsListBox.SelectedItem = selectedItem;
				ToolsListBox.Focus();
			}
		}

		private void ToolsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ToolsListBox.SelectedItem == null)
			{
				ToolsFallback.Show();
			}
			else
			{
				ToolsFallback.Collapse();
			}
		}

		private void ToolsListBox_ContextMenuOpening(object sender, global::Avalonia.Input.ContextRequestedEventArgs e)
		{
			if ((sender as ListBox)?.ContainerFromElement(e.Source as global::Avalonia.Visual) /* Migration note：WPF 双参静态方法 → 扩展方法实例调用 */ is ListBoxItem { DataContext: ExternalToolViewModel dataContext })
			{
				ToolsListBox.ContextMenu.Items.Clear();
				ToolsListBox.ContextMenu.SetItems(GetContextMenu(dataContext, _toolViewModels, _toolDefinitions));
			}
			else
			{
				e.Handled = true;
				ToolsListBox.ContextMenu.Close();
			}
		}

		private static IEnumerable<Control> GetContextMenu(ExternalToolViewModel tool, ObservableCollection<ExternalToolViewModel> allTools, ToolDefinition[] toolDefinitions)
		{
			MenuItem primaryMenuItem = new MenuItem();
			primaryMenuItem.Header = Preferences.PreferencesLocalization.MenuHeader("Primary");
			primaryMenuItem.IsEnabled = tool.IsAvailable || tool.IsPrimary;
			primaryMenuItem.Click += delegate
			{
				tool.IsPrimary = !tool.IsPrimary;
				if (tool.IsPrimary)
				{
					foreach (ExternalToolViewModel allTool in allTools)
					{
						if (allTool != tool)
						{
							allTool.IsPrimary = false;
						}
					}
				}
			};
			primaryMenuItem.IsChecked = tool.IsPrimary;
			yield return primaryMenuItem;

			MenuItem visibleMenuItem = new MenuItem();
			visibleMenuItem.Header = Preferences.PreferencesLocalization.MenuHeader("Visible");
			visibleMenuItem.Click += delegate
			{
				tool.IsVisible = !tool.IsVisible;
			};
			visibleMenuItem.IsChecked = tool.IsVisible;
			yield return visibleMenuItem;

			if (tool.IsPredefined)
			{
				yield return new Separator();

				MenuItem resetMenuItem = new MenuItem();
				resetMenuItem.Header = Preferences.PreferencesLocalization.MenuHeader("Reset to default");
				resetMenuItem.Click += delegate
				{
					tool.ResetToDefault(toolDefinitions);
				};
				yield return resetMenuItem;
			}
		}

	}
}
