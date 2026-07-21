using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus;
using ForkPlus.Settings;
using ToolDefinitionLocal = ForkPlus.ToolDefinition;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 ExternalToolsUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/ExternalToolsUserControl.xaml.cs（193 行）：
    //   - Initialize(parentWindow, externalTools, toolDefinitions, argumentsHint)：构造 ViewModel 列表
    //   - BrowseButton_Click：OpenDialog.SelectFile 选 .exe/.cmd 文件
    //   - AddToolButton_Click：构造 ExternalToolViewModel("Custom") + 加到列表
    //   - RemoveToolButton_Click：MessageBoxWindow 确认 + 移除选中
    //   - ToolsListBox_SelectionChanged：null 选中时显示 Fallback
    //   - ToolsListBox_ContextMenuOpening：动态构造右键菜单 Primary/Visible/Reset to default
    //   - GetContextMenu：3 个 MenuItem（Primary 设单选 / Visible 切换 / Reset 调用 ResetToDefault）
    //   - Result：_toolViewModels.Map(x => x.ExternalTool)
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF ExternalToolViewModel : INotifyPropertyChanged → spike POCO（重设 ItemsSource 触发刷新）
    //   - WPF ForkPlusDialogWindow → object 占位
    //   - WPF OpenDialog.SelectFile → onBrowse 回调注入
    //   - WPF MessageBoxWindow (删除确认) → spike 直接删除（无确认对话框）
    //   - WPF ContextMenu 右键菜单 → spike 顶部三个按钮 Primary/Visible/Reset
    //   - WPF ExternalToolManager.GetPredefinedToolPath → spike 不调用（ResetToDefault 留空实现）
    //   - WPF PreferencesLocalization → ServiceLocator.Localization
    public partial class ExternalToolsUserControl : UserControl
    {
        // ===== ExternalToolViewModel POCO（对照 WPF ExternalToolViewModel.cs：INotifyPropertyChanged）=====
        // spike 简化为 POCO：双向绑定通过手动调用 RefreshSelected() 触发
        public class ExternalToolViewModel
        {
            public ToolType Type { get; set; }
            public string Name { get; set; }
            public string Path { get; set; }
            public string Arguments { get; set; }
            public bool IsPrimary { get; set; }
            public bool IsVisible { get; set; } = true;
            public bool IsPredefined { get; set; }
            public bool IsAvailable => !string.IsNullOrEmpty(Path);
            public string PrimaryLabel => "primary";

            // 对照 WPF: public ExternalTool ExternalTool
            // ExternalTool 构造函数（ForkPlus.Core）：
            //   (ToolType type, string name, string path, bool pathOverridden,
            //    string[] arguments, bool argumentsOverridden, bool isPredefined,
            //    bool isPrimary, bool isVisible)
            // spike 版：pathOverridden/argumentsOverridden 默认 false（spike 不追踪 override 状态）
            public ExternalTool ExternalTool => new ExternalTool(
                Type, Name, Path, false,
                Arguments?.Split(' ') ?? Array.Empty<string>(),
                false, IsPredefined, IsPrimary, IsVisible);

            public ExternalToolViewModel(ExternalTool tool)
            {
                Type = tool.Type;
                Name = tool.Name;
                Path = tool.Path;
                // ExternalTool.Arguments 是 string[]，ViewModel 用 string 展示（空格分隔）
                Arguments = string.Join(" ", tool.Arguments);
                IsPrimary = tool.IsPrimary;
                IsVisible = tool.IsVisible;
                IsPredefined = Type != ToolType.Custom;
            }

            public ExternalToolViewModel(string name)
            {
                Type = ToolType.Custom;
                Name = name;
                Path = string.Empty;
                Arguments = string.Empty;
                IsPrimary = false;
                IsVisible = true;
                IsPredefined = false;
            }
        }

        // ===== 私有字段（对照 WPF）=====
        private ToolDefinitionLocal[] _toolDefinitions;
        private object _parentWindow;
        private ObservableCollection<ExternalToolViewModel> _toolViewModels;
        private Func<string, string> _onBrowse;

        // ===== 公共属性（对照 WPF: public ExternalTool[] Result）=====
        public ExternalTool[] Result => _toolViewModels?
            .Select(x => x.ExternalTool)
            .ToArray() ?? Array.Empty<ExternalTool>();

        // ===== 构造函数（对照 WPF）=====
        public ExternalToolsUserControl()
        {
            InitializeComponent();
        }

        // ===== Initialize（对照 WPF: Initialize(parentWindow, externalTools, toolDefinitions, argumentsHint)）=====
        // 对照 WPF:
        //   _parentWindow = parentWindow;
        //   _toolDefinitions = toolDefinitions;
        //   _toolViewModels = CreateExternalToolViewModels(externalTools);
        //   ToolsListBox.ItemsSource = _toolViewModels;
        //   ArgumentsHintTextBlock.Tag = argumentsHint;
        //   ToolsListBox.SelectedItem = _toolViewModels.FirstItem() ?? ToolsFallback.Show();
        // spike 版:
        //   - parentWindow → object 占位（spike 不依赖具体 Window 类型）
        //   - argumentsHint → spike 简化（不显示）
        //   - onBrowse 回调注入（替代 OpenDialog.SelectFile）
        public void Initialize(object parentWindow, ExternalTool[] externalTools,
                               ToolDefinitionLocal[] toolDefinitions,
                               string argumentsHint = null,
                               Func<string, string> onBrowse = null)
        {
            _parentWindow = parentWindow;
            _toolDefinitions = toolDefinitions;
            _onBrowse = onBrowse;
            _toolViewModels = CreateExternalToolViewModels(externalTools ?? Array.Empty<ExternalTool>());
            ToolsListBox.ItemsSource = _toolViewModels;

            ExternalToolViewModel first = _toolViewModels.FirstOrDefault();
            if (first != null)
            {
                ToolsListBox.SelectedItem = first;
            }
            else
            {
                ShowFallback();
            }
        }

        // ===== BrowseButton_Click（对照 WPF）=====
        // 对照 WPF: private void BrowseButton_Click(object sender, RoutedEventArgs e)
        //   WPF: 用 OpenDialog.SelectFile(_parentWindow, "Select external tool", initialDirectory, "Applications", "*.exe; *.cmd", out var filePath);
        //         ToolPathTextBox.Text = filePath;
        // spike 版: 调用注入的 _onBrowse 回调
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            string initialPath = ToolPathTextBox?.Text ?? string.Empty;
            string selectedPath = _onBrowse?.Invoke(initialPath);
            if (!string.IsNullOrEmpty(selectedPath) && ToolPathTextBox != null)
            {
                ToolPathTextBox.Text = selectedPath;
                // 同步到 ViewModel（spike 双向绑定需要手动触发）
                if (ToolsListBox.SelectedItem is ExternalToolViewModel vm)
                {
                    vm.Path = selectedPath;
                }
            }
        }

        // ===== AddToolButton_Click（对照 WPF）=====
        // 对照 WPF: private void AddToolButton_Click(object sender, RoutedEventArgs e)
        //   WPF: name = "Custom"（带数字后缀避免重名）
        //         new ExternalToolViewModel(name) 加入列表 + 选中 + 滚动到视图
        // spike 版: 同样逻辑（spike 不调用 ScrollIntoView）
        private void AddToolButton_Click(object sender, RoutedEventArgs e)
        {
            if (_toolViewModels == null) return;
            string name = "Custom";
            int count = _toolViewModels.Count(x => x.Name?.ToLowerInvariant().StartsWith(name.ToLowerInvariant()) == true);
            if (count > 0)
            {
                name += $"{count}";
            }
            var vm = new ExternalToolViewModel(name);
            _toolViewModels.Add(vm);
            ToolsListBox.SelectedItem = vm;
        }

        // ===== RemoveToolButton_Click（对照 WPF）=====
        // 对照 WPF: private void RemoveToolButton_Click(object sender, RoutedEventArgs e)
        //   WPF: MessageBoxWindow 确认 → _toolViewModels.Remove(item)
        // spike 版: 直接移除（无确认对话框）
        private void RemoveToolButton_Click(object sender, RoutedEventArgs e)
        {
            if (_toolViewModels == null) return;
            if (ToolsListBox.SelectedItem is ExternalToolViewModel item)
            {
                int num = _toolViewModels.IndexOf(item) - 1;
                _toolViewModels.Remove(item);
                ExternalToolViewModel selected = (num != -1) ? _toolViewModels[num] : _toolViewModels.FirstOrDefault();
                ToolsListBox.SelectedItem = selected;
                if (selected == null)
                {
                    ShowFallback();
                }
            }
        }

        // ===== ToolsListBox_SelectionChanged（对照 WPF）=====
        // 对照 WPF: private void ToolsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        //   WPF: if (ToolsListBox.SelectedItem == null) ToolsFallback.Show();
        //         else ToolsFallback.Collapse();
        private void ToolsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ToolsListBox.SelectedItem == null)
            {
                ShowFallback();
            }
            else
            {
                HideFallback();
            }
        }

        // ===== PrimaryButton_Click（spike 新增，对照 WPF ContextMenu Primary MenuItem）=====
        // 对照 WPF: GetContextMenu PrimaryMenuItem.Click：
        //   tool.IsPrimary = !tool.IsPrimary;
        //   if (tool.IsPrimary) foreach (other in allTools) other.IsPrimary = false;
        // spike 版: 同样逻辑
        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is ExternalToolViewModel tool)
            {
                tool.IsPrimary = !tool.IsPrimary;
                if (tool.IsPrimary && _toolViewModels != null)
                {
                    foreach (ExternalToolViewModel other in _toolViewModels)
                    {
                        if (other != tool) other.IsPrimary = false;
                    }
                }
                RefreshSelected();
            }
        }

        // ===== VisibleButton_Click（spike 新增，对照 WPF ContextMenu Visible MenuItem）=====
        // 对照 WPF: GetContextMenu visibleMenuItem.Click: tool.IsVisible = !tool.IsVisible;
        private void VisibleButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is ExternalToolViewModel tool)
            {
                tool.IsVisible = !tool.IsVisible;
                RefreshSelected();
            }
        }

        // ===== ResetButton_Click（spike 新增，对照 WPF ContextMenu Reset to default MenuItem）=====
        // 对照 WPF: GetContextMenu resetMenuItem.Click: tool.ResetToDefault(toolDefinitions);
        // spike 版: 不调用 ExternalToolManager（Avalonia 工程未迁移），仅清空 Path + Arguments
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsListBox.SelectedItem is ExternalToolViewModel tool && tool.IsPredefined && _toolDefinitions != null)
            {
                ToolDefinitionLocal? def = _toolDefinitions.FirstOrDefault(x => x.Type == tool.Type);
                if (def.HasValue)
                {
                    ToolDefinitionLocal d = def.Value;
                    tool.Path = string.Empty;
                    tool.Arguments = string.Join(" ", d.Arguments);
                    tool.IsVisible = true;
                    RefreshSelected();
                }
            }
        }

        // ===== 辅助方法 =====

        // 对照 WPF: private static ObservableCollection<ExternalToolViewModel> CreateExternalToolViewModels(ExternalTool[] tools)
        //   WPF: tools.Map(x => new ExternalToolViewModel(x)) + 按 IsAvailable 倒序排序
        private static ObservableCollection<ExternalToolViewModel> CreateExternalToolViewModels(ExternalTool[] tools)
        {
            ExternalToolViewModel[] array = tools
                .Select(x => new ExternalToolViewModel(x))
                .OrderByDescending(x => x.IsAvailable)
                .ToArray();
            return new ObservableCollection<ExternalToolViewModel>(array);
        }

        // spike 双向绑定刷新辅助：手动重新设 ItemsSource 触发 UI 更新
        // （ExternalToolViewModel 是 POCO，INotifyPropertyChanged 未实现）
        private void RefreshSelected()
        {
            if (_toolViewModels != null && ToolsListBox != null)
            {
                var selected = ToolsListBox.SelectedItem;
                ToolsListBox.ItemsSource = null;
                ToolsListBox.ItemsSource = _toolViewModels;
                ToolsListBox.SelectedItem = selected;
            }
        }

        // 对照 WPF FallbackUserControl.Show/Collapse
        private void ShowFallback()
        {
            if (ToolsFallback != null) ToolsFallback.IsVisible = true;
        }

        private void HideFallback()
        {
            if (ToolsFallback != null) ToolsFallback.IsVisible = false;
        }
    }
}
