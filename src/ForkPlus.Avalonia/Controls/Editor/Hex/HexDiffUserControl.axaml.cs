using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls.Editor.Hex
{
    // Phase 2.9：Avalonia 版 HexDiffUserControl（side-by-side Hex Diff 视图）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Hex/HexDiffUserControl.cs（298 行，纯 C# Grid 子类）：
    //   - HexDiffUserControl : Grid, FileContentControl.IFileContentControlSubControl
    //   - 字段：_srcEditor / _dstEditor / _bytesPerRowComboBox / _showAsciiCheckBox /
    //     _showOffsetCheckBox / _syncScrollCheckBox / _content / 滚动同步防抖状态
    //   - 构造函数：3 行 Grid + 工具栏 + 列头 + 双 HexEditor + 订阅 ScrollOffsetChanged
    //   - SetContent(HexDiffContent)：加载两侧字节 + ApplyDiffHighlight
    //   - ApplyDiffHighlight：逐字节比较，差异字节索引集合 → HexEditor.HighlightBytes
    //     超过 MaxBytesForDiffHighlight (2MB) 跳过；超出对侧长度的部分也视为差异
    //   - ControlWillBeRemovedFromFileContentControl：释放两侧 MemoryStream
    //   - 工具栏事件：3 个 setter 同时设置 _srcEditor + _dstEditor
    //   - OnScrollOffsetChanged：100ms 防抖 + _isSyncingScroll 重入守卫 + ScrollToVerticalOffset /
    //     ScrollToHorizontalOffset 同步对侧滚动
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. UserControl 替代 Grid 子类（axaml 已定义布局，code-behind 仅处理事件）
    //   2. 跳过 IFileContentControlSubControl 接口（接口在 Avalonia 工程不存在，spike 用方法签名兼容）
    //   3. 跳过 ForkPlusSettings.Default.HexView* 持久化（WPF-only Settings）
    //   4. spike 版跳过 SyncScroll CheckBox + 滚动同步逻辑（ScrollOffsetChanged 事件 API 在
    //      AvaloniaEdit 12 与 AvalonEdit 略有差异，留 Phase 3.9b 验证后接入）
    //   5. 保留 ApplyDiffHighlight 完整逻辑（HexDiffContent 的核心功能）
    //
    // 本 spike 版暂不迁移（留 Phase 3.9b）：
    //   - SyncScroll CheckBox + 滚动同步（_lastScrollTime / _lastScrolledEditor / _isSyncingScroll）
    //   - TextView.ScrollOffsetChanged 事件订阅
    //   - IsVerticalOffsetWithinDocumentArea / IsHorizontalOffsetWithinDocumentArea 边界检查
    //
    // 本 spike 版验证：
    //   - 双 HexEditor 可在 axaml Grid ColumnDefinitions 中并排实例化
    //   - SetContent(HexDiffContent) 可同时加载两侧字节
    //   - ApplyDiffHighlight 逐字节比较并触发 HighlightBytes 高亮差异字节
    public partial class HexDiffUserControl : UserControl
    {
        // v3.1.0：超过此阈值跳过逐字节比较（避免大文件卡顿）
        private const int MaxBytesForDiffHighlight = 2 * 1024 * 1024; // 2MB

        private HexDiffContent _content;

        public HexDiffUserControl()
        {
            InitializeComponent();

            // 工具栏事件订阅（对照 WPF 构造函数中的 +=）
            BytesPerRowComboBox.SelectionChanged += BytesPerRowComboBox_SelectionChanged;
            ShowAsciiCheckBox.Checked += ShowAsciiCheckBox_Changed;
            ShowAsciiCheckBox.Unchecked += ShowAsciiCheckBox_Changed;
            ShowOffsetCheckBox.Checked += ShowOffsetCheckBox_Changed;
            ShowOffsetCheckBox.Unchecked += ShowOffsetCheckBox_Changed;
        }

        // 对照 WPF: public void SetContent(HexDiffContent content)
        public void SetContent(HexDiffContent content)
        {
            _content = content;
            if (SrcEditor == null || DstEditor == null) return;

            byte[] srcBytes = content?.SrcData?.ToArray();
            byte[] dstBytes = content?.DstData?.ToArray();
            SrcEditor.LoadBytes(srcBytes);
            DstEditor.LoadBytes(dstBytes);
            ApplyDiffHighlight(srcBytes, dstBytes);
        }

        /// <summary>逐字节比较两侧，在差异字节位置叠加背景色（对照 WPF）。</summary>
        /// <remarks>
        /// 实现：收集两侧差异字节索引集合，通过 HexEditor.HighlightBytes 触发 HexColorizer 高亮。
        /// 超过 MaxBytesForDiffHighlight 跳过；超出对侧长度的部分也视为差异。
        /// </remarks>
        private void ApplyDiffHighlight(byte[] srcBytes, byte[] dstBytes)
        {
            if (srcBytes == null || dstBytes == null) return;
            if (SrcEditor == null || DstEditor == null) return;

            int len = Math.Min(srcBytes.Length, dstBytes.Length);
            if (len > MaxBytesForDiffHighlight) return; // 大文件跳过

            // 收集 src 侧 / dst 侧差异字节索引
            HashSet<int> srcDiff = new HashSet<int>();
            HashSet<int> dstDiff = new HashSet<int>();
            for (int i = 0; i < len; i++)
            {
                if (srcBytes[i] != dstBytes[i])
                {
                    srcDiff.Add(i);
                    dstDiff.Add(i);
                }
            }
            // 超出对侧长度的部分也视为差异
            if (srcBytes.Length > dstBytes.Length)
            {
                for (int i = dstBytes.Length; i < srcBytes.Length; i++) srcDiff.Add(i);
            }
            if (dstBytes.Length > srcBytes.Length)
            {
                for (int i = srcBytes.Length; i < dstBytes.Length; i++) dstDiff.Add(i);
            }

            SrcEditor.HighlightBytes(srcDiff);
            DstEditor.HighlightBytes(dstDiff);
        }

        /// <summary>从 FileContentControl 移除时释放两侧 MemoryStream（对照 WPF）。</summary>
        public void ControlWillBeRemovedFromFileContentControl()
        {
            _content?.DisposeData();
            _content = null;
        }

        private void BytesPerRowComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SrcEditor == null || DstEditor == null) return;
            if (BytesPerRowComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int v))
            {
                SrcEditor.BytesPerRow = v;
                DstEditor.BytesPerRow = v;
                // 对照 WPF: ForkPlusSettings.Default.HexViewBytesPerRow = v; ForkPlusSettings.Default.Save();
            }
        }

        private void ShowAsciiCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (SrcEditor == null || DstEditor == null) return;
            bool v = ShowAsciiCheckBox.IsChecked ?? true;
            SrcEditor.ShowAscii = v;
            DstEditor.ShowAscii = v;
            // 对照 WPF: ForkPlusSettings.Default.HexViewShowAscii = v; ForkPlusSettings.Default.Save();
        }

        private void ShowOffsetCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (SrcEditor == null || DstEditor == null) return;
            bool v = ShowOffsetCheckBox.IsChecked ?? true;
            SrcEditor.ShowOffset = v;
            DstEditor.ShowOffset = v;
            // 对照 WPF: ForkPlusSettings.Default.HexViewShowOffset = v; ForkPlusSettings.Default.Save();
        }

        // Phase 3.9b 在此补：
        //   - SyncScroll CheckBox + Checked/Unchecked 事件
        //   - OnScrollOffsetChanged(HexEditor editor) 方法
        //     对照 WPF：100ms 防抖 + _isSyncingScroll 重入守卫 + ScrollToVerticalOffset /
        //     ScrollToHorizontalOffset 同步对侧滚动
        //   - _srcEditor.TextArea.TextView.ScrollOffsetChanged 订阅
        //   - _dstEditor.TextArea.TextView.ScrollOffsetChanged 订阅
        //   - IsVerticalOffsetWithinDocumentArea / IsHorizontalOffsetWithinDocumentArea 边界检查
    }
}
