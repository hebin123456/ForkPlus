// WPF → Avalonia 迁移兼容层 第四部分：fixer pass3 依赖的 shim
// ScrollViewer.ScrollToVerticalOffset/ScrollToHorizontalOffset 等。

using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ForkPlus.UI.WpfCompat
{
    /// <summary>WPF ScrollViewer.ScrollTo*Offset 扩展（Avalonia 经 Offset 属性设置）。</summary>
    public static class ScrollViewerCompat
    {
        public static void ScrollToVerticalOffsetCompat(this ScrollViewer sv, double offset)
        {
            if (sv == null) return;
            sv.Offset = sv.Offset.WithY(offset);
        }

        public static void ScrollToHorizontalOffsetCompat(this ScrollViewer sv, double offset)
        {
            if (sv == null) return;
            sv.Offset = sv.Offset.WithX(offset);
        }

        // Migration note（2026-09-03，FileDiff 左右视图滚动不同步的根因）：
        // AvaloniaEdit 12.x 的 TextEditor.ScrollToVerticalOffset/ScrollToHorizontalOffset 是
        // 空操作（源码里滚动实现整段被注释，只剩 ApplyTemplate），直接转发等于什么都不做。
        // 正确入口是模板 PART_ScrollViewer（本项目为 TouchpadAwareScrollViewer）的 Offset：
        // 与 AvaloniaEdit 自家 ScrollTo(line,column) 和滚轮路径一致，Offset 变更经
        // ScrollContentPresenter 的逻辑滚动订阅推进 TextView → 触发 ScrollOffsetChanged。
        // 直接改 TextView 的 IScrollable.Offset 不行：TextView 不会反向通知 ScrollViewer
        // （RaiseScrollInvalidated 只在 SetScrollData/MakeVisible 里调），ScrollViewer.Offset
        // 会滞留旧值，用户下一次滚轮就跳回旧位置。
        /// <summary>滚动 TextEditor（经模板 PART_ScrollViewer.Offset，修复 AvaloniaEdit 空操作）。</summary>
        public static void ScrollToVerticalOffsetCompat(this AvaloniaEdit.TextEditor editor, double offset)
        {
            ScrollViewer sv = FindEditorScrollViewer(editor);
            if (sv == null) return;
            sv.Offset = sv.Offset.WithY(offset);
        }

        /// <summary>滚动 TextEditor（经模板 PART_ScrollViewer.Offset，修复 AvaloniaEdit 空操作）。</summary>
        public static void ScrollToHorizontalOffsetCompat(this AvaloniaEdit.TextEditor editor, double offset)
        {
            ScrollViewer sv = FindEditorScrollViewer(editor);
            if (sv == null) return;
            sv.Offset = sv.Offset.WithX(offset);
        }

        // TextEditor.ScrollViewer 是 AvaloniaEdit internal，从模板部件/可视树里找
        // PART_ScrollViewer（找不到具名的则退回第一个 ScrollViewer，兼容自定义模板）。
        private static ScrollViewer FindEditorScrollViewer(AvaloniaEdit.TextEditor editor)
        {
            if (editor == null)
            {
                return null;
            }
            ScrollViewer named = editor.GetVisualDescendants().OfType<ScrollViewer>()
                .FirstOrDefault((ScrollViewer x) => x.Name == "PART_ScrollViewer");
            if (named != null)
            {
                return named;
            }
            return editor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        }
    }
}
