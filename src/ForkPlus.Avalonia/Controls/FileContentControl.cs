using System;
using Avalonia.Controls;
using AvaloniaEdit;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 FileContentControl（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FileContentControl.cs（201 行）：
    //   - WPF FileContentControl : Grid
    //   - Grid 2 行：Row 0 (Auto) FileControlHeaderUserControl（默认 Collapse）
    //                  Row 1 (*)    动态切换的 _subView（ShowSubView<TChild>）
    //   - IFileContentControlSubControl 接口：ControlWillBeRemovedFromFileContentControl 回调
    //   - DependencyProperty Content (GitCommandResult<Content>)
    //   - UpdateView(bool loadLargeDiff) 路由：
    //     HexContent → HexContentControl
    //     BinaryContent → BinaryFileContentControl
    //     TextContent → TextContentControl（AvalonEdit）
    //   - MaxContentSize = 1048576（1MB，超出时显示 Fallback "too large"）
    //   - TextContentControl.Commands (TextContentControlCommands) 上下文菜单
    //
    // Avalonia 版差异（spike 简化策略，task spec：用 AvaloniaEdit.TextEditor 显示文件内容）：
    //   1. WPF Grid 基类 → spike 直接继承 UserControl
    //   2. WPF DependencyProperty.Register → spike 用 plain property（FileData 替代 Content 避免遮蔽）
    //   3. WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → spike 用 plain property setter
    //   4. WPF 3 种 sub-view 路由 → spike 仅用 1 个 AvaloniaEdit.TextEditor 显示文本
    //   5. WPF TextContentControl → spike 直接用 AvaloniaEdit.TextEditor
    //   6. spike 跳过 HexContentControl / BinaryFileContentControl 子视图路由
    //   7. spike 跳过 TextContentControlCommands 上下文菜单
    //   8. spike 跳过 MaxContentSize 大文件 fallback
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 UserControl
    //   - 内嵌 1 个 AvaloniaEdit.TextEditor 显示文件内容
    //   - SetContent(string text) 公共方法加载文本
    public class FileContentControl : UserControl
    {
        // 对照 WPF: public interface IFileContentControlSubControl
        public interface IFileContentControlSubControl
        {
            void ControlWillBeRemovedFromFileContentControl();
        }

        // 对照 WPF: DependencyProperty Content (GitCommandResult<Content>)
        // spike: 用 object FileData 替代（避免遮蔽 UserControl.Content）
        public object FileData { get; set; }

        // 对照 WPF: public RepositoryUserControl RepositoryUserControl { get; set; }
        // spike: 用 object 占位（RepositoryUserControl 类型在 Views/UserControls）
        public object RepositoryUserControl { get; set; }

        // spike: 内嵌 AvaloniaEdit.TextEditor 显示文件内容
        // 对照 WPF: TextContentControl → CodeEditor : ICSharpCode.AvalonEdit.TextEditor
        private readonly TextEditor _editor;

        public FileContentControl()
        {
            // spike: 用 AvaloniaEdit.TextEditor 直接显示文件内容（task spec 关键 API）
            _editor = new TextEditor
            {
                IsReadOnly = true,
                ShowLineNumbers = true,
                WordWrap = false
            };
            Content = _editor;
        }

        // spike 公共方法：加载文件文本到 AvaloniaEdit.TextEditor
        // 对照 WPF: TextContentControl.SetContent(textContent)
        // spike: 直接接收文本字符串（跳过 TextContent 对象）
        public void SetContent(string text)
        {
            _editor.Text = text ?? string.Empty;
        }

        // 对照 WPF: protected virtual void UpdateView(bool loadLargeDiff = false)
        // spike: 占位，不调 git 命令
        public virtual void UpdateView(bool loadLargeDiff = false)
        {
            // spike: 真实路由逻辑待 Phase 3.9b
        }
    }
}
