using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 GitPointView（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/GitPointView.cs（182 行）：
    //   - WPF GitPointView : System.Windows.Controls.Grid
    //   - 3 列 Grid 布局：Icon / Identifier(Sha) / Description
    //   - IconMargin (Thickness) DependencyProperty
    //   - CustomFontStyle bool
    //   - Value (IGitPoint) 属性：每次 set 清空 Children 并按 _value 类型重建
    //     - StashRevision / Revision / RevisionDetails → 头像图标 + Sha + Description
    //     - LocalBranch / RemoteBranch / Tag → 分支/标签图标 + Description
    //   - CreateImage / GetIcon 按 _value.GetType() 取 Theme.*Icon
    //   - Description(IGitPoint) 返回 Message / FriendlyName / ObjectName
    //   - 主题相关：Theme.LabelBrush / Theme.RevisionIcon / Theme.BranchIcon 等
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 System.Windows.Controls.Grid → Avalonia.Controls.Grid
    //   2. DependencyProperty.Register → StyledProperty<T>.Register
    //   3. WPF Grid.Children.Add + SetValue(Grid.ColumnProperty) →
    //      Avalonia Grid.Children.Add + Grid.SetColumn(child, n)
    //   4. spike 简化：根据 task spec，用 TextBlock 显示 gitPoint.FriendlyName
    //      （WPF 版按类型分发复杂图标 + Sha + Description 三段布局，spike 单段文本）
    //   5. spike 跳过 Theme.*Icon 引用（WPF Theme 类未迁移到 Avalonia，Phase 2.x）
    //   6. spike 跳过 CustomFontStyle 分支（仅在 false 时设置 FontSize/Foreground）
    //
    // spike 简化：
    //   - Value 属性 setter 清空 Children + 用 TextBlock 显示 _value.FriendlyName
    //   - 保留 IconMargin StyledProperty（接口契约，spike 不实际绘制图标）
    //   - 保留 CustomFontStyle bool 属性（接口契约，spike 不应用样式）
    public class GitPointView : Grid
    {
        // 对照 WPF: IconMarginProperty (Thickness, 默认 (1,3,7,1))
        public static readonly StyledProperty<Thickness> IconMarginProperty =
            AvaloniaProperty.Register<GitPointView, Thickness>(nameof(IconMargin), new Thickness(1, 3, 7, 1));

        // 对照 WPF: private bool _customFontStyle
        private bool _customFontStyle;

        // 对照 WPF: private IGitPoint _value
        private IGitPoint _value;

        public Thickness IconMargin
        {
            get => GetValue(IconMarginProperty);
            set => SetValue(IconMarginProperty, value);
        }

        // 对照 WPF: public bool CustomFontStyle
        public bool CustomFontStyle
        {
            get => _customFontStyle;
            set => _customFontStyle = value;
        }

        // 对照 WPF: public IGitPoint Value
        //   spike 版简化：仅用 TextBlock 显示 _value.FriendlyName
        public IGitPoint Value
        {
            get => _value;
            set
            {
                _value = value;
                Children.Clear();
                if (_value != null)
                {
                    // spike 简化：用 TextBlock 显示 FriendlyName（task spec 要求）
                    // 对照 WPF: 创建 3 列 + Image + 2 TextBlock 的复杂布局
                    var textBlock = new TextBlock
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        Text = Description(_value),
                        // spike 版用 ToolTip.Tip（Avalonia 11 API）替代 WPF ToolTip
                        // 注意 Avalonia 用 ToolTip.SetTip 静态方法或 ToolTip.Tip 附加属性
                    };
                    if (!_customFontStyle)
                    {
                        textBlock.FontSize = 13.0;
                    }
                    // spike 版：ToolTip 用附加属性设置
                    ToolTip.SetTip(textBlock, Description(_value));
                    Children.Add(textBlock);
                }
            }
        }

        public GitPointView()
        {
            // 对照 WPF: 3 列 ColumnDefinitions (Auto / Auto / Star)
            // spike 版仅 1 列（TextBlock 单段显示），保留 ColumnDefinitions 空（Grid 默认单列）
            // 但为兼容性保留 3 列布局（即使 spike 版只填第一列）
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // 对照 WPF: private static string Description(IGitPoint gitPoint)
        //   返回 Revision.Message / RevisionDetails.Subject / Reference.Name /
        //   SymbolicReference.FriendlyName / gitPoint.ObjectName
        //   spike 版简化：直接用 FriendlyName（IGitPoint 接口属性，所有实现类都有）
        private static string Description(IGitPoint gitPoint)
        {
            // spike 简化：所有类型统一用 FriendlyName
            // 对照 WPF 版按类型分发（Revision.Message / RevisionDetails.Subject 等），
            // 但 IGitPoint.FriendlyName 在大多数实现类中已返回合适的显示文本
            return gitPoint?.FriendlyName ?? gitPoint?.ObjectName ?? string.Empty;
        }
    }
}
