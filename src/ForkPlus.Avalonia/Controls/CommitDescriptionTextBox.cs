using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 CommitDescriptionTextBox（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/CommitDescriptionTextBox.cs（106 行）：
    //   - WPF CommitDescriptionTextBox : SpellingPlaceholderTextBox
    //   - GuideLineMarginProperty (Thickness) DependencyProperty
    //   - 构造函数：NotificationCenter.PageGuideLinePositionChanged 弱事件订阅
    //     + Loaded → RefreshGuideLine()
    //   - GetContextMenu override：ContextMenu + AddDefaultTextBoxMenuItems(this) +
    //     Separator + MenuItem "Wrap Paragraph at Ruler" → Text = WrapString(Text, width)
    //   - WrapString：纯 C# 段落换行算法（按 width 字符宽度插入换行符，按段落双换行分割）
    //   - RefreshGuideLine：TextGuidelineHelper.GuideLinePosition(this, ...) → GuideLineMargin
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 TextBox，跳过 SpellCheck/TextGuidelineHelper）：
    //   1. WPF SpellingPlaceholderTextBox 基类 → spike 直接继承 TextBox
    //      （task spec 明确要求：继承 TextBox，跳过 SpellCheck 和 TextGuidelineHelper）
    //   2. WPF DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF NotificationCenter 弱事件订阅 → spike 跳过
    //      （NotificationCenter 在 WPF 工程 ForkPlus.UI，不可访问）
    //   4. WPF TextGuidelineHelper.GuideLinePosition → spike 跳过
    //      （Avalonia 无对应 helper；GuideLineMargin 改由外部直接设置）
    //   5. WPF GetContextMenu override + AddDefaultTextBoxMenuItems → spike 跳过
    //      （Avalonia TextBox 已内置默认 ContextMenu）
    //   6. spike 保留 WrapString 段落换行算法（纯 C# 逻辑，无 WPF 依赖）
    //   7. spike 保留 GuideLineMargin StyledProperty<Thickness>（外部可读写）
    //   8. spike 新增 WrapParagraphAtRuler(int width) 公共方法（替代 ContextMenu MenuItem）
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 TextBox
    //   - GuideLineMargin StyledProperty<Thickness>
    //   - WrapString 段落换行算法保留（私有）
    //   - WrapParagraphAtRuler(int width) 公共方法（spike 替代 ContextMenu MenuItem）
    public class CommitDescriptionTextBox : TextBox
    {
        // 对照 WPF: public static readonly DependencyProperty GuideLineMarginProperty
        //   = DependencyProperty.Register("GuideLineMargin", typeof(Thickness), ...)
        // spike 版：StyledProperty<Thickness>（默认 Thickness(0,0,0,0)）
        public static readonly StyledProperty<Thickness> GuideLineMarginProperty =
            AvaloniaProperty.Register<CommitDescriptionTextBox, Thickness>(nameof(GuideLineMargin));

        // 对照 WPF: public Thickness GuideLineMargin { get; set; }
        public Thickness GuideLineMargin
        {
            get => GetValue(GuideLineMarginProperty);
            set => SetValue(GuideLineMarginProperty, value);
        }

        public CommitDescriptionTextBox()
        {
            // 对照 WPF: WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(
            //   NotificationCenter.Current, "PageGuideLinePositionChanged", RefreshGuideLine)
            // spike 跳过：NotificationCenter 在 WPF 工程，不可访问
            // 对照 WPF: base.Loaded += delegate { RefreshGuideLine(); };
            // spike 跳过：RefreshGuideLine 改为空实现
        }

        // spike 新增：替代 WPF GetContextMenu override 中的 MenuItem "Wrap Paragraph at Ruler" Click
        // 对照 WPF:
        //   int pageGuideLinePosition = ForkPlusSettings.Default.PageGuideLinePosition;
        //   int width = ((pageGuideLinePosition > 0) ? pageGuideLinePosition : 72);
        //   base.Text = WrapString(base.Text, width);
        // spike 版：直接接收 width 参数（调用方传入 PageGuideLinePosition 或 72）
        public void WrapParagraphAtRuler(int width)
        {
            if (width <= 0) width = 72;
            Text = WrapString(Text ?? string.Empty, width);
        }

        // 对照 WPF: private string WrapString(string input, int width)
        // 纯 C# 段落换行算法：按段落（双换行）分割，每段按 width 字符宽度插入换行
        // spike 版完整移植，无 WPF 依赖
        private string WrapString(string input, int width)
        {
            string paragraphSeparator = Environment.NewLine + Environment.NewLine;
            string[] paragraphs = input.Split(
                new string[2] { paragraphSeparator, "\n\n" },
                StringSplitOptions.RemoveEmptyEntries);
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < paragraphs.Length; i++)
            {
                // 对照 WPF: array[i].Replace(Environment.NewLine, " ").Split(Consts.Chars.Space, ...)
                // spike 版：硬编码 new char[] { ' ' } 替代 Consts.Chars.Space
                string[] words = paragraphs[i]
                    .Replace(Environment.NewLine, " ")
                    .Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                {
                    continue;
                }
                if (i > 0)
                {
                    stringBuilder.Append(paragraphSeparator);
                }
                int lineLength = 0;
                foreach (string word in words)
                {
                    if (string.IsNullOrWhiteSpace(word))
                    {
                        continue;
                    }
                    if (lineLength + 1 + word.Length > width)
                    {
                        stringBuilder.Append(Environment.NewLine);
                        stringBuilder.Append(word);
                        lineLength = 1 + word.Length;
                        continue;
                    }
                    if (lineLength > 0)
                    {
                        stringBuilder.Append(" ");
                        lineLength++;
                    }
                    stringBuilder.Append(word);
                    lineLength += word.Length;
                }
            }
            return stringBuilder.ToString();
        }
    }
}
