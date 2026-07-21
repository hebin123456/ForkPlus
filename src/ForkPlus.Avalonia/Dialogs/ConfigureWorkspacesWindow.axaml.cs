using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia 版 ConfigureWorkspacesWindow（对照 WPF ConfigureWorkspacesWindow.xaml 80 行 + .cs 156 行）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/ConfigureWorkspacesWindow.xaml.cs：
    //   - public partial class ConfigureWorkspacesWindow : ForkPlusDialogWindow
    //   - 字段: ObservableCollection<WorkspaceViewModel> _workspaceViewModels
    //   - 构造函数：
    //     * _workspaceViewModels = new ObservableCollection(All.Map(WorkspaceViewModel))
    //     * DialogTitle = PreferencesLocalization.Current("Workspaces")
    //     * DialogDescription = PreferencesLocalization.Current("Use '/' as path separator to create folders")
    //     * ShowSubmitButton = false / CancelButtonTitle = "Close"
    //     * WorkspacesListBox.ItemsSource = _workspaceViewModels / SelectedIndex = 0
    //     * UpdateDeleteButtonState / ShowWorkspaceInTitleCheckBox.IsChecked
    //   - OnClosing：_workspaceViewModels.Count > 1 时持久化到 Workspaces.Update + Save
    //     + MainWindow.Instance.TabManager.RestoreSession() / Toolbar.RefreshWorkspacesButton() / RefreshTitle()
    //   - WorkspacesListBox_ContextMenuOpening → 动态构造 Add/Rename/Delete 菜单
    //   - WorkspacesListBox_KeyDown(F2) → IsInEditMode = true
    //   - AddWorkspaceButton_Click → AddNewWorkspace()
    //   - RemoveWorkspaceButton_Click → RemoveWorkspace(selected)
    //   - ShowWorkspaceInTitleCheckBox_Changed → Workspaces.ShowInTitle 更新
    //   - SelectAndFocusWorkspace / UpdateDeleteButtonState
    //
    // Avalonia 版差异：
    //   1. spike 模式：构造函数 SetFooter/Footer 注入
    //   2. OnClosing(CancelEventArgs) → Closed 事件（Avalonia 11 等价，不可取消）
    //   3. ContextMenu / F2 rename → spike 简化（仅保留 Add/Remove 按钮）
    //   4. EditableTextBlock → TextBlock 显示 DisplayName（spike 不支持 inline 编辑）
    //   5. MainWindow.Instance.* → spike 不接入（无 MainWindow 引用，留 Phase 5 接入）
    //   6. WorkspaceViewModel → 嵌套类（spike 不引入 WPF 工程 WorkspaceViewModel.cs）
    //   7. MessageBoxWindow → 已迁移，使用 await ShowDialog<bool?>(this)
    //   8. CheckBox Checked/Unchecked 事件 → IsCheckedChanged 事件
    public partial class ConfigureWorkspacesWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        private readonly ObservableCollection<WorkspaceViewModel> _workspaceViewModels;

        public ConfigureWorkspacesWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: Workspace[] all = ForkPlusSettings.Default.Workspaces.All
            Workspace[] all = ForkPlusSettings.Default.Workspaces?.All ?? Array.Empty<Workspace>();
            _workspaceViewModels = new ObservableCollection<WorkspaceViewModel>(
                all.Select((Workspace x) => new WorkspaceViewModel(x)));

            // 对照 WPF: DialogTitle / DialogDescription / ShowSubmitButton / CancelButtonTitle
            DialogTitle = Current("Workspaces");
            DialogDescription = Current("Use '/' as path separator to create folders");
            ShowSubmitButton = false;
            CancelButtonTitle = Current("Close");
            Title = Current("Workspaces");

            // 对照 WPF: WorkspacesListBox.ItemsSource = _workspaceViewModels
            WorkspacesListBox.ItemsSource = _workspaceViewModels;
            if (_workspaceViewModels.Count > 0)
            {
                WorkspacesListBox.SelectedIndex = 0;
            }
            UpdateDeleteButtonState();
            // 对照 WPF: ShowWorkspaceInTitleCheckBox.IsChecked = ForkPlusSettings.Default.Workspaces.ShowInTitle
            ShowWorkspaceInTitleCheckBox.IsChecked = ForkPlusSettings.Default.Workspaces?.ShowInTitle ?? false;

            // 对照 WPF: OnClosing → 持久化（Avalonia 用 Closed 事件）
            Closed += ConfigureWorkspacesWindow_Closed;
        }

        // 对照 WPF: protected override void OnClosing(CancelEventArgs e)
        // Avalonia: Closed 事件（不可取消，但 WPF 版只在 Count > 1 时保存）
        private void ConfigureWorkspacesWindow_Closed(object? sender, EventArgs e)
        {
            if (_workspaceViewModels.Count > 1)
            {
                Workspace[] array = _workspaceViewModels
                    .Select((WorkspaceViewModel x) => x.CreateWorkspace())
                    .ToArray();
                Workspace activeWorkspace = array.FirstOrDefault(
                    (Workspace x) => x.Name == ForkPlusSettings.Default.Workspaces.ActiveWorkspace?.Name)
                    ?? array.FirstOrDefault();
                bool showInTitle = ShowWorkspaceInTitleCheckBox.IsChecked.GetValueOrDefault();
                ForkPlusSettings.Default.Workspaces.Update(array, activeWorkspace, showInTitle);
                ForkPlusSettings.Default.Save();
                // 对照 WPF: MainWindow.Instance.TabManager.RestoreSession() / Toolbar.RefreshWorkspacesButton() / RefreshTitle()
                // spike 版不接入 MainWindow，留 Phase 5 接入
            }
        }

        // 对照 WPF: AddWorkspaceButton_Click → AddNewWorkspace()
        private void AddWorkspaceButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewWorkspace();
        }

        // 对照 WPF: RemoveWorkspaceButton_Click → RemoveWorkspace(selected)
        private void RemoveWorkspaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkspacesListBox.SelectedItem is WorkspaceViewModel workspace)
            {
                RemoveWorkspace(workspace);
            }
        }

        // 对照 WPF: AddNewWorkspace
        private void AddNewWorkspace()
        {
            var workspaceViewModel = new WorkspaceViewModel();
            _workspaceViewModels.Add(workspaceViewModel);
            UpdateDeleteButtonState();
            SelectAndFocusWorkspace(workspaceViewModel);
            // 对照 WPF: workspaceViewModel.IsInEditMode = true
            // spike 版不支持 inline 编辑，新增工作区后用户需要重启对话框或通过其他方式重命名
        }

        // 对照 WPF: RemoveWorkspace
        private async void RemoveWorkspace(WorkspaceViewModel workspace)
        {
            // 对照 WPF: new MessageBoxWindow("Do you want to delete the workspace '" + workspace.Name + "'?", ...)
            var msgBox = new MessageBoxWindow(
                "Delete workspace",
                "Do you want to delete the workspace '" + workspace.Name + "'?",
                "Delete", "Cancel", showCancelButton: true, 500.0);
            bool? result = await msgBox.ShowDialog<bool?>(this);
            if (result != true)
            {
                return;
            }
            int num = _workspaceViewModels.IndexOf(workspace);
            _workspaceViewModels.Remove(workspace);
            UpdateDeleteButtonState();
            WorkspaceViewModel next = (num < _workspaceViewModels.Count) ? _workspaceViewModels[num] : _workspaceViewModels.FirstOrDefault();
            if (next != null)
            {
                SelectAndFocusWorkspace(next);
            }
        }

        // 对照 WPF: UpdateDeleteButtonState
        private void UpdateDeleteButtonState()
        {
            RemoveWorkspaceButton.IsEnabled = _workspaceViewModels.Count > 2;
        }

        // 对照 WPF: SelectAndFocusWorkspace
        private void SelectAndFocusWorkspace(WorkspaceViewModel workspaceViewModel)
        {
            if (workspaceViewModel == null) return;
            WorkspacesListBox.SelectedItem = workspaceViewModel;
            WorkspacesListBox.Focus();
        }

        // 对照 WPF: ShowWorkspaceInTitleCheckBox_Changed
        private void ShowWorkspaceInTitleCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (ForkPlusSettings.Default.Workspaces != null)
            {
                ForkPlusSettings.Default.Workspaces.ShowInTitle = ShowWorkspaceInTitleCheckBox.IsChecked.GetValueOrDefault();
            }
        }

        // PreferencesLocalization.Current(text) → ServiceLocator.Localization.Current(text)
        private static string Current(string text)
        {
            var localization = ServiceLocator.Localization;
            return localization != null ? localization.Current(text) : text;
        }

        // spike 版：嵌套 WorkspaceViewModel（对照 WPF src/ForkPlus/UI/Dialogs/WorkspaceViewModel.cs）。
        // 不引入 WPF 工程的 WorkspaceViewModel.cs，避免依赖 System.Windows.*。
        // 逻辑与 WPF 版一致：Name / DisplayName / IsInEditMode(INPC) / CreateWorkspace()。
        // spike 简化：不实现 IsInEditMode（spike 版不支持 inline 编辑），仅保留 Name/DisplayName/CreateWorkspace。
        public sealed class WorkspaceViewModel : System.ComponentModel.INotifyPropertyChanged
        {
            private const string DefaultName = "New Workspace";

            private readonly Workspace _workspace;
            private string _name;

            public string Name
            {
                get { return _name; }
                set
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        _name = _workspace?.Name ?? DefaultName;
                    }
                    else
                    {
                        _name = value;
                    }
                    NotifyPropertyChanged(nameof(Name));
                    NotifyPropertyChanged(nameof(DisplayName));
                }
            }

            public string DisplayName
            {
                get { return _name; }
                set { Name = value; }
            }

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

            public WorkspaceViewModel(string name = DefaultName)
            {
                Name = name;
            }

            public WorkspaceViewModel(Workspace workspace)
            {
                _workspace = workspace;
                Name = workspace.Name;
            }

            public Workspace CreateWorkspace()
            {
                string[] repositories = _workspace?.Repositories ?? Array.Empty<string>();
                string activeRepository = _workspace?.ActiveRepository ?? repositories.FirstOrDefault();
                return new Workspace(Name, repositories, activeRepository);
            }

            private void NotifyPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
