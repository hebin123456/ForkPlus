// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/EditCustomCommandUIControlsWindow.xaml.cs（495 行）：
//   - public partial class EditCustomCommandUIControlsWindow : ForkPlusDialogWindow
//   - 字段：ObservableCollection<CustomCommandUIControlViewModel> _controlViewModels /
//     bool _stopComboBoxEvents
//   - 构造函数(CustomCommandUI.Control[])：
//     * ShowLogo=false / ShowHeader=false
//     * SubmitButtonTitle = PreferencesLocalization.Current("Save")
//     * 遍历 controls 构造 _controlViewModels
//     * ControlsListBox.ItemsSource + SelectAndFocusControl
//   - OutControls getter：_controlViewModels.Map(x => x.Control)
//   - 7 个 Add*MenuItem_Click：构造 CustomCommandUI.Control 子类
//   - RemoveCustomCommandButton_Click：MessageBoxWindow 确认 + Remove
//   - ControlTypeComboBox_SelectionChanged / TextBoxTypeComboBox_SelectionChanged /
//     DialogTypeComboBox_SelectionChanged：切换 UI + RefreshDescription + SaveActiveControl
//   - ListBoxItem_Drop：拖拽排序（DragAndDropListBoxItem + DropPosition）
//   - RefreshControls / RefreshDescription / SaveActiveControl
//   - GetSelectedControlType / GetSelectedTextBoxType / GetSelectedDialogType
//   - RefreshTextBoxControlsVisibility / RefreshDialogTypeComboBox / RefreshPathTextBoxControlsVisibility
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ForkPlusDialogWindow 基类动态构造 chrome == spike 子类 axaml 自带 Header + Footer，
//      构造函数 SetFooter/Footer 注入
//   2. WPF ShowLogo=false / ShowHeader=false == spike axaml 中不放 Logo
//   3. WPF MessageBoxWindow（删除确认）== spike 移除（spike 直接删除）
//   4. WPF DragAndDropListBoxItem 拖拽排序 == spike 移除（spike 不支持拖拽）
//   5. WPF Inlines（DescriptionTextBlock.Inlines.Add(new Run(...))）== spike 改用 TextBlock.Text
//      （spike 不用富文本，RefreshDescription 简化为字符串拼接）
//   6. WPF 7 个 ContextMenu MenuItem == spike 改用 7 个 Button
//   7. WPF ControlsListBox.ScrollIntoView == spike 保留（Avalonia ListBox.ScrollIntoView）
//   8. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
//   9. 继承 global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class EditCustomCommandUIControlsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 对照 WPF: private ObservableCollection<CustomCommandUIControlViewModel> _controlViewModels;
        private ObservableCollection<CustomCommandUIControlViewModel>? _controlViewModels;

        // 对照 WPF: private bool _stopComboBoxEvents;
        private bool _stopComboBoxEvents;

        // 对照 WPF: public CustomCommandUI.Control[] OutControls
        public CustomCommandUI.Control[] OutControls
        {
            get
            {
                if (_controlViewModels == null) return new CustomCommandUI.Control[0];
                return _controlViewModels.Map((CustomCommandUIControlViewModel x) => x.Control);
            }
        }

        // 对照 WPF: public EditCustomCommandUIControlsWindow(CustomCommandUI.Control[] controls)
        public EditCustomCommandUIControlsWindow(CustomCommandUI.Control[] controls)
        {
            ShowFooter = true;
            // spike: base.ShowLogo = false; // axaml 中不放 Logo
            // spike: base.ShowHeader = false; // axaml 中不放 Header
            _stopComboBoxEvents = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);

            SubmitButtonTitle = PreferencesLocalization.Current("Save");

            ObservableCollection<CustomCommandUIControlViewModel> observableCollection = new ObservableCollection<CustomCommandUIControlViewModel>();
            for (int i = 0; i < controls.Length; i++)
            {
                observableCollection.Add(new CustomCommandUIControlViewModel(i, controls[i]));
            }
            _controlViewModels = observableCollection;
            ControlsListBox.ItemsSource = _controlViewModels;
            CustomCommandUIControlViewModel? customCommandUIControlViewModel = _controlViewModels.FirstOrDefault();
            if (customCommandUIControlViewModel != null)
            {
                SelectAndFocusControl(customCommandUIControlViewModel);
            }
            _stopComboBoxEvents = false;
        }

        // 对照 WPF: private void RemoveCustomCommandButton_Click(...)
        // spike 版：移除 MessageBoxWindow 确认（spike 直接删除）
        private void RemoveCustomCommandButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_controlViewModels == null) return;
            if (ControlsListBox.SelectedItem is not CustomCommandUIControlViewModel item) return;
            // spike: WPF 用 MessageBoxWindow 确认删除，spike 版直接删除
            int num = _controlViewModels.IndexOf(item) - 1;
            _controlViewModels.Remove(item);
            CustomCommandUIControlViewModel? control = ((num != -1) ? _controlViewModels[num] : _controlViewModels.FirstOrDefault());
            if (control != null) SelectAndFocusControl(control);
        }

        // 对照 WPF: private void AddGenericTextBoxMenuItem_Click(...)
        private void AddGenericTextBoxMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddControl(new CustomCommandUI.Control.GenericTextBox(PreferencesLocalization.Current("Text Box")));
        }

        // 对照 WPF: private void AddPathTextBoxMenuItem_Click(...)
        private void AddPathTextBoxMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddControl(new CustomCommandUI.Control.PathTextBox(PreferencesLocalization.Current("Path Text Box"), CustomCommandUI.Control.PathTextBox.DialogType.SaveFile));
        }

        // 对照 WPF: private void AddLocalBranchSelectorMenuItem_Click(...)
        private void AddLocalBranchSelectorMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Local Branch"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/heads/"));
        }

        // 对照 WPF: private void AddRemoteBranchSelectorMenuItem_Click(...)
        private void AddRemoteBranchSelectorMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Remote Branch"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/remotes/"));
        }

        // 对照 WPF: private void AddTagSelectorMenuItem_Click(...)
        private void AddTagSelectorMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Tag"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/tags/"));
        }

        // 对照 WPF: private void AddReferenceSelectorMenuItem_Click(...)
        private void AddReferenceSelectorMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddControl(new CustomCommandUI.Control.Dropdown(PreferencesLocalization.Current("Reference"), CustomCommandUI.Control.Dropdown.DropdownType.References, "refs/"));
        }

        // 对照 WPF: private void AddCheckBoxMenuItem_Click(...)
        private void AddCheckBoxMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            AddControl(new CustomCommandUI.Control.CheckBox(PreferencesLocalization.Current("Check Box")));
        }

        // 对照 WPF: private void AddControl(CustomCommandUI.Control control)
        private void AddControl(CustomCommandUI.Control control)
        {
            if (_controlViewModels == null) return;
            CustomCommandUIControlViewModel customCommandUIControlViewModel = new CustomCommandUIControlViewModel(_controlViewModels.Count, control);
            _controlViewModels.Add(customCommandUIControlViewModel);
            SelectAndFocusControl(customCommandUIControlViewModel);
            SaveActiveControl();
        }

        // 对照 WPF: private void ControlTypeComboBox_SelectionChanged(...)
        private void ControlTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_stopComboBoxEvents)
            {
                RefreshControls();
                RefreshDescription();
                SaveActiveControl();
            }
        }

        // 对照 WPF: private void TextBoxTypeComboBox_SelectionChanged(...)
        private void TextBoxTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_stopComboBoxEvents)
            {
                RefreshTextBoxControlsVisibility(GetSelectedTextBoxType());
                RefreshDescription();
                SaveActiveControl();
            }
        }

        // 对照 WPF: private void DialogTypeComboBox_SelectionChanged(...)
        private void DialogTypeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!_stopComboBoxEvents)
            {
                RefreshPathTextBoxControlsVisibility(GetSelectedDialogType());
                RefreshDescription();
                SaveActiveControl();
            }
        }

        // 对照 WPF: private void ControlsListBox_SelectionChanged(...)
        private void ControlsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel customCommandUIControlViewModel))
            {
                return;
            }
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

        // 对照 WPF: private void TextBox_TextChanged(...)
        private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
        {
            SaveActiveControl();
        }

        // 对照 WPF: private void CheckBoxDefaultValueCheckBox_Changed(...)
        private void CheckBoxDefaultValueCheckBox_Changed(object? sender, RoutedEventArgs e)
        {
            SaveActiveControl();
        }

        // 对照 WPF: private void RefreshControls()
        private void RefreshControls()
        {
            if (_controlViewModels == null) return;
            if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel { Control: var control }))
            {
                return;
            }
            switch (GetSelectedControlType())
            {
                case CustomCommandUI.Control.ControlType.TextBox:
                    DropdownControlsContainer.IsVisible = false;
                    CheckBoxControlsContainer.IsVisible = false;
                    TextBoxTypeTextBlock.IsVisible = true;
                    TextBoxTypeComboBox.IsVisible = true;
                    switch (GetSelectedTextBoxType())
                    {
                        case CustomCommandUI.Control.TextBoxType.Generic:
                            {
                                CustomCommandUI.Control.GenericTextBox? genericTextBox = control as CustomCommandUI.Control.GenericTextBox;
                                TitleTextBox.Text = genericTextBox?.Title ?? PreferencesLocalization.Current("Text Box");
                                PathTextBoxControlsContainer.IsVisible = false;
                                GenericTextBoxControlsContainer.IsVisible = true;
                                GenericTextBox.Text = genericTextBox?.Text ?? "";
                                GenericPlaceholderTextBox.Text = genericTextBox?.Placeholder ?? "";
                                break;
                            }
                        case CustomCommandUI.Control.TextBoxType.FilePath:
                            {
                                CustomCommandUI.Control.PathTextBox? pathTextBox = control as CustomCommandUI.Control.PathTextBox;
                                TitleTextBox.Text = pathTextBox?.Title ?? PreferencesLocalization.Current("Path Text Box");
                                GenericTextBoxControlsContainer.IsVisible = false;
                                PathTextBoxControlsContainer.IsVisible = true;
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
                        DropdownControlsContainer.IsVisible = true;
                        CheckBoxControlsContainer.IsVisible = false;
                        GenericTextBoxControlsContainer.IsVisible = false;
                        PathTextBoxControlsContainer.IsVisible = false;
                        TextBoxTypeTextBlock.IsVisible = false;
                        TextBoxTypeComboBox.IsVisible = false;
                        CustomCommandUI.Control.Dropdown? dropdown = control as CustomCommandUI.Control.Dropdown;
                        TitleTextBox.Text = dropdown?.Title ?? PreferencesLocalization.Current("Reference");
                        DropdownFilterTextBox.Text = dropdown?.Filter ?? "";
                        break;
                    }
                case CustomCommandUI.Control.ControlType.CheckBox:
                    {
                        CheckBoxControlsContainer.IsVisible = true;
                        DropdownControlsContainer.IsVisible = false;
                        GenericTextBoxControlsContainer.IsVisible = false;
                        PathTextBoxControlsContainer.IsVisible = false;
                        TextBoxTypeTextBlock.IsVisible = false;
                        TextBoxTypeComboBox.IsVisible = false;
                        CustomCommandUI.Control.CheckBox? checkBox = control as CustomCommandUI.Control.CheckBox;
                        TitleTextBox.Text = checkBox?.Title ?? PreferencesLocalization.Current("Check Box");
                        CheckBoxDefaultValueCheckBox.IsChecked = checkBox?.DefaultValue ?? false;
                        CheckBoxCheckedValueTextBox.Text = checkBox?.CheckedValue ?? "";
                        CheckBoxUncheckedValueTextBox.Text = checkBox?.UncheckedValue ?? "";
                        break;
                    }
            }
        }

        // 对照 WPF: private void RefreshDescription()
        // spike 版：改用 TextBlock.Text（WPF 用 Inlines）
        private void RefreshDescription()
        {
            if (_controlViewModels == null) return;
            if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel item)) return;
            int num = _controlViewModels.IndexOf(item) + 1;
            switch (GetSelectedControlType())
            {
                case CustomCommandUI.Control.ControlType.TextBox:
                    switch (GetSelectedTextBoxType())
                    {
                        case CustomCommandUI.Control.TextBoxType.Generic:
                            DescriptionTextBlock.Text = "Generic Text Box\n\nCan be used to prompt a generic string.\n\nPossible use cases:\n- a name for a new branch\n- a commit message\n- a custom argument for a git command\n\nYou can set the default value and the placeholder text.\n\nThe entered string value can be used in the 'OK' action as `${num}{{text}}` variable.";
                            break;
                        case CustomCommandUI.Control.TextBoxType.FilePath:
                            DescriptionTextBlock.Text = "File Path Text Box\n\nPrompt file path using the system open file dialog.\n\nYou can set the default folder and the file name.\n\nThe selected path can be used in the 'OK' action as `${num}{{path}}` variable.";
                            break;
                    }
                    break;
                case CustomCommandUI.Control.ControlType.Dropdown:
                    DescriptionTextBlock.Text = "Branch Drop Down\n\nAllows to select a branch or a tag (i.e a reference).\n\nThe list can be filtered by a full reference prefix.\nFor example:\n- `refs/heads` - local branches\n- `refs/remotes` - remote branches\n- `refs/tags` - tags\n\nThe selected branch or tag name can be used in the 'OK' action as `${num}{{ref}}` variable.";
                    break;
                case CustomCommandUI.Control.ControlType.CheckBox:
                    DescriptionTextBlock.Text = "Check Box\n\nCan be used to pass optional argument relying on the check box state.\n\n- You can set value for both unchecked and checked states.\n- A value can be empty.\n\nCheck box value can be used in the 'OK' action as `${num}{{value}}` variable.";
                    break;
            }
        }

        // 对照 WPF: private void SaveActiveControl()
        private void SaveActiveControl()
        {
            if (_controlViewModels == null) return;
            if (!(ControlsListBox.SelectedItem is CustomCommandUIControlViewModel customCommandUIControlViewModel)) return;
            if (ControlTypeComboBox.SelectedItem is not ComboBoxItem comboBoxItem) return;
            CustomCommandUI.Control control = customCommandUIControlViewModel.Control;
            if (comboBoxItem == TextBoxComboBoxItem)
            {
                if (TextBoxTypeComboBox.SelectedItem is not ComboBoxItem comboBoxItem2) return;
                if (comboBoxItem2 == GenericComboBoxItem)
                {
                    string text = TitleTextBox.Text ?? "";
                    string text2 = GenericTextBox.Text ?? "";
                    string text3 = GenericPlaceholderTextBox.Text ?? "";
                    control = new CustomCommandUI.Control.GenericTextBox(text, text2, text3);
                }
                else if (comboBoxItem2 == FilePathComboBoxItem)
                {
                    string text4 = TitleTextBox.Text ?? "";
                    CustomCommandUI.Control.PathTextBox.DialogType selectedDialogType = GetSelectedDialogType();
                    string text5 = DefaultDirectoryTextBox.Text ?? "";
                    string text6 = FileNameTextBox.Text ?? "";
                    control = new CustomCommandUI.Control.PathTextBox(text4, selectedDialogType, text5, text6);
                }
            }
            else if (comboBoxItem == DropdownComboBoxItem)
            {
                string text7 = TitleTextBox.Text ?? "";
                string text8 = DropdownFilterTextBox.Text ?? "";
                control = new CustomCommandUI.Control.Dropdown(text7, CustomCommandUI.Control.Dropdown.DropdownType.References, text8);
            }
            else if (comboBoxItem == CheckBoxComboBoxItem)
            {
                string text9 = TitleTextBox.Text ?? "";
                bool valueOrDefault = CheckBoxDefaultValueCheckBox.IsChecked.GetValueOrDefault();
                string text10 = CheckBoxCheckedValueTextBox.Text ?? "";
                string text11 = CheckBoxUncheckedValueTextBox.Text ?? "";
                control = new CustomCommandUI.Control.CheckBox(text9, valueOrDefault, text10, text11);
            }
            customCommandUIControlViewModel.Control = control;
        }

        // 对照 WPF: private CustomCommandUI.Control.ControlType GetSelectedControlType()
        private CustomCommandUI.Control.ControlType GetSelectedControlType()
        {
            if (ControlTypeComboBox.SelectedItem is not ComboBoxItem comboBoxItem)
            {
                throw new InvalidOperationException();
            }
            if (comboBoxItem == TextBoxComboBoxItem) return CustomCommandUI.Control.ControlType.TextBox;
            if (comboBoxItem == DropdownComboBoxItem) return CustomCommandUI.Control.ControlType.Dropdown;
            if (comboBoxItem == CheckBoxComboBoxItem) return CustomCommandUI.Control.ControlType.CheckBox;
            throw new InvalidOperationException();
        }

        // 对照 WPF: private CustomCommandUI.Control.TextBoxType GetSelectedTextBoxType()
        private CustomCommandUI.Control.TextBoxType GetSelectedTextBoxType()
        {
            if (TextBoxTypeComboBox.SelectedItem is not ComboBoxItem comboBoxItem)
            {
                TextBoxTypeComboBox.SelectedItem = GenericComboBoxItem;
                comboBoxItem = GenericComboBoxItem;
            }
            if (comboBoxItem == GenericComboBoxItem) return CustomCommandUI.Control.TextBoxType.Generic;
            if (comboBoxItem == FilePathComboBoxItem) return CustomCommandUI.Control.TextBoxType.FilePath;
            throw new InvalidOperationException();
        }

        // 对照 WPF: private CustomCommandUI.Control.PathTextBox.DialogType GetSelectedDialogType()
        private CustomCommandUI.Control.PathTextBox.DialogType GetSelectedDialogType()
        {
            if (DialogTypeComboBox.SelectedItem is not ComboBoxItem comboBoxItem)
            {
                DialogTypeComboBox.SelectedItem = SaveFileComboBoxItem;
                comboBoxItem = SaveFileComboBoxItem;
            }
            if (comboBoxItem == SaveFileComboBoxItem) return CustomCommandUI.Control.PathTextBox.DialogType.SaveFile;
            if (comboBoxItem == OpenFileComboBoxItem) return CustomCommandUI.Control.PathTextBox.DialogType.OpenFile;
            if (comboBoxItem == OpenDirectoryComboBoxItem) return CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory;
            throw new InvalidOperationException();
        }

        // 对照 WPF: private void RefreshTextBoxControlsVisibility(...)
        private void RefreshTextBoxControlsVisibility(CustomCommandUI.Control.TextBoxType textBoxType)
        {
            switch (textBoxType)
            {
                case CustomCommandUI.Control.TextBoxType.Generic:
                    PathTextBoxControlsContainer.IsVisible = false;
                    GenericTextBoxControlsContainer.IsVisible = true;
                    break;
                case CustomCommandUI.Control.TextBoxType.FilePath:
                    PathTextBoxControlsContainer.IsVisible = true;
                    GenericTextBoxControlsContainer.IsVisible = false;
                    break;
            }
        }

        // 对照 WPF: private void RefreshDialogTypeComboBox(...)
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

        // 对照 WPF: private void RefreshPathTextBoxControlsVisibility(...)
        private void RefreshPathTextBoxControlsVisibility(CustomCommandUI.Control.PathTextBox.DialogType dialogType)
        {
            if (dialogType != CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory)
            {
                FileNameTextBlock.IsVisible = true;
                FileNameTextBox.IsVisible = true;
            }
            else
            {
                FileNameTextBlock.IsVisible = false;
                FileNameTextBox.IsVisible = false;
            }
        }

        // 对照 WPF: private void SelectAndFocusControl(CustomCommandUIControlViewModel control)
        private void SelectAndFocusControl(CustomCommandUIControlViewModel control)
        {
            ControlsListBox.SelectedItem = control;
            ControlsListBox.Focus();
        }
    }
}
