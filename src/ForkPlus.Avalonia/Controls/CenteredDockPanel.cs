using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 CenteredDockPanel（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/CenteredDockPanel.cs（116 行）：
    //   - WPF CenteredDockPanel : DockPanel
    //   - _sizes 数组缓存每个子控件的 Measure 结果
    //   - MeasureOverride：遍历 InternalChildren + DockPanel.GetDock
    //     Left/Right → 累加 Width / Top/Bottom → 累加 Height
    //   - ArrangeOverride：前 N-1 个按 Dock 排列（Left/Right/Top/Bottom）
    //     最后一个（LastChildFill=true）水平居中（剪裁到 dock 区域内）
    //     若 desiredSize.Width < _sizes[i].Width（被压缩），左对齐到 num2
    //   - 居中逻辑：num6 = (arrangeSize.Width - desiredSize.Width) / 2.0
    //     num6 = System.Math.Max(num6, num2)（不超出左 dock 区域）
    //     若超出右 dock 区域，左移 num6
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 Panel + 居中布局）：
    //   1. WPF DockPanel → Avalonia Panel（Avalonia 11 无 DockPanel 内置类，
    //      spike 用 Panel + Dock 附加属性兼容）
    //   2. WPF DockPanel.GetDock → Avalonia 无内置 Dock 附加属性
    //      spike 用本地 Dock 附加属性（attached property）
    //   3. WPF InternalChildren → Avalonia Children（API 一致）
    //   4. WPF MeasureOverride / ArrangeOverride → Avalonia MeasureOverride / ArrangeOverride
    //   5. WPF UIElement → Avalonia Control
    //   6. WPF LastChildFill → spike 用 LastChildFill 公共属性（默认 true）
    //   7. spike 简化：保留 Dock 附加属性 + LastChildFill 居中布局逻辑
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 Panel + Dock 附加属性 + LastChildFill 属性
    //   - MeasureOverride / ArrangeOverride 实现居中 dock 布局
    public class CenteredDockPanel : Panel
    {
        // spike 版本地 Dock 枚举（替代 WPF System.Windows.Controls.Dock）
        public enum Dock
        {
            Left,
            Top,
            Right,
            Bottom,
        }

        // spike 版 Dock 附加属性（替代 WPF DockPanel.Dock）
        public static readonly AttachedProperty<Dock> DockProperty =
            AvaloniaProperty.RegisterAttached<CenteredDockPanel, Control, Dock>(
                "Dock", defaultValue: Dock.Left);

        public static Dock GetDock(Control element)
        {
            return element.GetValue(DockProperty);
        }

        public static void SetDock(Control element, Dock value)
        {
            element.SetValue(DockProperty, value);
        }

        // 对照 WPF: DockPanel.LastChildFill（默认 true）
        public static readonly StyledProperty<bool> LastChildFillProperty =
            AvaloniaProperty.Register<CenteredDockPanel, bool>(nameof(LastChildFill), true);

        public bool LastChildFill
        {
            get => GetValue(LastChildFillProperty);
            set => SetValue(LastChildFillProperty, value);
        }

        // 对照 WPF: private Size[] _sizes
        private global::Avalonia.Size[] _sizes;

        // 对照 WPF: protected override Size MeasureOverride(Size constraint)
        protected override global::Avalonia.Size MeasureOverride(global::Avalonia.Size availableSize)
        {
            var children = Children;
            double val = 0.0;
            double val2 = 0.0;
            double num = 0.0;
            double num2 = 0.0;
            if (_sizes == null || _sizes.Length != children.Count)
            {
                _sizes = new global::Avalonia.Size[children.Count];
            }
            for (int i = 0; i < children.Count; i++)
            {
                Control control = children[i];
                if (control != null)
                {
                    var avail = new global::Avalonia.Size(
                        System.Math.Max(0.0, availableSize.Width - num),
                        System.Math.Max(0.0, availableSize.Height - num2));
                    control.Measure(availableSize);
                    _sizes[i] = control.DesiredSize;
                    control.Measure(avail);
                    var desiredSize = control.DesiredSize;
                    switch (GetDock(control))
                    {
                        case Dock.Left:
                        case Dock.Right:
                            val2 = System.Math.Max(val2, num2 + desiredSize.Height);
                            num += desiredSize.Width;
                            break;
                        case Dock.Top:
                        case Dock.Bottom:
                            val = System.Math.Max(val, num + desiredSize.Width);
                            num2 += desiredSize.Height;
                            break;
                    }
                }
            }
            val = System.Math.Max(val, num);
            val2 = System.Math.Max(val2, num2);
            return new global::Avalonia.Size(val, val2);
        }

        // 对照 WPF: protected override Size ArrangeOverride(Size arrangeSize)
        protected override global::Avalonia.Size ArrangeOverride(global::Avalonia.Size finalSize)
        {
            var children = Children;
            int count = children.Count;
            int num = count - (LastChildFill ? 1 : 0);
            double num2 = 0.0;
            double num3 = 0.0;
            double num4 = 0.0;
            double num5 = 0.0;
            for (int i = 0; i < count; i++)
            {
                Control control = children[i];
                if (control == null)
                {
                    continue;
                }
                var desiredSize = control.DesiredSize;
                var finalRect = new global::Avalonia.Rect(
                    num2, num3,
                    System.Math.Max(0.0, finalSize.Width - (num2 + num4)),
                    System.Math.Max(0.0, finalSize.Height - (num3 + num5)));
                if (i < num)
                {
                    switch (GetDock(control))
                    {
                        case Dock.Left:
                            num2 += desiredSize.Width;
                            finalRect = finalRect.WithWidth(desiredSize.Width);
                            break;
                        case Dock.Right:
                            num4 += desiredSize.Width;
                            finalRect = new global::Avalonia.Rect(
                                System.Math.Max(0.0, finalSize.Width - num4),
                                finalRect.Y,
                                desiredSize.Width,
                                finalRect.Height);
                            break;
                        case Dock.Top:
                            num3 += desiredSize.Height;
                            finalRect = finalRect.WithHeight(desiredSize.Height);
                            break;
                        case Dock.Bottom:
                            num5 += desiredSize.Height;
                            finalRect = new global::Avalonia.Rect(
                                finalRect.X,
                                System.Math.Max(0.0, finalSize.Height - num5),
                                finalRect.Width,
                                desiredSize.Height);
                            break;
                    }
                }
                else
                {
                    // 对照 WPF: LastChildFill 居中逻辑
                    double num6 = (finalSize.Width - desiredSize.Width) / 2.0;
                    double num7 = num6 + desiredSize.Width;
                    num6 = System.Math.Max(num6, num2);
                    if (num7 > finalSize.Width - num4)
                    {
                        double num8 = num7 - (finalSize.Width - num4);
                        num6 -= num8;
                    }
                    if (desiredSize.Width < _sizes[i].Width)
                    {
                        num6 = num2;
                    }
                    finalRect = new global::Avalonia.Rect(
                        num6,
                        finalRect.Y,
                        desiredSize.Width,
                        finalRect.Height);
                }
                control.Arrange(finalRect);
            }
            return finalSize;
        }
    }
}
