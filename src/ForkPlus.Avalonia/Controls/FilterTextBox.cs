using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 FilterTextBox（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/FilterTextBox.cs（220 行）：
    //   - WPF FilterTextBox : PlaceholderTextBox（自定义 WPF 基类，有 Placeholder 属性）
    //   - [TemplatePart] PART_ClearButton (FrameworkElement) — 清除按钮
    //   - [TemplatePart] PART_TranslateTransform (TranslateTransform) — 滑入动画
    //   - [TemplatePart] PART_DropDownButton (DropDownButton) — 下拉按钮
    //   - 4 个 DependencyProperty：AnimationPlaceholder (Grid) /
    //     UseSecondaryTextBoxBackground (bool) / ShowDropdown (bool) / Hint (string)
    //   - 3 个事件：FilterRequestChanged / DropdownContextMenuOpened / ClearButtonClicked
    //   - 构造函数：PreviewKeyDown (Key.Down → 打开 dropdown) + KeyDown (Escape → Clear)
    //     + TextChanged → FilterRequestChanged
    //   - OnApplyTemplate: 获取 PART_*，订阅 ClearButton.Click / DropdownButton 事件
    //   - ShowWithAnimation / HideWithAnimation：SlidingPanelHelper + DoubleAnimation 透明度
    //   - FocusAndSelectAllText: SelectAll + Focus
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 PlaceholderTextBox → Avalonia TextBox（内置 Watermark 属性，等价 WPF Placeholder）
    //   2. DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF PreviewKeyDown (tunneling) → Avalonia KeyDown (bubble)
    //      （Avalonia 没有 WPF 的 Preview tunneling 事件，用 KeyDown + Handled 替代）
    //   4. WPF Key.Escape / Key.Down → Avalonia Key.Escape / Key.Down（Avalonia.Input.Key 枚举名一致）
    //   5. spike 跳过 [TemplatePart] + OnApplyTemplate（Avalonia TextBox 已内置清除按钮逻辑，
    //      spike 不实现自定义 TemplatePart）
    //   6. spike 跳过 ShowWithAnimation / HideWithAnimation（依赖 SlidingPanelHelper WPF 工具类，
    //      Phase 3.x 迁移；spike 用 IsVisible 切换实现简化的显隐）
    //   7. spike 跳过 DropDownButton（WPF 自定义控件，未迁移）
    //   8. Hint 属性 → 映射到 Avalonia TextBox.Watermark（内置 placeholder）
    //
    // spike 简化：
    //   - 继承 TextBox + Hint StyledProperty（setter 同步到 Watermark）
    //   - TextChanged 事件转发到 FilterRequestChanged
    //   - Escape 键清空文本
    //   - Clear() 方法（已在 TextBox 基类提供，spike 版包装一下）
    public class FilterTextBox : TextBox
    {
        // 对照 WPF: HintProperty (string)
        // spike 版：Hint 设置时同步到 Avalonia TextBox.Watermark（内置 placeholder）
        public static readonly StyledProperty<string> HintProperty =
            AvaloniaProperty.Register<FilterTextBox, string>(nameof(Hint));

        // 对照 WPF: UseSecondaryTextBoxBackgroundProperty (bool, default false)
        // spike 版：保留接口契约，不应用主题画刷（Theme 未迁移）
        public static readonly StyledProperty<bool> UseSecondaryTextBoxBackgroundProperty =
            AvaloniaProperty.Register<FilterTextBox, bool>(nameof(UseSecondaryTextBoxBackground));

        // 对照 WPF: ShowDropdownProperty (bool, default false)
        // spike 版：保留接口契约，不实际显示 dropdown button
        public static readonly StyledProperty<bool> ShowDropdownProperty =
            AvaloniaProperty.Register<FilterTextBox, bool>(nameof(ShowDropdown));

        // 对照 WPF: AnimationPlaceholderProperty (Grid)
        // spike 版：保留接口契约，spike 不实现滑动动画
        public static readonly StyledProperty<Grid> AnimationPlaceholderProperty =
            AvaloniaProperty.Register<FilterTextBox, Grid>(nameof(AnimationPlaceholder));

        // 对照 WPF: public string FilterRequest => base.Text
        public string FilterRequest => Text;

        // 对照 WPF: public bool IsAnimationPlaceholderVisible { get; private set; }
        public bool IsAnimationPlaceholderVisible { get; private set; }

        // 对照 WPF: 3 个事件
        public event EventHandler FilterRequestChanged;
        public event EventHandler DropdownContextMenuOpened;
        public event EventHandler ClearButtonClicked;

        public string Hint
        {
            get => GetValue(HintProperty);
            set => SetValue(HintProperty, value);
        }

        public bool UseSecondaryTextBoxBackground
        {
            get => GetValue(UseSecondaryTextBoxBackgroundProperty);
            set => SetValue(UseSecondaryTextBoxBackgroundProperty, value);
        }

        public bool ShowDropdown
        {
            get => GetValue(ShowDropdownProperty);
            set => SetValue(ShowDropdownProperty, value);
        }

        public Grid AnimationPlaceholder
        {
            get => GetValue(AnimationPlaceholderProperty);
            set => SetValue(AnimationPlaceholderProperty, value);
        }

        public FilterTextBox()
        {
            // 对照 WPF: PreviewKeyDown (Key.Down → _dropdownButton.IsChecked = true)
            // spike 版：KeyDown 替代 PreviewKeyDown（Avalonia 无 tunneling）
            KeyDown += (s, e) =>
            {
                // 对照 WPF: Key.Escape → Clear() + e.Handled = true
                if (e.Key == Key.Escape && !string.IsNullOrEmpty(Text))
                {
                    Clear();
                    e.Handled = true;
                }
            };

            // 对照 WPF: TextChanged → FilterRequestChanged?.Invoke(this, EventArgs.Empty)
            TextChanged += (s, e) =>
            {
                FilterRequestChanged?.Invoke(this, EventArgs.Empty);
            };
        }

        // 对照 WPF: public override void OnApplyTemplate()
        // spike 版：Hint 变化时同步到 Watermark（Avalonia TextBox 内置 placeholder）
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == HintProperty)
            {
                // Hint → Watermark 同步（spike 简化：用 Avalonia 内置 Watermark 替代 WPF Placeholder）
                Watermark = Hint;
            }
        }

        // 对照 WPF: public void FocusAndSelectAllText()
        public void FocusAndSelectAllText()
        {
            SelectAll();
            Focus();
        }

        // 对照 WPF: public void ShowWithAnimation()
        // spike 版简化：直接显示 + Clear + Focus（跳过 SlidingPanelHelper 动画）
        public void ShowWithAnimation()
        {
            if (AnimationPlaceholder != null)
            {
                Clear();
                FocusAndSelectAllText();
                IsAnimationPlaceholderVisible = true;
            }
        }

        // 对照 WPF: public void HideWithAnimation()
        // spike 版简化：直接隐藏（跳过 SlidingPanelHelper 动画）
        public void HideWithAnimation()
        {
            if (AnimationPlaceholder != null && IsAnimationPlaceholderVisible)
            {
                Clear();
                IsAnimationPlaceholderVisible = false;
            }
        }
    }
}
