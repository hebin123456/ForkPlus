// Avalonia 版 TextContentControl（spike 简化版）。
//
// 对照 WPF 工程 src/ForkPlus/UI/Controls/TextContentControl.cs：
//   - 继承 CodeEditor（AvalonEdit），实现 FileContentControl.IFileContentControlSubControl
//   - SetContent(TextContent) 设置文本内容 + 语法高亮
//   - 代码编辑器行号边距 + 滚动位置缓存
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF CodeEditor (AvalonEdit) → Avalonia AvaloniaEdit.TextEditor
//   2. WPF SyntaxHighlighting → spike 省略（保留占位）
//   3. WPF CodeEditorLineNumberMargin → spike 省略（AvaloniaEdit 内置 ShowLineNumbers）
//   4. WPF NotificationCenter 弱事件 → spike 省略
//   5. WPF CodeEditorScrollPositionCache → spike 省略
using AvaloniaEdit;
using ForkPlus.Git;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Controls
{
    public class TextContentControl : TextEditor
    {
        private Content? _content;

        public TextContentControl()
        {
            IsReadOnly = true;
            ShowLineNumbers = true;
            FontSize = ForkPlusSettings.Default.CodeEditorFontSize;
            // spike: 省略 SyntaxHighlighting 和 CodeEditorLineNumberMargin
        }

        public void SetContent(TextContent? content)
        {
            _content = content;
            Text = content?.Text ?? string.Empty;
            // spike: 省略滚动位置保存/恢复
        }

        public object? GetContent()
        {
            return _content;
        }
    }
}
