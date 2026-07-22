using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using ForkPlus.Git.Merge;
using ForkPlus.Git.Merge.Presentation;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls.Editor.Merge
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/Merge/MergeChunkSelectionLayer.cs（146 行）：
    //   - public class MergeChunkSelectionLayer : ChunkSelectionLayer<MergeConflictView.Chunk>
    //   - 构造函数：base((CodeEditor)mergeCodeEditor)，保存 _textEditor
    //   - CreateAdornerContent：创建 FloatingButton + StackPanel + Border（Adorner 浮动按钮容器）
    //     + FloatingButton.Click 事件 → OnMergeChunkAdded / OnMergeChunkRemoved
    //   - RefreshActiveChunk：GetChunkUnderMousePointer → ActiveChunk → RefreshButtonsState
    //   - RefreshButtonsState：根据 ViewMode + AllItemSelected 设置按钮文字
    //     (Select Right / Remove Right / Select Left / Remove Left)
    //   - OnRender：base.OnRender + 若 ViewMode 是 Local/Remote + activeChunk.Node is ConflictChunk
    //     → DrawChunk
    //   - GetRectForChunk：遍历 chunk.Lines 取第一个可视 VisualLine → CreateLineBlockRect
    //   - GetChunkByOffset：MergeConflictView?.GetConflictedChunkAt(offset)
    //   - ShowAdornerOnMouseOver：调整 topPosition（-15 或 0）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 ChunkSelectionLayer → 本工程 ChunkSelectionLayer（spike 版，Adorner 留空实现）
    //   2. WPF PreferencesLocalization.Current("xxx") → ServiceLocator.Localization.Translate
    //      （task spec 关键 API：PreferencesLocalization → ServiceLocator.Localization）
    //   3. WPF FrameworkElement → Avalonia.Controls.Control（CreateAdornerContent 返回类型）
    //   4. WPF StackPanel / Border / Button → Avalonia.Controls 同名类型
    //   5. WPF Brushes.Transparent → Avalonia.Media.Brushes.Transparent
    //   6. WPF CornerRadius → Avalonia CornerRadius（同名）
    //   7. WPF Thickness → Avalonia Thickness（同名）
    //   8. WPF HorizontalAlignment → Avalonia.Layout.HorizontalAlignment（需 using Avalonia.Layout）
    //   9. WPF OnRender(DrawingContext) → Avalonia Render(DrawingContext)
    //  10. namespace 改为 ForkPlus.Avalonia.Controls.Editor.Merge
    //
    // spike 简化（task spec：复杂渲染逻辑可简化为空实现 + 注释）：
    //   - CreateAdornerContent：保留 StackPanel + Border + FloatingButton 创建逻辑
    //     （spike 阶段 ChunkSelectionLayer.ShowChunkAdorner 是空实现，此内容不会被渲染，
    //      但保留创建逻辑以便 phase 3.9b 接入 Popup/Canvas 时立即可用）
    //   - RefreshButtonsState：保留完整逻辑（按钮文字设置）
    //   - OnRender → Render：调 base.Render + DrawChunk（spike 阶段 DrawChunk 空实现）
    //   - GetRectForChunk：返回 null（CreateLineBlockRect 在 spike 阶段返回 default(Rect)）
    //   - GetChunkByOffset：保留完整逻辑
    public class MergeChunkSelectionLayer : ChunkSelectionLayer<MergeConflictView.Chunk>
    {
        private readonly MergeCodeEditor _textEditor;
        private FloatingButton _selectButton;

        // 对照 WPF: public MergeChunkSelectionLayer(MergeCodeEditor mergeCodeEditor) : base((CodeEditor)mergeCodeEditor)
        public MergeChunkSelectionLayer(MergeCodeEditor mergeCodeEditor)
            : base(mergeCodeEditor)
        {
            _textEditor = mergeCodeEditor;
        }

        // 对照 WPF: protected override FrameworkElement CreateAdornerContent(TextEditor textEditor)
        // Avalonia: protected override Control CreateAdornerContent(TextEditor textEditor)
        protected override Control CreateAdornerContent(TextEditor textEditor)
        {
            _selectButton = new FloatingButton(textEditor);
            RefreshButtonsState();

            // 对照 WPF: WeakEventManager<FloatingButton, RoutedEventArgs>.AddHandler(_selectButton, "Click", ...)
            // spike 版：Avalonia Button.Click 事件直接订阅
            _selectButton.Click += (sender, e) =>
            {
                MergeConflictView.Chunk activeChunk = ActiveChunk;
                if (activeChunk != null)
                {
                    if (activeChunk.AllItemSelected(_textEditor.ViewMode))
                    {
                        _textEditor.OnMergeChunkRemoved(activeChunk);
                    }
                    else
                    {
                        _textEditor.OnMergeChunkAdded(activeChunk);
                    }
                    RefreshButtonsState();
                }
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brushes.Transparent
            };
            stackPanel.Children.Add(_selectButton);
            return new Border
            {
                Child = stackPanel,
                Background = global::ForkPlus.Avalonia.Theme.Diff.FloatingButtonContainerBackground,
                CornerRadius = new CornerRadius(3.0),
                Margin = new Thickness(0.0, 0.0, 20.0, 0.0)
            };
        }

        // 对照 WPF: protected override void RefreshActiveChunk()
        protected override void RefreshActiveChunk()
        {
            MergeConflictView.Chunk chunkUnderMousePointer = GetChunkUnderMousePointer();
            if (chunkUnderMousePointer != ActiveChunk)
            {
                ActiveChunk = chunkUnderMousePointer;
                RefreshButtonsState();
            }
        }

        // 对照 WPF: private void RefreshButtonsState()
        private void RefreshButtonsState()
        {
            MergeConflictView.Chunk activeChunk = ActiveChunk;
            if (activeChunk == null)
            {
                RemoveChunkAdorner();
            }
            else if (_textEditor.ViewMode == MergeConflictPart.Local)
            {
                _selectButton.Content = activeChunk.AllItemSelected(_textEditor.ViewMode)
                    ? Translate("Remove Right")
                    : Translate("Select Right");
            }
            else if (_textEditor.ViewMode == MergeConflictPart.Remote)
            {
                _selectButton.Content = activeChunk.AllItemSelected(_textEditor.ViewMode)
                    ? Translate("Remove Left")
                    : Translate("Select Left");
            }
        }

        // 对照 WPF: protected override void OnRender(DrawingContext drawingContext)
        // Avalonia: public override void Render(DrawingContext drawingContext)
        public override void Render(DrawingContext drawingContext)
        {
            base.Render(drawingContext);
            TextArea textArea = _textEditor.TextArea;
            if (_textEditor.ViewMode == MergeConflictPart.Local || _textEditor.ViewMode == MergeConflictPart.Remote)
            {
                MergeConflictView.Chunk activeChunk = ActiveChunk;
                if (activeChunk != null && activeChunk.Node is MergeConflict.ConflictChunk)
                {
                    DrawChunk(drawingContext, textArea.TextView, activeChunk);
                }
            }
        }

        // 对照 WPF: protected override Rect? GetRectForChunk(MergeConflictView.Chunk chunk)
        protected override global::Avalonia.Rect? GetRectForChunk(MergeConflictView.Chunk chunk)
        {
            TextView textView = _textEditor.TextArea.TextView;
            global::Avalonia.Rect? result = null;
            MergeConflictView.Line[] lines = chunk.Lines;
            foreach (MergeConflictView.Line line in lines)
            {
                VisualLine visualLine = textView.GetVisualLine(line.LineNumber + 1);
                if (visualLine != null)
                {
                    int lineNumber = line.LineNumber;
                    int lineNumber2 = chunk.Lines[chunk.Lines.Length - 1].LineNumber;
                    result = CreateLineBlockRect(visualLine, lineNumber2 - lineNumber + 1);
                    break;
                }
            }
            return result;
        }

        // 对照 WPF: protected override MergeConflictView.Chunk GetChunkByOffset(int offset)
        protected override MergeConflictView.Chunk GetChunkByOffset(int offset)
        {
            return _textEditor.MergeConflictView?.GetConflictedChunkAt(offset);
        }

        // 对照 WPF: protected override void ShowAdornerOnMouseOver(double topPosition)
        protected override void ShowAdornerOnMouseOver(double topPosition)
        {
            topPosition = (topPosition < 0.0) ? 0.0 : (topPosition - 15.0);
            base.ShowAdornerOnMouseOver(topPosition);
        }

        // 对照 WPF: PreferencesLocalization.Current("xxx")
        // spike 版：ServiceLocator.Localization.Translate(name, UiLanguage)
        private static string Translate(string name)
        {
            var localization = ForkPlus.Services.ServiceLocator.Localization;
            var userSettings = ForkPlus.Services.ServiceLocator.UserSettings;
            if (localization != null && userSettings != null)
            {
                return localization.Translate(name, userSettings.UiLanguage);
            }
            return name;
        }
    }
}
