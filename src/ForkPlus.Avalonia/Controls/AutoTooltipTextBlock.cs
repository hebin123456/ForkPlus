using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 AutoTooltipTextBlock（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/AutoTooltipTextBlock.cs（48 行）：
    //   - WPF AutoTooltipTextBlock : TextBlock
    //   - CustomToolTip DependencyProperty（string）
    //   - 构造函数：TextTrimming = CharacterEllipsis + ToolTip = ""
    //   - ToolTipOpening 事件处理：
    //     - CustomToolTip != null → ToolTip = CustomToolTip
    //     - else if TextIsTrimmed() → ToolTip = Text
    //     - else → e.Handled = true（不显示 tooltip）
    //   - TextIsTrimmed()：Measure(infinite) + ActualWidth < DesiredSize.Width
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextBlock + 检测文本截断）：
    //   1. 基类 TextBlock → Avalonia.Controls.TextBlock（API 一致）
    //   2. DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF TextTrimming.CharacterEllipsis → Avalonia TextTrimming.CharacterEllipsis（API 一致）
    //   4. WPF ToolTipOpening 事件 → Avalonia 无对应事件，spike 改用 OnPropertyChanged +
    //      LayoutUpdated 实时检测：当 CustomToolTip 非空或文本截断时设置 ToolTip
    //   5. WPF TextIsTrimmed() 用 Measure + ActualWidth/DesiredSize 对比 →
    //      Avalonia 11 spike 用 Bounds.Width 与 desired size 对比（Measure(infinite)）
    //   6. spike 跳过 ToolTipOpening e.Handled = true 路径（Avalonia ToolTip 自动管理显示）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TextBlock + CustomToolTip StyledProperty
    //   - LayoutUpdated 实时检测文本截断
    //   - 截断或 CustomToolTip 非空时设置 ToolTip
    public class AutoTooltipTextBlock : TextBlock
    {
        // 对照 WPF: CustomToolTipProperty (string)
        public static readonly StyledProperty<string> CustomToolTipProperty =
            AvaloniaProperty.Register<AutoTooltipTextBlock, string>(nameof(CustomToolTip));

        public string CustomToolTip
        {
            get => GetValue(CustomToolTipProperty);
            set => SetValue(CustomToolTipProperty, value);
        }

        public AutoTooltipTextBlock()
        {
            // 对照 WPF: base.TextTrimming = TextTrimming.CharacterEllipsis
            TextTrimming = TextTrimming.CharacterEllipsis;
            // 对照 WPF: base.ToolTip = ""
            ToolTip.SetTip(this, string.Empty);

            // spike 版：LayoutUpdated 时检测文本截断
            LayoutUpdated += AutoTooltipTextBlock_LayoutUpdated;
        }

        // spike 版：替代 WPF ToolTipOpening 事件
        //   - CustomToolTip 非空 → ToolTip = CustomToolTip
        //   - else if 文本截断 → ToolTip = Text
        //   - else → ToolTip = null（不显示）
        private void AutoTooltipTextBlock_LayoutUpdated(object sender, EventArgs e)
        {
            string customToolTip = CustomToolTip;
            if (!string.IsNullOrEmpty(customToolTip))
            {
                ToolTip.SetTip(this, customToolTip);
            }
            else if (TextIsTrimmed())
            {
                ToolTip.SetTip(this, Text);
            }
            else
            {
                ToolTip.SetTip(this, null);
            }
        }

        // 对照 WPF: private bool TextIsTrimmed()
        //   Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        //   return base.ActualWidth < base.DesiredSize.Width;
        // spike 版：用 Bounds.Width 替代 ActualWidth（Avalonia 11 无 ActualWidth）
        private bool TextIsTrimmed()
        {
            Measure(global::Avalonia.Size.Infinity);
            double desiredWidth = DesiredSize.Width;
            return Bounds.Width < desiredWidth;
        }

        // Avalonia 11：CustomToolTip 变化时立即更新 ToolTip
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == CustomToolTipProperty || change.Property == TextProperty)
            {
                AutoTooltipTextBlock_LayoutUpdated(this, EventArgs.Empty);
            }
        }
    }
}
