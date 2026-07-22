using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI;

namespace ForkPlus.Avalonia.Controls.Editor
{
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/Editor/CodeEditorLineNumberMargin.cs（113 行）：
    //   - internal class CodeEditorLineNumberMargin : ClearTypeLineNumberMargin
    //   - 静态字段：_typeface（Consolas + Courier New fallback）/ _lightBrush / _darkBrush /
    //     _separatorPenLight / _separatorPenDark / HorizontalMargin
    //   - 实例字段：_brush / _separatorPen / _lineNumberLength
    //   - 构造函数：设置 typeface/emSize（基类 LineNumberMargin 的 protected 字段）+
    //     RefreshBrushes + 订阅 NotificationCenter.ApplicationThemeChanged +
    //     RenderOptions.SetClearTypeHint
    //   - UpdateLineNumbersData：根据 Document.LineCount 更新 _lineNumberLength + InvalidateMeasure
    //   - MeasureOverride：根据 _lineNumberLength 计算宽度
    //   - OnRender：base.OnRender 画背景 + 遍历 VisualLines 画行号文本 + 画分隔线
    //   - RefreshBrushes：优先读资源（LineNumber.ForegroundColor / LineNumber.SeparatorColor），
    //     回退到 light/dark 静态画刷
    //   - CreateFormattedText：用 typeface/emSize/_brush 创建 FormattedText
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. WPF Typeface(FontFamily, FontStyle, FontWeight, FontStretch, FontFamily fallback) →
    //      Avalonia Typeface(FontFamily, FontStyle, FontWeight, FontStretch)（无 fallback 参数）
    //      spike 用 FontConstants.MonospaceFontFamily（与 GitOutputColorizer 一致）
    //   2. WPF 基类 LineNumberMargin 的 protected typeface/emSize 字段 →
    //      AvaloniaEdit LineNumberMargin 无此字段（API 不同），spike 自建 _typeface/_emSize
    //   3. WPF OnRender(DrawingContext) → Avalonia Render(DrawingContext)
    //   4. WPF FormattedText(text, culture, FlowDirection, typeface, emSize, brush, pixelsPerDip) →
    //      Avalonia FormattedText(text, culture, FlowDirection, typeface, emSize, brush)
    //      （Avalonia 无 pixelsPerDip 参数）
    //   5. WPF base.RenderSize.Width/Height → Avalonia Bounds.Width/Bounds.Height
    //   6. WPF RenderOptions.SetClearTypeHint → spike 移除（WPF-only ClearType 优化）
    //   7. WPF brush.Freeze() → 删除（Avalonia Brush immutable）
    //   8. WPF NotificationCenter.ApplicationThemeChanged → spike 移除（不可访问）
    //   9. WPF base.TextView.VisualLines / VerticalOffset → AvaloniaEdit 同名 API
    //  10. namespace 改为 ForkPlus.Avalonia.Controls.Editor
    //
    // spike 简化（task spec：复杂渲染逻辑可简化为空实现 + 注释）：
    //   - 保留静态画刷字段 + RefreshBrushes（主题感知，子类可能引用）
    //   - 保留 UpdateLineNumbersData（行号宽度计算，逻辑简单）
    //   - Render：调 base.Render() 画背景 + 画分隔线（行号文本由基类 LineNumberMargin 渲染，
    //     spike 不重复画自定义行号文本，留 phase 3.9b 接入 FormattedText 自定义渲染）
    //   - MeasureOverride：调 base.MeasureOverride（基类已按 MaxLineNumberLength 计算宽度）
    //   - ApplicationThemeChanged：保留方法签名（子类可能调用），spike 不订阅 NotificationCenter
    internal class CodeEditorLineNumberMargin : ClearTypeLineNumberMargin
    {
        // ===== 静态画刷（Avalonia.Media，无 Freeze） =====
        // 对照 WPF: _lightBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192))
        private static readonly IBrush LightBrush = new SolidColorBrush(Color.FromRgb(192, 192, 192));

        // 对照 WPF: _darkBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160))
        private static readonly IBrush DarkBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));

        // 对照 WPF: _separatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0)
        private static readonly Pen SeparatorPenLight = new Pen(new SolidColorBrush(Color.FromRgb(218, 218, 215)), 1.0);

        // 对照 WPF: _separatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0)
        private static readonly Pen SeparatorPenDark = new Pen(new SolidColorBrush(Color.FromRgb(110, 110, 110)), 1.0);

        // 对照 WPF: private static readonly double HorizontalMargin = 5.0;
        private const double HorizontalMargin = 5.0;

        // 对照 WPF: private Brush _brush;
        private IBrush _brush;

        // 对照 WPF: private Pen _separatorPen;
        private Pen _separatorPen;

        // 对照 WPF: private int _lineNumberLength = 2;
        private int _lineNumberLength = 2;

        // spike 自建 typeface/emSize（AvaloniaEdit LineNumberMargin 无基类 protected 字段）
        private static readonly Typeface LineNumberTypeface = new Typeface(
            FontConstants.MonospaceFontFamily, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
        private const double EmSize = 11.0;

        public CodeEditorLineNumberMargin()
        {
            RefreshBrushes();
            // 对照 WPF: RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
            // spike 移除（WPF-only ClearType 优化，Avalonia 跨平台默认渲染）
            // 对照 WPF: WeakEventManager<NotificationCenter, ...>.AddHandler(...);
            // spike 移除（NotificationCenter 不可访问）
        }

        // 对照 WPF: public void UpdateLineNumbersData()
        public void UpdateLineNumbersData()
        {
            int num = Math.Max(_lineNumberLength, Document?.LineCount.ToString().Length ?? 1);
            if (num != _lineNumberLength)
            {
                _lineNumberLength = num;
                InvalidateMeasure();
            }
        }

        // 对照 WPF: protected override Size MeasureOverride(Size availableSize)
        // spike 版：调基类 MeasureOverride（AvaloniaEdit LineNumberMargin 已按 MaxLineNumberLength 计算宽度）
        protected override Size MeasureOverride(Size availableSize)
        {
            return base.MeasureOverride(availableSize);
        }

        // 对照 WPF: protected override void OnRender(DrawingContext drawingContext)
        // Avalonia: public override void Render(DrawingContext drawingContext)
        public override void Render(DrawingContext drawingContext)
        {
            // 对照 WPF: base.OnRender(drawingContext);
            // spike 版：先调基类 Render 画背景 + 行号（AvaloniaEdit 内置行号渲染）
            base.Render(drawingContext);

            // 对照 WPF: drawingContext.DrawLine(_separatorPen,
            //           new Point(RenderSize.Width - HorizontalMargin, 0),
            //           new Point(RenderSize.Width - HorizontalMargin, RenderSize.Height));
            // spike 版：画右侧分隔线（与 WPF 行为一致）
            double x = Bounds.Width - HorizontalMargin;
            drawingContext.DrawLine(_separatorPen, new Point(x, 0.0), new Point(x, Bounds.Height));

            // Phase 3.9b 在此补：遍历 TextView.VisualLines 用 FormattedText 画自定义行号文本
            // （spike 阶段行号由基类 LineNumberMargin 渲染，不重复画）
        }

        // 对照 WPF: private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
        // spike 版：保留方法签名（子类可能调用），不订阅 NotificationCenter
        protected void ApplicationThemeChanged(object sender, System.EventArgs e)
        {
            RefreshBrushes();
        }

        // 对照 WPF: private void RefreshBrushes()
        private void RefreshBrushes()
        {
            // 优先读资源（CustomColorsDialog 覆盖或主题字典），取不到回退到 light/dark 静态画刷。
            _brush = TryFindColorBrush("LineNumber.ForegroundColor")
                ?? (ForkPlusSettings.Default.Theme.IsDarkBase() ? DarkBrush : LightBrush);
            Color? sepColor = TryFindColor("LineNumber.SeparatorColor");
            _separatorPen = sepColor.HasValue
                ? new Pen(new SolidColorBrush(sepColor.Value), 1.0)
                : (ForkPlusSettings.Default.Theme.IsDarkBase() ? SeparatorPenDark : SeparatorPenLight);
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

        // 对照 WPF: private FormattedText CreateFormattedText(string text)
        // spike 版：保留方法（phase 3.9b 自定义行号渲染时使用）
        private static FormattedText CreateFormattedText(string text)
        {
            // Avalonia FormattedText 无 pixelsPerDip 参数（与 WPF 不同）
            return new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.RightToLeft, LineNumberTypeface, EmSize, LightBrush);
        }
    }
}
