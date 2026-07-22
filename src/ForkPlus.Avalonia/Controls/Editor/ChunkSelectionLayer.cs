using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/ChunkSelectionLayer.cs（413 行）：
    //   - public abstract class ChunkSelectionLayer<TChunk> : FrameworkElement, IWeakEventListener
    //     where TChunk : class
    //   - 嵌套类 ButtonsAdorner : Adorner（WPF AdornerLayer 浮动按钮容器）
    //   - 静态字段：_chunkBorderPen / _chunkBorderBrush / _chunkBackgroundBrush /
    //     _chunkBackgroundBrushDark（WPF SolidColorBrush + Color.FromRgb/FromArgb + Freeze）
    //   - 构造函数：订阅 TextEditor MouseEnter/Leave/Move + TextArea.SelectionChanged +
    //     TextChanged + IsVisibleChanged + TextView ScrollOffsetChanged +
    //     NotificationCenter.ApplicationThemeChanged
    //   - ActiveChunk 属性（get/set，set 时 InvalidateAdornerVisibility + InvalidateVisual）
    //   - abstract：RefreshActiveChunk / CreateAdornerContent / GetRectForChunk / GetChunkByOffset
    //   - DrawChunk：取 chunk 矩形 + DrawBorder + ShowAdornerOnMouseOver
    //   - ShowChunkAdorner：创建 ButtonsAdorner 并添加到 AdornerLayer，设置 Margin
    //   - RemoveChunkAdorner：从 AdornerLayer 移除
    //   - DrawSelectionBorder：用 BackgroundGeometryBuilder.GetRectsForSegment 画选区边框
    //   - DrawBorder：用 RectangleGeometry + DrawGeometry 画圆角矩形边框
    //   - CreateSelectionGeometry：用 BackgroundGeometryBuilder 构建选区几何
    //   - CreateLineBlockRect：根据 VisualLine + lineCount 计算行块矩形
    //   - GetChunkUnderMousePointer：Mouse.GetPosition + VisualTreeHelper.HitTest +
    //     GetPositionFromPoint + GetChunkByOffset
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF FrameworkElement → Avalonia.Controls.Control（Avalonia 无 FrameworkElement，
    //      Control 是等价基类，spike 不需要 FrameworkElement 的 Width/Height/Alignment 属性）
    //   2. WPF IWeakEventListener → spike 移除（Avalonia 无 WeakEventManager 模式，
    //      ScrollOffsetChanged 改为直接订阅 TextView.ScrollOffsetChanged 事件，
    //      或 spike 阶段不订阅，留空实现 + 注释）
    //   3. WPF Adorner / AdornerLayer → spike 移除（Avalonia 无 AdornerLayer 等价物，
    //      浮动按钮改用 Avalonia Popup 或 Canvas 叠加层实现，spike 阶段留空实现 + 注释）
    //   4. WPF NotificationCenter.ApplicationThemeChanged → spike 移除（NotificationCenter
    //      在 WPF 工程，Avalonia 工程不可访问，Phase 0/4 抽象 INotificationService 后再接入）
    //   5. WPF Mouse.GetPosition / VisualTreeHelper.HitTest → spike 移除
    //      （Avalonia 用 PointerEventArgs.GetPosition + InputHitTest，
    //      spike 阶段 GetChunkUnderMousePointer 返回 null + 注释）
    //   6. WPF DrawingContext.DrawGeometry(pen, brush, geometry) →
    //      Avalonia DrawingContext.DrawGeometry(brush, pen, geometry)（参数顺序不同）
    //   7. WPF Pen / SolidColorBrush / Color / RectangleGeometry →
    //      Avalonia.Media.Pen / SolidColorBrush / Color / RectangleGeometry（同名，API 一致）
    //   8. WPF brush.Freeze() → 删除（Avalonia Brush immutable）
    //   9. WPF BackgroundGeometryBuilder → AvaloniaEdit.Rendering.BackgroundGeometryBuilder
    //      （API 一致，spike 阶段渲染方法留空实现 + 注释）
    //  10. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    //
    // spike 简化（task spec：复杂渲染逻辑可简化为空实现 + 注释）：
    //   - ActiveChunk 属性保留（get/set，set 时调 InvalidateAdornerVisibility）
    //   - abstract 方法保留（子类 MergeChunkSelectionLayer 需要实现）
    //   - DrawChunk / DrawSelectionBorder / DrawBorder / CreateSelectionGeometry /
    //     CreateLineBlockRect / ShowChunkAdorner / RemoveChunkAdorner → 空实现 + 注释
    //   - ShowAdornerOnMouseOver / InvalidateAdornerVisibility / OnTextAreaSelectionChanged →
    //     空实现 + 注释
    //   - GetChunkUnderMousePointer → 返回 null + 注释
    //   - 静态画刷字段保留（子类可能引用，Avalonia.Media 类型）
    //   - 构造函数：订阅 TextEditor 事件（PointerMoved 替代 MouseMove 等），spike 阶段
    //     事件处理器留空实现 + 注释
    public abstract class ChunkSelectionLayer<TChunk> : Control where TChunk : class
    {
        // ===== 静态画刷（Avalonia.Media，无 Freeze） =====
        // 对照 WPF: _chunkBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(65, 155, 249)), 1.0)
        protected static readonly Pen ChunkBorderPen = new Pen(new SolidColorBrush(Color.FromRgb(65, 155, 249)), 1.0);

        // 对照 WPF: _chunkBorderBrush = new SolidColorBrush(Color.FromRgb(65, 155, 249))
        protected static readonly IBrush ChunkBorderBrush = new SolidColorBrush(Color.FromRgb(65, 155, 249));

        // 对照 WPF: _chunkBackgroundBrush = new SolidColorBrush(Color.FromArgb(60, 230, 241, 255))
        protected static readonly IBrush ChunkBackgroundBrushStatic = new SolidColorBrush(Color.FromArgb(60, 230, 241, 255));

        // 对照 WPF: _chunkBackgroundBrushDark = new SolidColorBrush(Color.FromArgb(20, 53, 140, 255))
        protected static readonly IBrush ChunkBackgroundBrushDarkStatic = new SolidColorBrush(Color.FromArgb(20, 53, 140, 255));

        // 对照 WPF: protected TChunk _activeChunk;
        protected TChunk _activeChunk;

        // 对照 WPF: protected Brush ChunkBackgroundBrush;
        protected IBrush ChunkBackgroundBrush;

        // 对照 WPF: private readonly CodeEditor _textEditor;
        protected readonly CodeEditor _textEditor;

        // 对照 WPF: public virtual TChunk ActiveChunk { get; set; }
        public virtual TChunk ActiveChunk
        {
            get => _activeChunk;
            set
            {
                if (_activeChunk != value)
                {
                    _activeChunk = value;
                    InvalidateAdornerVisibility();
                    InvalidateVisual();
                }
            }
        }

        // 对照 WPF: public ChunkSelectionLayer(CodeEditor textEditor)
        public ChunkSelectionLayer(CodeEditor textEditor)
        {
            _textEditor = textEditor;
            IsHitTestVisible = false;
            RefreshBrush();

            // 对照 WPF: _textEditor.MouseEnter += TextEditor_MouseEnter;
            //           _textEditor.MouseLeave += TextEditor_MouseLeave;
            //           _textEditor.MouseMove += TextEditor_MouseMove;
            // spike 版：Avalonia 用 PointerEntered/PointerLeave/PointerMoved 替代 WPF Mouse 事件。
            // spike 阶段不订阅（事件处理器需 PointerEventArgs → 屏幕坐标转换，留 Phase 3.9b 接入）。

            // 对照 WPF: _textEditor.TextArea.SelectionChanged += TextArea_SelectionChanged;
            // spike 版：保留订阅（SelectionChanged 事件签名跨平台一致）。
            _textEditor.TextArea.SelectionChanged += TextArea_SelectionChanged;

            // 对照 WPF: _textEditor.TextChanged += TextEditor_TextChanged;
            _textEditor.TextChanged += TextEditor_TextChanged;

            // 对照 WPF: _textEditor.IsVisibleChanged += TextEditor_IsVisibleChanged;
            // spike 版：Avalonia 无 IsVisibleChanged 事件，用 EffectiveVisibleChanged 或
            // AttachedToVisualTree/DetachedFromVisualTree 替代，spike 阶段不订阅。

            // 对照 WPF: WeakEventManagerBase<TextViewWeakEventManager.ScrollOffsetChanged, TextView>.AddListener(...)
            // spike 版：AvaloniaEdit TextView 有 ScrollOffsetChanged 事件，spike 阶段不订阅。

            // 对照 WPF: WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(
            //           NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged)
            // spike 版：NotificationCenter 在 WPF 工程，Avalonia 不可访问，spike 不订阅。
        }

        // 对照 WPF: protected abstract void RefreshActiveChunk();
        protected abstract void RefreshActiveChunk();

        // 对照 WPF: protected abstract FrameworkElement CreateAdornerContent(TextEditor textEditor);
        // spike 版：返回类型改为 Avalonia.Controls.Control（FrameworkElement → Control）
        protected abstract Control CreateAdornerContent(TextEditor textEditor);

        // 对照 WPF: protected abstract Rect? GetRectForChunk(TChunk chunk);
        protected abstract Rect? GetRectForChunk(TChunk chunk);

        // 对照 WPF: protected abstract TChunk GetChunkByOffset(int offset);
        protected abstract TChunk GetChunkByOffset(int offset);

        // 对照 WPF: protected void DrawChunk(DrawingContext drawingContext, TextView textView, TChunk chunk)
        // spike 版：空实现（AdornerLayer 渲染依赖移除，留 phase 3.9b 用 Canvas 叠加层重新实现）
        protected void DrawChunk(DrawingContext drawingContext, TextView textView, TChunk chunk)
        {
            // Phase 3.9b 在此补：
            //   - GetRectForChunk(chunk) 取矩形
            //   - DrawBorder(rect, drawingContext) 画边框
            //   - ShowAdornerOnMouseOver(rect.Top + _textEditor.SearchBarHeight) 显示浮动按钮
        }

        // 对照 WPF: protected virtual void OnTextAreaSelectionChanged()
        protected virtual void OnTextAreaSelectionChanged()
        {
            RefreshActiveChunk();
            InvalidateVisual();
        }

        // 对照 WPF: protected virtual void InvalidateAdornerVisibility()
        // spike 版：空实现（AdornerLayer 移除，无可见性需要失效）
        protected virtual void InvalidateAdornerVisibility()
        {
            // Phase 3.9b 在此补：
            //   - if (_activeChunk != null || _textEditor.TextArea.Selection.Length > 0)
            //         ShowChunkAdorner(0.0); else RemoveChunkAdorner();
        }

        // 对照 WPF: protected virtual void ShowAdornerOnMouseOver(double topPosition)
        protected virtual void ShowAdornerOnMouseOver(double topPosition)
        {
            ShowChunkAdorner(topPosition);
        }

        // 对照 WPF: protected void ShowChunkAdorner(double popupTopPosition)
        // spike 版：空实现（AdornerLayer 创建逻辑移除，留 phase 3.9b 用 Popup/Canvas 实现）
        protected void ShowChunkAdorner(double popupTopPosition)
        {
            // Phase 3.9b 在此补：
            //   - 创建 Popup 或 Canvas 叠加层
            //   - CreateAdornerContent(_textEditor) 设置内容
            //   - 设置位置（popupTopPosition + SearchBarHeight 偏移）
        }

        // 对照 WPF: protected void RemoveChunkAdorner()
        // spike 版：空实现（无 AdornerLayer 需要移除）
        protected void RemoveChunkAdorner()
        {
            // Phase 3.9b 在此补：移除 Popup/Canvas 叠加层
        }

        // 对照 WPF: private void TextEditor_TextChanged(object sender, EventArgs e)
        private void TextEditor_TextChanged(object sender, System.EventArgs e)
        {
            ActiveChunk = null;
        }

        // 对照 WPF: private void TextArea_SelectionChanged(object sender, EventArgs e)
        private void TextArea_SelectionChanged(object sender, System.EventArgs e)
        {
            OnTextAreaSelectionChanged();
        }

        // 对照 WPF: private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
        // spike 版：保留方法签名（NotificationCenter 订阅移除，但子类可能直接调用）
        protected void ApplicationThemeChanged(object sender, System.EventArgs e)
        {
            RefreshBrush();
        }

        // 对照 WPF: private void RefreshBrush()
        private void RefreshBrush()
        {
            // 优先读资源（CustomColorsDialog 覆盖或主题字典），取不到回退到 light/dark 静态画刷。
            ChunkBackgroundBrush = TryFindColorBrush("ChunkSelection.BackgroundColor")
                ?? (ForkPlusSettings.Default.Theme.IsDarkBase() ? ChunkBackgroundBrushDarkStatic : ChunkBackgroundBrushStatic);
        }

        private static Color? TryFindColor(string key)
        {
            var app = global::Avalonia.Application.Current;
            if (app != null && app.TryGetResource(key, null, out var res))
            {
                if (res is Color c) return c;
                if (res is ISolidColorBrush b) return b.Color;
            }
            return null;
        }

        private static IBrush TryFindColorBrush(string key)
        {
            Color? c = TryFindColor(key);
            return c.HasValue ? new SolidColorBrush(c.Value) : null;
        }

        // 对照 WPF: protected void DrawSelectionBorder(DrawingContext drawingContext, TextArea textArea)
        // spike 版：空实现（BackgroundGeometryBuilder 渲染留 phase 3.9b）
        protected void DrawSelectionBorder(DrawingContext drawingContext, TextArea textArea)
        {
            // Phase 3.9b 在此补：
            //   - textArea.Selection.SurroundingSegment 取选区
            //   - BackgroundGeometryBuilder.GetRectsForSegment 遍历矩形
            //   - DrawBorder(rect, drawingContext) 画边框
            //   - ShowChunkAdorner(top + SearchBarHeight) 显示浮动按钮
        }

        // 对照 WPF: protected virtual void DrawBorder(Rect rect, DrawingContext drawingContext)
        // spike 版：空实现（RectangleGeometry + DrawGeometry 留 phase 3.9b）
        protected virtual void DrawBorder(Rect rect, DrawingContext drawingContext)
        {
            // Phase 3.9b 在此补：
            //   - var geometry = new RectangleGeometry(new Rect(rect.X+2, rect.Y, rect.Width, rect.Height), 2, 2);
            //   - drawingContext.DrawGeometry(ChunkBackgroundBrush, ChunkBorderPen, geometry);
            //   （Avalonia DrawGeometry 参数顺序：brush, pen, geometry）
        }

        // 对照 WPF: protected Geometry CreateSelectionGeometry(TextArea textArea)
        // spike 版：返回 null（BackgroundGeometryBuilder 留 phase 3.9b）
        protected Geometry CreateSelectionGeometry(TextArea textArea)
        {
            // Phase 3.9b 在此补：BackgroundGeometryBuilder + SelectionSegment → Geometry
            return null;
        }

        // 对照 WPF: protected Rect CreateLineBlockRect(VisualLine topVisualLine, int lineCount)
        // spike 版：返回 default(Rect)（VisualLine 高度计算留 phase 3.9b）
        protected Rect CreateLineBlockRect(VisualLine topVisualLine, int lineCount)
        {
            // Phase 3.9b 在此补：
            //   - textView = _textEditor.TextArea.TextView
            //   - top = topVisualLine.VisualTop - textView.ScrollOffset.Y
            //   - height = sum of GetVisualLine(i).Height for i in [lineNumber, lineNumber+lineCount)
            //   - return new Rect(0, top+1, textView.Bounds.Width, height-1)
            return default(Rect);
        }

        // 对照 WPF: protected TChunk GetChunkUnderMousePointer()
        // spike 版：返回 null（Mouse.GetPosition + HitTest 留 phase 3.9b）
        protected TChunk GetChunkUnderMousePointer()
        {
            // Phase 3.9b 在此补：
            //   - PointerEventArgs.GetPosition(_textEditor) 取鼠标坐标
            //   - _textEditor.InputHitTest(position) 命中测试
            //   - _textEditor.GetPositionFromPoint(position) 取 TextViewPosition
            //   - _textEditor.Document.GetOffset(location) 取 offset
            //   - return GetChunkByOffset(offset)
            return null;
        }
    }
}
