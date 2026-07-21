using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls.Editor.Hex
{
    // Phase 2.9：Avalonia 版 HexContentControl（单文件 Hex 视图容器）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Hex/HexContentControl.cs（181 行，纯 C# Grid 子类）：
    //   - HexContentControl : Grid, FileContentControl.IFileContentControlSubControl
    //   - 字段：_editor / _bytesPerRowComboBox / _showAsciiCheckBox / _showOffsetCheckBox / _content
    //   - 构造函数：构建工具栏（DockPanel + ComboBox + 2 个 CheckBox + 2 个 Button）+ 创建 HexEditor
    //   - SetContent(HexContent)：调 _editor.LoadBytes(content.Data.ToArray())
    //   - ControlWillBeRemovedFromFileContentControl：调 _content?.DisposeData()
    //   - 工具栏事件：3 个 setter → _editor.BytesPerRow/ShowAscii/ShowOffset + ForkPlusSettings.Save
    //   - SearchButton_Click：_editor.InstallSearchPanel + _editor.ShowSearch
    //   - CopyRawButton_Click：_editor.GetSelectedBytes + Clipboard.SetData(Serializable + Text)
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. UserControl 替代 Grid 子类（axaml 已定义布局，code-behind 仅处理事件）
    //   2. 跳过 IFileContentControlSubControl 接口（接口在 Avalonia 工程不存在，spike 用方法签名兼容）
    //   3. 跳过 ForkPlusSettings.Default.HexView* 持久化（WPF-only Settings）
    //   4. PreferencesLocalization.Current(...) 用字面量字符串替代（已在 axaml 中）
    //   5. 复制为原始字节简化：仅写入文本格式（Avalonia IClipboard 不支持 DataFormats.Serializable）
    //   6. ComboBox.Items.Add(8/16/32) → axaml 中 3 个 ComboBoxItem + SelectedIndex=1（默认 16）
    //      SelectionChanged 事件中通过 SelectedItem 解析 ComboBoxItem.Content 转 int
    //
    // 本 spike 版验证：
    //   - HexEditor 可在 axaml 中实例化并正确渲染
    //   - 工具栏控件交互可触发 HexEditor 重渲染
    //   - SetContent 可加载字节并显示
    public partial class HexContentControl : UserControl
    {
        private HexContent _content;

        public HexContentControl()
        {
            InitializeComponent();

            // 工具栏事件订阅（对照 WPF 构造函数中的 +=）
            BytesPerRowComboBox.SelectionChanged += BytesPerRowComboBox_SelectionChanged;
            ShowAsciiCheckBox.Checked += ShowAsciiCheckBox_Changed;
            ShowAsciiCheckBox.Unchecked += ShowAsciiCheckBox_Changed;
            ShowOffsetCheckBox.Checked += ShowOffsetCheckBox_Changed;
            ShowOffsetCheckBox.Unchecked += ShowOffsetCheckBox_Changed;
            SearchButton.Click += SearchButton_Click;
            CopyRawButton.Click += CopyRawButton_Click;
        }

        // 对照 WPF: public void SetContent(HexContent content)
        public void SetContent(HexContent content)
        {
            _content = content;
            if (HexEditorControl == null) return;
            if (content?.Data != null)
            {
                HexEditorControl.LoadBytes(content.Data.ToArray());
            }
            else
            {
                HexEditorControl.LoadBytes(null);
            }
        }

        /// <summary>从 FileContentControl 移除时释放 MemoryStream（对照 WPF）。</summary>
        public void ControlWillBeRemovedFromFileContentControl()
        {
            _content?.DisposeData();
            _content = null;
        }

        // 对照 WPF: ComboBox.Items.Add(8/16/32) + SelectedItem = ForkPlusSettings.Default.HexViewBytesPerRow
        // Avalonia 版：axaml 中已定义 3 个 ComboBoxItem，这里通过 SelectedItem 转换
        private void BytesPerRowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HexEditorControl == null) return;
            if (BytesPerRowComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int v))
            {
                HexEditorControl.BytesPerRow = v;
                // 对照 WPF: ForkPlusSettings.Default.HexViewBytesPerRow = v; ForkPlusSettings.Default.Save();
                // spike 版跳过持久化（Phase 0 抽 IPreferencesService 后再接入）
            }
        }

        private void ShowAsciiCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;
            bool v = ShowAsciiCheckBox.IsChecked ?? true;
            HexEditorControl.ShowAscii = v;
            // 对照 WPF: ForkPlusSettings.Default.HexViewShowAscii = v; ForkPlusSettings.Default.Save();
        }

        private void ShowOffsetCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;
            bool v = ShowOffsetCheckBox.IsChecked ?? true;
            HexEditorControl.ShowOffset = v;
            // 对照 WPF: ForkPlusSettings.Default.HexViewShowOffset = v; ForkPlusSettings.Default.Save();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // 对照 WPF: _editor.InstallSearchPanel(); _editor.ShowSearch();
            // Avalonia 版：HexEditor 继承 CodeEditor，构造时已 SearchPanel.Install，InstallSearchPanel 是 no-op
            HexEditorControl?.InstallSearchPanel();
            HexEditorControl?.ShowSearch();
        }

        private void CopyRawButton_Click(object sender, RoutedEventArgs e)
        {
            if (HexEditorControl == null) return;
            byte[] bytes = HexEditorControl.GetSelectedBytes();
            if (bytes.Length == 0) return;
            try
            {
                // 对照 WPF: Clipboard.SetData(DataFormats.Serializable, bytes) + SetData(Text, hex string)
                // Avalonia 11 的 Clipboard 通过 TopLevel.GetTopLevel 获取（不是 Application.Clipboard）。
                // IClipboard 只支持 SetTextAsync（不支持二进制格式），spike 仅写入文本。
                string hexText = BitConverter.ToString(bytes).Replace("-", " ");
                TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(hexText);
            }
            catch { }
        }
    }
}
