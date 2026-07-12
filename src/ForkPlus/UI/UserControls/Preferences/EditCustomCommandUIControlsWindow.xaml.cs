using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls.Preferences
{
	public partial class EditCustomCommandUIControlsWindow : ForkPlusDialogWindow
	{
		private ObservableCollection<CustomCommandUIControlViewModel> _controlViewModels;

		private bool _stopComboBoxEvents;

		public CustomCommandUI.Control[] OutControls => _controlViewModels.Map((CustomCommandUIControlViewModel x) => x.Control);

		public EditCustomCommandUIControlsWindow(CustomCommandUI.Control[] controls)
		{
			base.ShowLogo = false;
			base.ShowHeader = false;
			_stopComboBoxEvents = true;
			InitializeComponent();
			base.SubmitButtonTitle = PreferencesLocalization.Current("Save");
			ObservableCollection<CustomCommandUIControlViewModel> observableCollection = new ObservableCollection<CustomCommandUIControlViewModel>();
			for (int i = 0; i < controls.Length; i++)
			{
				observableCollection.Add(new CustomCommandUIControlViewModel(i, controls[i]));
			}
			_controlViewModels = observableCollection;
			ControlsListBox.ItemsSource = _controlViewModels;
			CustomCommandUIControlViewModel customCommandUIControlViewModel = _controlViewModels.FirstOrDefault();
			if (customCommandUIControlViewModel != null)
			{
				SelectAndFocusControl(customCommandUIControlViewModel);
			}
			else
			{
				FallbackUserControl.Show();
			}
		}

		private void RemoveCustomCommandButton_Click(object sender, RoutedEventArgs e)
		{
			if (ControlsListBox.SelectedItem is CustomCommandUIControlViewModel item && new MessageBoxWindow("Do you want to remove the selected control?", "You can't undo this action", "Remove", "Cancel", showCancelButton: true, 550.0)
			{
				Owner = this,
				WindowStartupLocation = WindowStartupLocation.CenterOwner
			}.ShowDialog().GetValueOrDefault())
			{
				int num = _controlViewModels.IndexOf(item) - 1;
				_controlViewModels.Remove(item);
				CustomCommandUIControlViewModel control = ((num != -1) ? _controlViewModels[num] : _controlViewModels.FirstOrDefault());
				SelectAndFocusControl(control);
			}
		}

		private void AddGenericTextBoxMenuItem_Click(object sender, RoutedEventArgs e)
		{
			AddControl(new CustomCommandUI.Control.GenericTextBox(PreferencesLocalization.Current("Text Box")));
		}

		private void AddPathTextBoxMenuItem_Click(object sender, RoutedEventArgs e)
		{
			AddControl(new CustomCommandUI.Control.PathTextBox(PreferencesLocalization.Current("Path Text Box"), CustomCommandUI.Control.PathTextBox.DialogType.SaveFile));
		}

		private void AddLocalBranchSelectorMenuItem_Click(object sender, RoutedEventArgs e)
		{
			AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Local Branch"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/heads/"));
		}

		private void AddRemoteBranchSelectorMenuItem_Click(object sender, RoutedEventArgs e)
		{
			AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Remote Branch"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/remotes/"));
		}

		private void AddTagSelectorMenuItem_Click(object sender, RoutedEventArgs e)
		{
			AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Tag"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/tags/"));
		}

		private void AddReferenceSelectorMenuItem_Click(object sender, RoutedEventArgs e)
		{
			AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Reference"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/"));
		}

		private void AddCheckBoxMenuItem_Click(object sender, RoutedEventArgs e)
		{
			AddControl(new CustomCommandUI.Control.CheckBox(PreferencesLocalization.Current("Check Box")));
		}

		private void AddControl(CustomCommandUI.Control control)
		{
			CustomCommandUIControlViewModel customCommandUIControlViewModel = new CustomCommandUIControlViewModel(_controlViewModels.Count, control);
			_controlViewModels.Add(customCommandUIControlViewModel);
			SelectAndFocusControl(customCommandUIControlViewModel);
			SaveActiveControl();
		}

		private void ControlTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!_stopComboBoxEvents)
			{
				RefreshControls();
				RefreshDescription();
				SaveActiveControl();
			}
		}

		private void TextBoxTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!_stopComboBoxEvents)
			{
				RefreshTextBoxControlsVisibility(GetSelectedTextBoxType());
				RefreshDescription();
				SaveActiveControl();
			}
		}

		private void DialogTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!_stopComboBoxEvents)
			{
				RefreshPathTextBoxControlsVisibility(GetSelectedDialogType());
				RefreshDescription();
				SaveActiveControl();
			}
		}

		private void ControlsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel customCommandUIControlViewModel))
			{
				FallbackUserControl.Show();
				return;
			}
			FallbackUserControl.Collapse();
			_stopComboBoxEvents = true;
			CustomCommandUI.Control control = customCommandUIControlViewModel.Control;
			if (control is CustomCommandUI.Control.GenericTextBox)
			{
				ControlTypeComboBox.SelectedItem = TextBoxComboBoxItem;
				TextBoxTypeComboBox.SelectedItem = GenericComboBoxItem;
			}
			else if (control is CustomCommandUI.Control.PathTextBox pathTextBox)
			{
				ControlTypeComboBox.SelectedItem = TextBoxComboBoxItem;
				TextBoxTypeComboBox.SelectedItem = FilePathComboBoxItem;
				RefreshDialogTypeComboBox(pathTextBox.PathDialogType);
			}
			else if (control is CustomCommandUI.Control.Dropdown)
			{
				ControlTypeComboBox.SelectedItem = DropdownComboBoxItem;
			}
			else if (control is CustomCommandUI.Control.CheckBox)
			{
				ControlTypeComboBox.SelectedItem = CheckBoxComboBoxItem;
			}
			RefreshControls();
			RefreshDescription();
			_stopComboBoxEvents = false;
		}

		private void ListBoxItem_Drop(object sender, DragEventArgs e)
		{
			if (!(sender is DragAndDropListBoxItem { DataContext: var dataContext } dragAndDropListBoxItem))
			{
				return;
			}
			CustomCommandUIControlViewModel targetItem = dataContext as CustomCommandUIControlViewModel;
			if (targetItem == null || !(e.Data.GetData(typeof(object[])) is object[] array) || array.Length != 1)
			{
				return;
			}
			CustomCommandUIControlViewModel droppedItem = array.CompactMap((object x) => x as CustomCommandUIControlViewModel).First();
			if (droppedItem == targetItem)
			{
				return;
			}
			int num = _controlViewModels.IndexOf((CustomCommandUIControlViewModel x) => x == droppedItem);
			int num2 = _controlViewModels.IndexOf((CustomCommandUIControlViewModel x) => x == targetItem);
			if ((dragAndDropListBoxItem.DropPosition != ForkPlus.UI.Dialogs.DropPosition.Bottom || num - 1 != num2) && (dragAndDropListBoxItem.DropPosition != 0 || num + 1 != num2))
			{
				_controlViewModels.RemoveAt(num);
				_controlViewModels.Insert(num2, droppedItem);
				for (int i = 0; i < _controlViewModels.Count; i++)
				{
					_controlViewModels[i].RowIndex = i;
				}
				SelectAndFocusControl(droppedItem);
				ControlsListBox.ScrollIntoView(droppedItem);
			}
		}

		private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			SaveActiveControl();
		}

		private void CheckBoxDefaultValueCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			SaveActiveControl();
		}

		private void RefreshControls()
		{
			if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel { Control: var control }))
			{
				return;
			}
			switch (GetSelectedControlType())
			{
			case CustomCommandUI.Control.ControlType.TextBox:
				DropdownControlsContainer.Collapse();
				CheckBoxControlsContainer.Collapse();
				TextBoxTypeTextBlock.Show();
				TextBoxTypeComboBox.Show();
				switch (GetSelectedTextBoxType())
				{
				case CustomCommandUI.Control.TextBoxType.Generic:
				{
					CustomCommandUI.Control.GenericTextBox genericTextBox = control as CustomCommandUI.Control.GenericTextBox;
					TitleTextBox.Text = genericTextBox?.Title ?? PreferencesLocalization.Current("Text Box");
					PathTextBoxControlsContainer.Collapse();
					GenericTextBoxControlsContainer.Show();
					GenericTextBox.Text = genericTextBox?.Text ?? "";
					GenericPlaceholderTextBox.Text = genericTextBox?.Placeholder ?? "";
					break;
				}
				case CustomCommandUI.Control.TextBoxType.FilePath:
				{
					CustomCommandUI.Control.PathTextBox pathTextBox = control as CustomCommandUI.Control.PathTextBox;
					TitleTextBox.Text = pathTextBox?.Title ?? PreferencesLocalization.Current("Path Text Box");
					GenericTextBoxControlsContainer.Collapse();
					PathTextBoxControlsContainer.Show();
					CustomCommandUI.Control.PathTextBox.DialogType dialogType = pathTextBox?.PathDialogType ?? CustomCommandUI.Control.PathTextBox.DialogType.SaveFile;
					RefreshDialogTypeComboBox(dialogType);
					RefreshPathTextBoxControlsVisibility(dialogType);
					DefaultDirectoryTextBox.Text = pathTextBox?.DefaultDirectory ?? "";
					FileNameTextBox.Text = pathTextBox?.FileName ?? "";
					break;
				}
				}
				break;
			case CustomCommandUI.Control.ControlType.Dropdown:
			{
				DropdownControlsContainer.Show();
				CheckBoxControlsContainer.Collapse();
				GenericTextBoxControlsContainer.Collapse();
				PathTextBoxControlsContainer.Collapse();
				TextBoxTypeTextBlock.Collapse();
				TextBoxTypeComboBox.Collapse();
				CustomCommandUI.Control.Dropdown dropdown = control as CustomCommandUI.Control.Dropdown;
				TitleTextBox.Text = dropdown?.Title ?? PreferencesLocalization.Current("Reference");
				DropdownFilterTextBox.Text = dropdown?.Filter ?? "";
				break;
			}
			case CustomCommandUI.Control.ControlType.CheckBox:
			{
				CheckBoxControlsContainer.Show();
				DropdownControlsContainer.Collapse();
				GenericTextBoxControlsContainer.Collapse();
				PathTextBoxControlsContainer.Collapse();
				TextBoxTypeTextBlock.Collapse();
				TextBoxTypeComboBox.Collapse();
				CustomCommandUI.Control.CheckBox checkBox = control as CustomCommandUI.Control.CheckBox;
				TitleTextBox.Text = checkBox?.Title ?? PreferencesLocalization.Current("Check Box");
				CheckBoxDefaultValueCheckBox.IsChecked = checkBox?.DefaultValue ?? false;
				CheckBoxCheckedValueTextBox.Text = checkBox?.CheckedValue ?? "";
				CheckBoxUncheckedValueTextBox.Text = checkBox?.UncheckedValue ?? "";
				break;
			}
			}
		}

		private void RefreshDescription()
		{
			if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel item))
			{
				return;
			}
			int num = _controlViewModels.IndexOf(item) + 1;
			switch (GetSelectedControlType())
			{
			case CustomCommandUI.Control.ControlType.TextBox:
				switch (GetSelectedTextBoxType())
				{
				case CustomCommandUI.Control.TextBoxType.Generic:
					DescriptionTextBlock.Inlines.Clear();
					DescriptionTextBlock.Inlines.Add(new Run("Generic Text Box\n\n")
					{
						FontSize = 13.0,
						FontWeight = FontWeights.Medium
					});
					DescriptionTextBlock.Inlines.Add(new Run($"Can be used to prompt a generic string.\n\nPossible use cases:\n- a name for a new branch\n- a commit message\n- a custom argument for a git command\n\nYou can set the default value and the placeholder text.\n\nThe entered string value can be used in the 'OK' action as `${num}{{text}}` variable."));
					break;
				case CustomCommandUI.Control.TextBoxType.FilePath:
					DescriptionTextBlock.Inlines.Clear();
					DescriptionTextBlock.Inlines.Add(new Run("File Path Text Box\n\n")
					{
						FontSize = 13.0,
						FontWeight = FontWeights.Medium
					});
					DescriptionTextBlock.Inlines.Add(new Run($"Prompt file path using the system open file dialog.\n\nYou can set the default folder and the file name.\n\nThe selected path can be used in the 'OK' action as `${num}{{path}}` variable."));
					break;
				}
				break;
			case CustomCommandUI.Control.ControlType.Dropdown:
				DescriptionTextBlock.Inlines.Clear();
				DescriptionTextBlock.Inlines.Add(new Run("Branch Drop Down\n\n")
				{
					FontSize = 13.0,
					FontWeight = FontWeights.Medium
				});
				DescriptionTextBlock.Inlines.Add(new Run($"Allows to select a branch or a tag (i.e a reference).\n\nThe list can be filtered by a full reference prefix.\nFor example:\n- `refs/heads` - local branches\n- `refs/remotes` - remote branches\n- `refs/tags` - tags\n\nYou can use more complex filters like:\n- `refs/heads/feature` - local feature branches\n- `refs/heads/john` - John's local branches\n- `refs/remotes/origin` - remote branches of the `origin` remote\n\nMultiple filters can be separated by space:\n- `refs/heads/develop refs/heads/main` - develop and main branches\n\nThe selected branch or tag name can be used in the 'OK' action as `${num}{{ref}}` variable."));
				break;
			case CustomCommandUI.Control.ControlType.CheckBox:
				DescriptionTextBlock.Inlines.Clear();
				DescriptionTextBlock.Inlines.Add(new Run("Check Box\n\n")
				{
					FontSize = 13.0,
					FontWeight = FontWeights.Medium
				});
				DescriptionTextBlock.Inlines.Add(new Run($"Can be used to pass optional argument relying on the check box state.\n\n- You can set value for both unchecked and checked states.\n- A value can be empty.\n\nPossible use cases:\n- an optional argument for a command (e.g. `--force` when enabled, and none otherwise)\n\nCheck box value can be used in the 'OK' action as `${num}{{value}}` variable."));
				break;
			}
		}

		private void SaveActiveControl()
		{
			if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel customCommandUIControlViewModel) || !(ControlTypeComboBox.SelectedItem is ComboBoxItem comboBoxItem))
			{
				return;
			}
			CustomCommandUI.Control control = customCommandUIControlViewModel.Control;
			if (comboBoxItem == TextBoxComboBoxItem)
			{
				if (!(TextBoxTypeComboBox.SelectedItem is ComboBoxItem comboBoxItem2))
				{
					return;
				}
				if (comboBoxItem2 == GenericComboBoxItem)
				{
					string text = TitleTextBox.Text;
					string text2 = GenericTextBox.Text;
					string text3 = GenericPlaceholderTextBox.Text;
					control = new CustomCommandUI.Control.GenericTextBox(text, text2, text3);
				}
				else if (comboBoxItem2 == FilePathComboBoxItem)
				{
					string text4 = TitleTextBox.Text;
					CustomCommandUI.Control.PathTextBox.DialogType selectedDialogType = GetSelectedDialogType();
					string text5 = DefaultDirectoryTextBox.Text;
					string text6 = FileNameTextBox.Text;
					control = new CustomCommandUI.Control.PathTextBox(text4, selectedDialogType, text5, text6);
				}
			}
			else if (comboBoxItem == DropdownComboBoxItem)
			{
				string text7 = TitleTextBox.Text;
				string text8 = DropdownFilterTextBox.Text;
				control = new CustomCommandUI.Control.Dropdown(text7, CustomCommandUI.Control.Dropdown.DropdownType.References, text8);
			}
			else if (comboBoxItem == CheckBoxComboBoxItem)
			{
				string text9 = TitleTextBox.Text;
				bool valueOrDefault = CheckBoxDefaultValueCheckBox.IsChecked.GetValueOrDefault();
				string text10 = CheckBoxCheckedValueTextBox.Text;
				string text11 = CheckBoxUncheckedValueTextBox.Text;
				control = new CustomCommandUI.Control.CheckBox(text9, valueOrDefault, text10, text11);
			}
			customCommandUIControlViewModel.Control = control;
		}

		private CustomCommandUI.Control.ControlType GetSelectedControlType()
		{
			if (!(ControlTypeComboBox.SelectedItem is ComboBoxItem comboBoxItem))
			{
				throw new InvalidOperationException();
			}
			if (comboBoxItem == TextBoxComboBoxItem)
			{
				return CustomCommandUI.Control.ControlType.TextBox;
			}
			if (comboBoxItem == DropdownComboBoxItem)
			{
				return CustomCommandUI.Control.ControlType.Dropdown;
			}
			if (comboBoxItem == CheckBoxComboBoxItem)
			{
				return CustomCommandUI.Control.ControlType.CheckBox;
			}
			throw new InvalidOperationException();
		}

		private CustomCommandUI.Control.TextBoxType GetSelectedTextBoxType()
		{
			ComboBoxItem comboBoxItem = TextBoxTypeComboBox.SelectedItem as ComboBoxItem;
			if (comboBoxItem == null)
			{
				TextBoxTypeComboBox.SelectedItem = GenericComboBoxItem;
				comboBoxItem = GenericComboBoxItem;
			}
			if (comboBoxItem == GenericComboBoxItem)
			{
				return CustomCommandUI.Control.TextBoxType.Generic;
			}
			if (comboBoxItem == FilePathComboBoxItem)
			{
				return CustomCommandUI.Control.TextBoxType.FilePath;
			}
			throw new InvalidOperationException();
		}

		private CustomCommandUI.Control.PathTextBox.DialogType GetSelectedDialogType()
		{
			ComboBoxItem comboBoxItem = DialogTypeComboBox.SelectedItem as ComboBoxItem;
			if (comboBoxItem == null)
			{
				DialogTypeComboBox.SelectedItem = SaveFileComboBoxItem;
				comboBoxItem = SaveFileComboBoxItem;
			}
			if (comboBoxItem == SaveFileComboBoxItem)
			{
				return CustomCommandUI.Control.PathTextBox.DialogType.SaveFile;
			}
			if (comboBoxItem == OpenFileComboBoxItem)
			{
				return CustomCommandUI.Control.PathTextBox.DialogType.OpenFile;
			}
			if (comboBoxItem == OpenDirectoryComboBoxItem)
			{
				return CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory;
			}
			throw new InvalidOperationException();
		}

		private void RefreshTextBoxControlsVisibility(CustomCommandUI.Control.TextBoxType textBoxType)
		{
			switch (textBoxType)
			{
			case CustomCommandUI.Control.TextBoxType.Generic:
				PathTextBoxControlsContainer.Collapse();
				GenericTextBoxControlsContainer.Show();
				break;
			case CustomCommandUI.Control.TextBoxType.FilePath:
				PathTextBoxControlsContainer.Show();
				GenericTextBoxControlsContainer.Collapse();
				break;
			}
		}

		private void RefreshDialogTypeComboBox(CustomCommandUI.Control.PathTextBox.DialogType dialogType)
		{
			switch (dialogType)
			{
			case CustomCommandUI.Control.PathTextBox.DialogType.SaveFile:
				DialogTypeComboBox.SelectedItem = SaveFileComboBoxItem;
				break;
			case CustomCommandUI.Control.PathTextBox.DialogType.OpenFile:
				DialogTypeComboBox.SelectedItem = OpenFileComboBoxItem;
				break;
			case CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory:
				DialogTypeComboBox.SelectedItem = OpenDirectoryComboBoxItem;
				break;
			}
		}

		private void RefreshPathTextBoxControlsVisibility(CustomCommandUI.Control.PathTextBox.DialogType dialogType)
		{
			if (dialogType != CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory)
			{
				FileNameTextBlock.Show();
				FileNameTextBox.Show();
			}
			else
			{
				FileNameTextBlock.Collapse();
				FileNameTextBox.Collapse();
			}
		}

		private void SelectAndFocusControl(CustomCommandUIControlViewModel control)
		{
			ControlsListBox.SelectedItem = control;
			ControlsListBox.Focus();
		}

	}
}
