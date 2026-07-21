using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 TextField（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/TextField.cs（103 行）：
    //   - WPF TextField : TextBlock
    //   - StringValueProperty + HighlightPatternProperty (string) DependencyProperty
    //     （注：WPF 字段名 HighlightPatternProperty，注册名 "HighlightString"）
    //   - RefreshInlines virtual：用 Inlines 渲染
    //     - 无高亮：Inlines.Add(new Run(stringValue))
    //     - 有高亮：GetSearchMatchRanges + Range.Merge + Run.Background/Foreground
    //   - GetSearchMatchRanges：纯 C# 子串匹配算法（大小写不敏感）
    //   - Empty 静态字段（List<Range>）
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextBox + 跳过 Inlines）：
    //   1. WPF TextBlock 基类 → spike 继承 TextBox
    //      （task spec 明确要求：继承 TextBox；
    //       Avalonia 11 TextBox 不支持 Inlines 段落渲染，spike 用 Text 直接显示）
    //   2. WPF DependencyProperty.RegisterAttached → StyledProperty<T>.Register
    //   3. WPF Inlines + Run + Background/Foreground → spike 简化为 Text 显示
    //      spike 版 RefreshInlines 仅设置 Text = StringValue（无高亮渲染）
    //   4. WPF Range.Merge 区间合并 → spike 跳过（无 Inlines 支持）
    //   5. WPF Theme.FindBrush → spike 跳过（Theme 在 WPF 工程）
    //   6. spike 保留 StringValue / HighlightString StyledProperty<string>（API 形状）
    //   7. spike 保留 GetSearchMatchRanges 静态算法（纯 C# 逻辑）
    //   8. spike 保留 Empty 静态字段（List<(int,int)> 类型，替代 WPF List<Range>）
    //   9. spike 用 (int Start, int End) 元组替代 WPF Range 类型
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TextBox（IsReadOnly = true，仅显示）
    //   - StringValue / HighlightString StyledProperty<string>
    //   - RefreshInlines virtual（spike 简化为 Text = StringValue）
    //   - GetSearchMatchRanges 静态方法保留算法
    public class TextField : TextBox
    {
        // 对照 WPF: protected static readonly List<Range> Empty = new List<Range>();
        // spike 版：用 (int Start, int End) 元组替代 WPF Range 类型
        protected static readonly List<(int Start, int End)> Empty = new List<(int Start, int End)>();

        // 对照 WPF: public static readonly DependencyProperty StringValueProperty
        //   = DependencyProperty.RegisterAttached("StringValue", typeof(string), ...)
        // spike 版：StyledProperty<string>
        public static readonly StyledProperty<string> StringValueProperty =
            AvaloniaProperty.Register<TextField, string>(nameof(StringValue));

        // 对照 WPF: public static readonly DependencyProperty HighlightPatternProperty
        //   = DependencyProperty.RegisterAttached("HighlightString", typeof(string), ...)
        // spike 版：StyledProperty<string>（命名统一为 HighlightStringProperty）
        public static readonly StyledProperty<string> HighlightStringProperty =
            AvaloniaProperty.Register<TextField, string>(nameof(HighlightString));

        // 对照 WPF: public string StringValue { get; set; }
        public string StringValue
        {
            get => GetValue(StringValueProperty);
            set => SetValue(StringValueProperty, value);
        }

        // 对照 WPF: public string HighlightString { get; set; }
        public string HighlightString
        {
            get => GetValue(HighlightStringProperty);
            set => SetValue(HighlightStringProperty, value);
        }

        public TextField()
        {
            // spike 版：设置为只读，仅显示（对照 WPF TextField 是 TextBlock 显示控件）
            IsReadOnly = true;
        }

        // 对照 WPF: PropertyMetadata delegate → (s as TextField).RefreshInlines()
        // spike 版：通过 OnPropertyChanged 触发 RefreshInlines
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == StringValueProperty || change.Property == HighlightStringProperty)
            {
                RefreshInlines();
            }
        }

        // 对照 WPF: protected virtual void RefreshInlines()
        // spike 版简化：跳过 Inlines + Range.Merge，直接设置 Text = StringValue
        protected virtual void RefreshInlines()
        {
            string stringValue = StringValue;
            if (string.IsNullOrEmpty(stringValue))
            {
                Text = string.Empty;
                return;
            }
            // 对照 WPF: 高亮匹配 → Inlines + Run.Background/Foreground
            // spike 版跳过：Avalonia 11 TextBox 不支持 Inlines 段落渲染
            // 直接显示 StringValue 原文（高亮渲染由调用方通过 SelectableTextBlock + Inlines 自行实现）
            Text = stringValue;
        }

        // 对照 WPF: protected static List<Range> GetSearchMatchRanges(string stringValue, [Null] string highlightString)
        // spike 版：用 (int, int) 元组替代 Range，算法完整移植
        public static List<(int Start, int End)> GetSearchMatchRanges(string stringValue, string highlightString)
        {
            if (string.IsNullOrEmpty(stringValue) || string.IsNullOrEmpty(highlightString))
            {
                return Empty;
            }
            int num = stringValue.IndexOf(highlightString, StringComparison.OrdinalIgnoreCase);
            if (num == -1)
            {
                return Empty;
            }
            List<(int Start, int End)> list = new List<(int Start, int End)>();
            int length = highlightString.Length;
            while (num != -1)
            {
                int num2 = num + length;
                list.Add((num, num2));
                num = stringValue.IndexOf(highlightString, num2, StringComparison.OrdinalIgnoreCase);
            }
            return list;
        }
    }
}
