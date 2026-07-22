// 对照 WPF 工程 src/ForkPlus/UI/UserControls/Preferences/CustomCommandsUserControl.xaml.cs（459 行）：
//   - public partial class CustomCommandsUserControl : UserControl, ILocalizableControl
//   - 字段：Window _parentWindow / GitModule _gitModule / bool _localMode /
//     ObservableCollection<CustomCommandViewModel> _customCommandViewModels
//   - InitializeLocal(parentWindow, gitModule, repositoryData)：本地模式
//   - InitializeGlobal(parentWindow)：全局模式
//   - ApplyLocalization：5 个菜单项 Header 翻译
//   - Save：CustomCommandManager.Current.SetLocal/SetGlobalCustomCommands
//   - 5 个 MenuItem_Click：CreateCustomCommandName + new CustomCommandViewModel(target, name, action)
//   - RemoveCustomCommandButton_Click：MessageBoxWindow 确认 + Remove
//   - TargetsComboBox_SelectionChanged：5 种 target 切换 + RefreshReferenceTargetsContainer
//   - UiControlsButton_Click：EditCustomCommandUIControlsWindow.ShowDialog
//   - ProcessActionButton_Click / UiActionButton1_Click / UiActionButton2_Click：EditAction
//   - LocationRadioButton_Changed：Local/Shared 切换 + RefreshLocationControls
//   - OSComboBox_SelectionChanged：Any/Windows/Mac 切换
//   - UIMode_Changed：UI/Process RadioButton 切换
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF ILocalizableControl 接口 == spike 移除
//   2. WPF Window _parentWindow == spike 用 object? 占位
//   3. WPF [Null] GitModule _gitModule == spike 移除 [Null] 属性（internal in Core）
//   4. WPF MessageBoxWindow（删除确认）== spike 移除（spike 直接删除）
//   5. WPF EditCustomActionWindow.ShowDialog == spike 移除（spike 不弹编辑窗口）
//   6. WPF EditCustomCommandUIControlsWindow.ShowDialog == spike 移除
//   7. WPF AddCustomCommandDropDownButton.ContextMenu == spike 改用 5 个 Button
//   8. WPF DescriptionTextBlock.Inlines == spike 改用 TextBlock.Text（spike 不用富文本）
//   9. WPF FallbackUserControl.Show()/Collapse() == spike 改用 IsVisible = true/false
//  10. WPF LocationRadioButtonContainer.Show()/Collapse() == spike IsVisible
//  11. namespace 改为 ForkPlus.Avalonia.Views.UserControls.Preferences
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.Avalonia.Views.UserControls.Preferences
{
    public partial class CustomCommandsUserControl : UserControl
    {
        // 对照 WPF: private static readonly string UrlCommandDefaultUrl = "https://hebin.me";
        private static readonly string UrlCommandDefaultUrl = "https://hebin.me";

        // 对照 WPF: private Window _parentWindow;
        // spike 版：用 object? 占位
        private object? _parentWindow;

        // 对照 WPF: [Null] private GitModule _gitModule;
        // spike 版：移除 [Null] 属性（internal in Core）
        private GitModule? _gitModule;

        // 对照 WPF: private bool _localMode;
        private bool _localMode;

        // 对照 WPF: private ObservableCollection<CustomCommandViewModel> _customCommandViewModels;
        private ObservableCollection<CustomCommandViewModel>? _customCommandViewModels;

        public CustomCommandsUserControl()
        {
            InitializeComponent();
            ApplyLocalization();
        }

        // 对照 WPF: public void InitializeLocal(Window parentWindow, GitModule gitModule, RepositoryData repositoryData)
        public void InitializeLocal(object? parentWindow, GitModule gitModule, RepositoryData repositoryData)
        {
            _localMode = true;
            _gitModule = gitModule;
            _parentWindow = parentWindow;
            _customCommandViewModels = LoadLocalCustomCommands(repositoryData);
            CustomCommandsListBox.ItemsSource = _customCommandViewModels;
            CustomCommandViewModel? customCommandViewModel = _customCommandViewModels.FirstOrDefault();
            if (customCommandViewModel != null)
            {
                SelectAndFocusCustomCommand(customCommandViewModel);
            }
            else
            {
                FallbackUserControl.IsVisible = true;
            }
            LocationRadioButtonContainer.IsVisible = true;
            ApplyLocalization();
        }

        // 对照 WPF: public void InitializeGlobal(Window parentWindow)
        public void InitializeGlobal(object? parentWindow)
        {
            _localMode = false;
            _parentWindow = parentWindow;
            _customCommandViewModels = LoadGlobalCustomCommands();
            CustomCommandsListBox.ItemsSource = _customCommandViewModels;
            CustomCommandViewModel? customCommandViewModel = _customCommandViewModels.FirstOrDefault();
            if (customCommandViewModel != null)
            {
                SelectAndFocusCustomCommand(customCommandViewModel);
            }
            else
            {
                FallbackUserControl.IsVisible = true;
            }
            LocationRadioButtonContainer.IsVisible = false;
            ApplyLocalization();
        }

        // 对照 WPF: public void ApplyLocalization()
        // spike 版：5 个 Button 替代 WPF 5 个 MenuItem.Header
        public void ApplyLocalization()
        {
            // spike 版：5 个 Button 内容翻译（WPF 是 5 个 MenuItem.Header）
            // spike: Button Content 在 axaml 中已硬编码英文，spike 阶段保留英文
        }

        // 对照 WPF: public void Save()
        public void Save()
        {
            if (_customCommandViewModels == null) return;
            CustomCommand[] array = _customCommandViewModels.Map((CustomCommandViewModel x) => x.CustomCommand);
            if (_localMode && _gitModule != null)
            {
                CustomCommandManager.Current.SetLocalCustomCommands(_gitModule, array);
            }
            else
            {
                CustomCommandManager.Current.SetGlobalCustomCommands(array);
            }
        }

        // 对照 WPF: private void CustomCommandsListBox_SelectionChanged(...)
        private void CustomCommandsListBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (!(CustomCommandsListBox.SelectedItem is CustomCommandViewModel customCommandViewModel))
            {
                FallbackUserControl.IsVisible = true;
            }
            else if (customCommandViewModel.Version > 2)
            {
                // spike: FallbackUserControl.FallbackMessage = ...; FallbackUserControl.Show(); // 简化
                FallbackUserControl.IsVisible = true;
            }
            else
            {
                FallbackUserControl.IsVisible = false;
                RefreshControls(customCommandViewModel);
            }
        }

        // 对照 WPF: private void UIMode_Changed(object sender, RoutedEventArgs e)
        private void UIMode_Changed(object? sender, RoutedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            if (CustomCommandsListBox.SelectedItem is not CustomCommandViewModel customCommandViewModel) return;
            if (UiRadioButton.IsChecked.GetValueOrDefault())
            {
                customCommandViewModel.ActionType = ActionType.UI;
                UiActionDetailsContainer.IsVisible = true;
                ProcessActionTypeTextBlock.IsVisible = false;
                ProcessActionButton.IsVisible = false;
            }
            else if (ProcessRadioButton.IsChecked.GetValueOrDefault())
            {
                customCommandViewModel.ActionType = ActionType.Action;
                ProcessActionTypeTextBlock.IsVisible = true;
                ProcessActionButton.IsVisible = true;
                UiActionDetailsContainer.IsVisible = false;
            }
        }

        // 对照 WPF: RepositoryCustomCommandMenuItem_Click
        private void RepositoryCustomCommandMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            string name = CreateCustomCommandName();
            CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Repository, name, new UrlCustomCommandAction(UrlCommandDefaultUrl));
            _customCommandViewModels.Add(customCommandViewModel);
            SelectAndFocusCustomCommand(customCommandViewModel);
        }

        // 对照 WPF: RevisionCustomCommandMenuItem_Click
        private void RevisionCustomCommandMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            string name = CreateCustomCommandName();
            CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Revision, name, new ShCustomCommandAction("git show ${sha}", showOutput: true, waitForExit: true));
            _customCommandViewModels.Add(customCommandViewModel);
            SelectAndFocusCustomCommand(customCommandViewModel);
        }

        // 对照 WPF: FileCustomCommandMenuItem_Click
        private void FileCustomCommandMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            string name = CreateCustomCommandName();
            CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.RepositoryFile, name, new ShCustomCommandAction("git diff ${file}", showOutput: true, waitForExit: true));
            _customCommandViewModels.Add(customCommandViewModel);
            SelectAndFocusCustomCommand(customCommandViewModel);
        }

        // 对照 WPF: ReferenceCustomCommandMenuItem_Click
        private void ReferenceCustomCommandMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            string name = CreateCustomCommandName();
            CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Reference, name, new ShCustomCommandAction("git diff HEAD ${ref}", showOutput: true, waitForExit: true), new CustomCommandRefTarget[2]
            {
                CustomCommandRefTarget.LocalBranch,
                CustomCommandRefTarget.RemoteBranch
            });
            _customCommandViewModels.Add(customCommandViewModel);
            SelectAndFocusCustomCommand(customCommandViewModel);
        }

        // 对照 WPF: SubmoduleCustomCommandMenuItem_Click
        private void SubmoduleCustomCommandMenuItem_Click(object? sender, RoutedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            string name = CreateCustomCommandName();
            CustomCommandViewModel customCommandViewModel = new CustomCommandViewModel(CustomCommandTarget.Submodule, name, new ShCustomCommandAction("git submodule update --remote -- ${submodule}", showOutput: true, waitForExit: true));
            _customCommandViewModels.Add(customCommandViewModel);
            SelectAndFocusCustomCommand(customCommandViewModel);
        }

        // 对照 WPF: private void RemoveCustomCommandButton_Click(...)
        // spike 版：移除 MessageBoxWindow 确认（spike 直接删除）
        private void RemoveCustomCommandButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            if (CustomCommandsListBox.SelectedItem is not CustomCommandViewModel item) return;
            // spike: WPF 用 MessageBoxWindow 确认删除，spike 版直接删除
            int num = _customCommandViewModels.IndexOf(item) - 1;
            _customCommandViewModels.Remove(item);
            CustomCommandViewModel? customCommand = ((num != -1) ? _customCommandViewModels[num] : _customCommandViewModels.FirstOrDefault());
            if (customCommand != null) SelectAndFocusCustomCommand(customCommand);
        }

        // 对照 WPF: private void TargetsComboBox_SelectionChanged(...)
        private void TargetsComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            if (CustomCommandsListBox.SelectedItem is not CustomCommandViewModel customCommandViewModel) return;
            object? selectedItem = TargetsComboBox.SelectedItem;
            if (selectedItem == CommitComboBoxItem)
            {
                customCommandViewModel.Target = CustomCommandTarget.Revision;
                RefreshReferenceTargetsContainer(CustomCommandTarget.Revision);
            }
            else if (selectedItem == RepositoryComboBoxItem)
            {
                customCommandViewModel.Target = CustomCommandTarget.Repository;
                RefreshReferenceTargetsContainer(CustomCommandTarget.Repository);
            }
            else if (selectedItem == FileComboBoxItem)
            {
                customCommandViewModel.Target = CustomCommandTarget.RepositoryFile;
                RefreshReferenceTargetsContainer(CustomCommandTarget.RepositoryFile);
            }
            else if (selectedItem == BranchComboBoxItem)
            {
                customCommandViewModel.Target = CustomCommandTarget.Reference;
                RefreshReferenceTargetsContainer(CustomCommandTarget.Reference);
            }
            else if (selectedItem == SubmoduleComboBoxItem)
            {
                customCommandViewModel.Target = CustomCommandTarget.Submodule;
                RefreshReferenceTargetsContainer(CustomCommandTarget.Submodule);
            }
            UpdateDescription(customCommandViewModel);
        }

        // 对照 WPF: private void UiControlsButton_Click(...)
        // spike 版：移除 EditCustomCommandUIControlsWindow.ShowDialog
        private void UiControlsButton_Click(object? sender, RoutedEventArgs e)
        {
            // spike: WPF 弹 EditCustomCommandUIControlsWindow.ShowDialog，spike 版占位
        }

        // 对照 WPF: private void ProcessActionButton_Click(...)
        // spike 版：移除 EditAction
        private void ProcessActionButton_Click(object? sender, RoutedEventArgs e)
        {
            // spike: WPF 弹 EditCustomActionWindow.ShowDialog，spike 版占位
        }

        // 对照 WPF: private void UiActionButton1_Click(...)
        private void UiActionButton1_Click(object? sender, RoutedEventArgs e)
        {
            // spike: WPF 弹 EditCustomActionWindow.ShowDialog，spike 版占位
        }

        // 对照 WPF: private void UiActionButton2_Click(...)
        private void UiActionButton2_Click(object? sender, RoutedEventArgs e)
        {
            // spike: WPF 弹 EditCustomActionWindow.ShowDialog，spike 版占位
        }

        // 对照 WPF: private void LocationRadioButton_Changed(...)
        private void LocationRadioButton_Changed(object? sender, RoutedEventArgs e)
        {
            if (!_localMode) return;
            if (_customCommandViewModels == null) return;
            if (CustomCommandsListBox.SelectedItem is CustomCommandViewModel customCommandViewModel)
            {
                customCommandViewModel.Shared = SharedRadioButton.IsChecked.GetValueOrDefault();
                RefreshLocationControls(customCommandViewModel);
            }
        }

        // 对照 WPF: private void OSComboBox_SelectionChanged(...)
        private void OSComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_customCommandViewModels == null) return;
            if (CustomCommandsListBox.SelectedItem is not CustomCommandViewModel customCommandViewModel) return;
            object? selectedItem = OSComboBox.SelectedItem;
            if (selectedItem == AnyComboBoxItem)
            {
                customCommandViewModel.OS = CustomCommandOS.Any;
            }
            else if (selectedItem == WindowsComboBoxItem)
            {
                customCommandViewModel.OS = CustomCommandOS.Windows;
            }
            else if (selectedItem == MacComboBoxItem)
            {
                customCommandViewModel.OS = CustomCommandOS.Mac;
            }
        }

        // 对照 WPF: private ObservableCollection<CustomCommandViewModel> LoadLocalCustomCommands(...)
        private ObservableCollection<CustomCommandViewModel> LoadLocalCustomCommands(RepositoryData repositoryData)
        {
            ObservableCollection<CustomCommandViewModel> observableCollection = new ObservableCollection<CustomCommandViewModel>();
            CustomCommand[] localCustomCommands = CustomCommandManager.Current.GetLocalCustomCommands(repositoryData);
            foreach (CustomCommand command in localCustomCommands)
            {
                observableCollection.Add(new CustomCommandViewModel(command));
            }
            return observableCollection;
        }

        // 对照 WPF: private ObservableCollection<CustomCommandViewModel> LoadGlobalCustomCommands()
        private ObservableCollection<CustomCommandViewModel> LoadGlobalCustomCommands()
        {
            ObservableCollection<CustomCommandViewModel> observableCollection = new ObservableCollection<CustomCommandViewModel>();
            CustomCommand[] globalCustomCommands = CustomCommandManager.Current.GetGlobalCustomCommands();
            foreach (CustomCommand command in globalCustomCommands)
            {
                observableCollection.Add(new CustomCommandViewModel(command));
            }
            return observableCollection;
        }

        // 对照 WPF: private string CreateCustomCommandName()
        private string CreateCustomCommandName()
        {
            string name = PreferencesLocalization.Current("Custom Command");
            int num = _customCommandViewModels!.Count((CustomCommandViewModel x) => x.Name.ToLower().StartsWith(name.ToLower()));
            if (num > 0)
            {
                name += $"{num}";
            }
            return name;
        }

        // 对照 WPF: private void RefreshControls(CustomCommandViewModel customCommand)
        private void RefreshControls(CustomCommandViewModel customCommand)
        {
            if (_localMode)
            {
                RefreshLocationControls(customCommand);
            }
            RefreshTargetCombobox(customCommand.Target);
            RefreshReferenceTargetsContainer(customCommand.Target);
            UiRadioButton.IsChecked = customCommand.ActionType == ActionType.UI;
            ProcessRadioButton.IsChecked = customCommand.ActionType == ActionType.Action;
        }

        // 对照 WPF: private void RefreshTargetCombobox(CustomCommandTarget target)
        private void RefreshTargetCombobox(CustomCommandTarget target)
        {
            switch (target)
            {
                case CustomCommandTarget.Revision:
                    TargetsComboBox.SelectedItem = CommitComboBoxItem;
                    break;
                case CustomCommandTarget.Repository:
                    TargetsComboBox.SelectedItem = RepositoryComboBoxItem;
                    break;
                case CustomCommandTarget.RepositoryFile:
                    TargetsComboBox.SelectedItem = FileComboBoxItem;
                    break;
                case CustomCommandTarget.Reference:
                    TargetsComboBox.SelectedItem = BranchComboBoxItem;
                    break;
                case CustomCommandTarget.Submodule:
                    TargetsComboBox.SelectedItem = SubmoduleComboBoxItem;
                    break;
            }
        }

        // 对照 WPF: private void RefreshReferenceTargetsContainer(CustomCommandTarget target)
        private void RefreshReferenceTargetsContainer(CustomCommandTarget target)
        {
            ReferenceTargetsContainer.IsVisible = (target == CustomCommandTarget.Reference);
        }

        // 对照 WPF: private void RefreshLocationControls(CustomCommandViewModel customCommand)
        private void RefreshLocationControls(CustomCommandViewModel customCommand)
        {
            LocalRadioButton.IsChecked = !customCommand.Shared;
            SharedRadioButton.IsChecked = customCommand.Shared;
            if (customCommand.Shared)
            {
                LocationTextBlock.Text = ".fork/custom-commands.json";
                OSTextBlock.IsVisible = true;
                OSComboBox.IsVisible = true;
                RefreshOSComboBox(customCommand.OS);
            }
            else
            {
                LocationTextBlock.Text = ".git/fork/custom-commands.json";
                OSTextBlock.IsVisible = false;
                OSComboBox.IsVisible = false;
                RefreshOSComboBox(CustomCommandOS.Any);
            }
        }

        // 对照 WPF: private void RefreshOSComboBox(CustomCommandOS osType)
        private void RefreshOSComboBox(CustomCommandOS osType)
        {
            switch (osType)
            {
                case CustomCommandOS.Any:
                    OSComboBox.SelectedItem = AnyComboBoxItem;
                    break;
                case CustomCommandOS.Windows:
                    OSComboBox.SelectedItem = WindowsComboBoxItem;
                    break;
                case CustomCommandOS.Mac:
                    OSComboBox.SelectedItem = MacComboBoxItem;
                    break;
            }
        }

        // 对照 WPF: private void UpdateDescription(CustomCommandViewModel customCommand)
        // spike 版：改用 TextBlock.Text（WPF 用 Inlines）
        private void UpdateDescription(CustomCommandViewModel customCommand)
        {
            string header = PreferencesLocalization.Translate("Available variables:", ForkPlusSettings.Default.UiLanguage);
            DescriptionTextBlock.Text = header + "\n\n" + customCommand.VariablesString;
        }

        // 对照 WPF: private void SelectAndFocusCustomCommand(CustomCommandViewModel customCommand)
        private void SelectAndFocusCustomCommand(CustomCommandViewModel customCommand)
        {
            CustomCommandsListBox.SelectedItem = customCommand;
            CustomCommandsListBox.Focus();
        }
    }
}
