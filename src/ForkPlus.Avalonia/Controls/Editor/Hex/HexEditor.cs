using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls.Editor.Hex
{
    // Phase 2.9：Avalonia 版 HexEditor（从 WPF 工程迁移）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Hex/HexEditor.cs（178 行）：
    //   - public class HexEditor : ICSharpCode.AvalonEdit.TextEditor
    //   - 字段：_bytes / _bytesPerRow / _showAscii / _showOffset / _colorizer /
    //     _searchPanel / _highlightedBytes
    //   - 公共属性：BytesPerRow / ShowAscii / ShowOffset（setter 调 Rebuild()）
    //   - 构造函数：IsReadOnly / WordWrap / Options / TextArea.SelectionBorder /
    //     TextArea.SelectionCornerRadius / FontFamily / FontSize + 创建 HexColorizer +
    //     LineTransformers.Add + 从 ForkPlusSettings 恢复 3 个偏好
    //   - InstallSearchPanel / ShowSearch：AvalonEdit SearchPanel.Install + Open + Reactivate
    //   - LoadBytes / GetBytes / HighlightBytes / GetHighlightedBytes
    //   - Rebuild()：调 HexFormatter.Format 赋给 base.Text
    //   - GetSelectedBytes()：用 SelectionStart/SelectionLength 反推字节范围
    //   - OnPreviewKeyDown：Ctrl+C 用 Clipboard.SetText
    //
    // Avalonia 版差异：
    //   1. 继承 Avalonia 版 CodeEditor（已安装 AvaloniaEdit SearchPanel），不直接继承 TextEditor，
    //      这样可复用 CodeEditor 的 SearchPanel + ShowSearchBar/HideSearchBar + GetScrollPosition 等
    //   2. 删除 TextArea.SelectionBorder = null / SelectionCornerRadius = 0（WPF-only，
    //      AvaloniaEdit 用 SelectionBrush/SelectionForeground，spike 不调整）
    //   3. FontFamily 用 Avalonia.Media.FontFamily（替代 System.Windows.Media.FontFamily）
    //   4. ForkPlusSettings.Default.HexView* 在 Avalonia 工程不可访问（WPF-only Settings），
    //      spike 版使用默认值 16 / true / true，Phase 0 抽 IPreferencesService 后再接入
    //   5. InstallSearchPanel / ShowSearch：直接复用 CodeEditor.ShowSearchBar
    //      （CodeEditor 构造函数已 SearchPanel.Install）
    //   6. OnPreviewKeyDown → OnKeyDown（Avalonia 没有 WPF 的 Preview tunneling 事件）
    //   7. Ctrl+C 复制逻辑：System.Windows.Clipboard.SetText →
    //      Application.Current.Clipboard.SetTextAsync（异步，fire-and-forget）
    //   8. Key / Keyboard / ModifierKeys → Avalonia.Input.Key / KeyEventArgs.KeyModifiers
    //
    // 本 spike 版完整迁移（保留所有渲染 + 选中 + 复制 + 搜索逻辑），因为 HexEditor 是
    // HexContentControl / HexDiffUserControl 的核心，spike 必须能独立工作。
    public class HexEditor : CodeEditor
    {
        private byte[] _bytes;
        private int _bytesPerRow = 16;
        private bool _showAscii = true;
        private bool _showOffset = true;
        private HexColorizer _colorizer;
        // v3.1.0：差异字节索引集合（用于 Hex Diff 视图高亮），null 表示不高亮
        private HashSet<int> _highlightedBytes;

        /// <summary>每行字节数（支持 8/16/32）。</summary>
        public int BytesPerRow
        {
            get { return _bytesPerRow; }
            set
            {
                int v = value == 8 || value == 16 || value == 32 ? value : 16;
                if (v != _bytesPerRow)
                {
                    _bytesPerRow = v;
                    Rebuild();
                }
            }
        }

        public bool ShowAscii
        {
            get { return _showAscii; }
            set
            {
                if (value != _showAscii)
                {
                    _showAscii = value;
                    Rebuild();
                }
            }
        }

        public bool ShowOffset
        {
            get { return _showOffset; }
            set
            {
                if (value != _showOffset)
                {
                    _showOffset = value;
                    Rebuild();
                }
            }
        }

        public HexEditor()
        {
            IsReadOnly = true;
            WordWrap = false;
            // CodeEditor 基类已设置 Options.InheritWordWrapIndentation / EnableHyperlinks / EnableEmailHyperlinks
            // 这里仅补充 HexEditor 特有的字体
            FontFamily = new FontFamily("Consolas, Courier New, monospace");
            FontSize = 13.0;

            _colorizer = new HexColorizer(this);
            TextArea.TextView.LineTransformers.Add(_colorizer);

            // 对照 WPF：从 ForkPlusSettings.Default 恢复 3 个偏好
            // spike 版跳过（ForkPlusSettings 是 WPF-only Settings，Avalonia 工程不可访问；
            //   Phase 0 抽 IPreferencesService 后再接入）
            // _bytesPerRow = ForkPlusSettings.Default.HexViewBytesPerRow;
            // _showAscii = ForkPlusSettings.Default.HexViewShowAscii;
            // _showOffset = ForkPlusSettings.Default.HexViewShowOffset;
        }

        // 对照 WPF: public void InstallSearchPanel() — SearchPanel.Install(base.TextArea)
        // Avalonia 版：CodeEditor 基类构造函数已 SearchPanel.Install(this)，这里无需再 Install，
        // 保留方法签名兼容 WPF 调用方（HexContentControl 可能调用此方法）
        public void InstallSearchPanel()
        {
            // CodeEditor 基类已 Install，这里 no-op
        }

        /// <summary>显示搜索面板。</summary>
        // 对照 WPF: _searchPanel?.Open(); if (!_searchPanel.IsClosed) _searchPanel.Reactivate();
        // Avalonia 版：直接复用 CodeEditor.ShowSearchBar（调 _searchPanel.Open()）
        // spike 版跳过 Reactivate（AvaloniaEdit SearchPanel.Open 已包含聚焦逻辑）
        public void ShowSearch()
        {
            ShowSearchBar();
        }

        /// <summary>加载字节并渲染。</summary>
        public void LoadBytes(byte[] bytes)
        {
            _bytes = bytes ?? Array.Empty<byte>();
            Rebuild();
        }

        /// <summary>当前已加载的字节（可能为 null）。</summary>
        public byte[] GetBytes()
        {
            return _bytes;
        }

        /// <summary>v3.1.0：标记需要高亮背景的字节索引（用于 Hex Diff）。传 null 清除高亮。</summary>
        public void HighlightBytes(HashSet<int> byteIndices)
        {
            _highlightedBytes = byteIndices;
            _colorizer?.SetHighlightedBytes(byteIndices);
            TextArea.TextView.Redraw();
        }

        /// <summary>v3.1.0：当前高亮的字节索引集合（可能为 null）。</summary>
        public HashSet<int> GetHighlightedBytes()
        {
            return _highlightedBytes;
        }

        private void Rebuild()
        {
            if (_bytes == null)
            {
                Text = "";
                return;
            }
            string text = HexFormatter.Format(_bytes, _bytesPerRow, _showOffset, _showAscii);
            Text = text;
        }

        /// <summary>把选中文本中的 hex 字节解析回原始字节（用于"复制为原始字节"）。</summary>
        public byte[] GetSelectedBytes()
        {
            if (_bytes == null) return Array.Empty<byte>();
            // AvaloniaEdit Selection 是基于字符偏移的，根据选中起止字符偏移反推字节区间
            int startOffset = SelectionStart;
            int endOffset = startOffset + SelectionLength;
            ByteRange range = HexFormatter.CharOffsetsToByteRange(startOffset, endOffset, _bytesPerRow, _showOffset, _showAscii);
            int start = Math.Max(0, range.Start);
            int end = Math.Min(_bytes.Length, range.End);
            if (end <= start) return Array.Empty<byte>();
            byte[] result = new byte[end - start];
            Array.Copy(_bytes, start, result, 0, result.Length);
            return result;
        }

        // 对照 WPF: protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
        // Avalonia 版改为 OnKeyDown（无 Preview tunneling 事件，用 bubble + Handled）
        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Ctrl+C：复制选中文本到剪贴板
            // 对照 WPF：e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
            // Avalonia：e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0
            if (e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0)
            {
                string selectedText = SelectedText;
                if (!string.IsNullOrEmpty(selectedText))
                {
                    try
                    {
                        // 对照 WPF: System.Windows.Clipboard.SetText(selectedText)（同步）
                        // Avalonia: Application.Current.Clipboard.SetTextAsync（异步，fire-and-forget）
                        Application.Current?.Clipboard?.SetTextAsync(selectedText);
                        e.Handled = true;
                    }
                    catch { }
                }
            }
            base.OnKeyDown(e);
        }
    }
}
