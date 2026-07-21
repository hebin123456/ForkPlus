using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 RevisionSubjectTextField（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/RevisionSubjectTextField.cs（159 行）：
    //   - WPF RevisionSubjectTextField : TextField
    //   - IsParentSelectedProperty + HasBodyProperty (bool) DependencyProperty
    //   - RefreshInlines override：复杂 Inlines 渲染
    //     - GetPrefixHighlighting：[xxx] 或 prefix: 模式加粗
    //     - GetCodeHighlighting：`code` 反引号代码块
    //     - GetSearchMatchRanges：搜索高亮（继承自 TextField）
    //     - Range.Merge 三路区间合并 + Run.Foreground/Background/FontFamily 设置
    //     - HasBody 时追加 " ↩" 图标（Foreground 随 IsParentSelected 变化）
    //   - GetPrefixHighlighting / GetCodeHighlighting / FindCodeBlock：纯 C# 算法
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextField + 跳过 Range.Merge）：
    //   1. WPF TextField 基类（已迁移到 ForkPlus.Avalonia.Controls.TextField spike）
    //      → spike 继承 TextField（保持与 WPF 一致）
    //   2. WPF DependencyProperty.RegisterAttached → StyledProperty<T>.Register
    //   3. WPF Inlines + Run + Background/Foreground + FontWeight + FontFamily
    //      → spike 简化为 Text 显示（Avalonia 11 TextBox 不支持 Inlines 段落渲染）
    //      spike 版 RefreshInlines 仅设置 Text = StringValue + （HasBody 时追加 " ↩"）
    //   4. WPF Range.Merge 三路区间合并 → spike 跳过（无 Inlines 支持）
    //   5. WPF Theme.FindBrush → spike 跳过（Theme 在 WPF 工程）
    //   6. WPF FontConstants.MonospaceFontFamily → spike 跳过
    //   7. spike 保留 IsParentSelected / HasBody StyledProperty<bool>（API 形状）
    //   8. spike 保留 GetPrefixHighlighting / GetCodeHighlighting / FindCodeBlock
    //      纯 C# 算法移植（虽然 Inlines 渲染跳过，但 API 形状保留供调用方使用）
    //   9. spike 用 (int Start, int End) 元组替代 WPF Range 类型
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TextField（spike）
    //   - IsParentSelected / HasBody StyledProperty<bool>
    //   - RefreshInlines override（spike 简化为 Text = StringValue + " ↩"）
    //   - GetPrefixHighlighting / GetCodeHighlighting / FindCodeBlock 保留算法
    public class RevisionSubjectTextField : TextField
    {
        // 对照 WPF: public static readonly DependencyProperty IsParentSelectedProperty
        //   = DependencyProperty.RegisterAttached("IsParentSelected", typeof(bool), ...)
        // spike 版：StyledProperty<bool>
        public static readonly StyledProperty<bool> IsParentSelectedProperty =
            AvaloniaProperty.Register<RevisionSubjectTextField, bool>(nameof(IsParentSelected));

        // 对照 WPF: public static readonly DependencyProperty HasBodyProperty
        //   = DependencyProperty.RegisterAttached("HasBody", typeof(bool), ...)
        // spike 版：StyledProperty<bool>
        public static readonly StyledProperty<bool> HasBodyProperty =
            AvaloniaProperty.Register<RevisionSubjectTextField, bool>(nameof(HasBody));

        // 对照 WPF: public bool IsParentSelected { get; set; }
        public bool IsParentSelected
        {
            get => GetValue(IsParentSelectedProperty);
            set => SetValue(IsParentSelectedProperty, value);
        }

        // 对照 WPF: public bool HasBody { get; set; }
        public bool HasBody
        {
            get => GetValue(HasBodyProperty);
            set => SetValue(HasBodyProperty, value);
        }

        public RevisionSubjectTextField()
        {
        }

        // 对照 WPF: PropertyMetadata delegate → (s as RevisionSubjectTextField).RefreshInlines()
        // spike 版：通过 OnPropertyChanged 触发 RefreshInlines（IsParentSelected / HasBody 变化时）
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsParentSelectedProperty || change.Property == HasBodyProperty)
            {
                RefreshInlines();
            }
        }

        // 对照 WPF: protected override void RefreshInlines()
        // spike 版简化：跳过 Inlines + Range.Merge，直接设置 Text
        protected override void RefreshInlines()
        {
            string stringValue = StringValue ?? string.Empty;
            if (string.IsNullOrEmpty(stringValue))
            {
                Text = string.Empty;
                return;
            }
            // 对照 WPF: HasBody 时追加 " ↩" 图标
            //   WPF: Run run = new Run(" ↩");
            //        run.FontSize = 10.0;
            //        run.Foreground = (IsParentSelected ?
            //          Theme.FindBrush("RevisionList.BodyIndicator.Selected.ForegroundBrush") :
            //          Theme.FindBrush("RevisionList.BodyIndicator.ForegroundBrush"));
            //        base.Inlines.Add(run);
            // spike 版：直接拼接为 Text（无 Inlines 段落渲染，无 Theme 画刷）
            if (HasBody)
            {
                Text = stringValue + " ↩";
            }
            else
            {
                Text = stringValue;
            }
            // 对照 WPF: GetPrefixHighlighting + GetCodeHighlighting + GetSearchMatchRanges
            //   + Range.Merge + Inlines.Add(Run)
            // spike 版跳过：Avalonia 11 TextBox 不支持 Inlines 段落渲染
            // 调用方可通过 GetPrefixHighlighting / GetCodeHighlighting 获取区间列表
            // 自行通过 SelectableTextBlock + Inlines 渲染
        }

        // 对照 WPF: private static List<Range> GetPrefixHighlighting(string stringValue)
        // spike 版改为公共静态方法，调用方可使用
        // 用 (int, int) 元组替代 WPF Range 类型
        public static List<(int Start, int End)> GetPrefixHighlighting(string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
            {
                return new List<(int, int)>();
            }
            if (stringValue.StartsWith("["))
            {
                int num = stringValue.IndexOf(']');
                if (num != -1)
                {
                    return new List<(int, int)> { (0, num + 1) };
                }
            }
            int num2 = stringValue.IndexOf(' ');
            if (num2 == -1 || num2 == 0)
            {
                return new List<(int, int)>();
            }
            int num3 = stringValue.IndexOf(':', 1, num2);
            if (num3 == -1)
            {
                return new List<(int, int)>();
            }
            return new List<(int, int)> { (0, num3) };
        }

        // 对照 WPF: private static List<Range> GetCodeHighlighting(string stringValue)
        // spike 版改为公共静态方法，调用方可使用
        public static List<(int Start, int End)> GetCodeHighlighting(string stringValue)
        {
            if (string.IsNullOrEmpty(stringValue))
            {
                return new List<(int, int)>();
            }
            (int Start, int End)? range = FindCodeBlock(stringValue, 0);
            List<(int, int)> list = new List<(int, int)>();
            while (range.HasValue)
            {
                var valueOrDefault = range.Value;
                list.Add(valueOrDefault);
                int num = valueOrDefault.End + 1;
                if (num >= stringValue.Length)
                {
                    break;
                }
                range = FindCodeBlock(stringValue, num);
            }
            return list;
        }

        // 对照 WPF: private static Range? FindCodeBlock(string str, int start)
        // spike 版用 (int, int)? 替代 Range?
        private static (int Start, int End)? FindCodeBlock(string str, int start)
        {
            int num = str.IndexOf('`', start);
            if (num == -1)
            {
                return null;
            }
            int num2 = str.IndexOf('`', num + 1);
            if (num2 == -1)
            {
                return null;
            }
            return (num, num2 + 1);
        }
    }
}
