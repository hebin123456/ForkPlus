using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ForkPlus.Settings;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RepositoryColorsUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RepositoryColorsUserControl.xaml.cs（150 行）：
    //   - 静态 _repositoryBrushes[7] 数组（Transparent + Red/Orange/Yellow/Green/Blue/Violet）
    //   - 静态 GetBrush(RepositoryColor) API（外部调用方根据 RepositoryColor 取对应 SolidColorBrush）
    //   - 构造函数接受 RepositoryManager.Repository，初始化时按 repository.Color 选中对应 RadioButton
    //   - ColorButton_Changed：UpdateRepositoryColor() + HideParentContextMenu(sender)
    //   - UpdateRepositoryColor()：RepositoryManager.Instance.UpdateRepositoryColor + Save +
    //     NotificationCenter.Current.RaiseRepositoryColorChanged
    //   - GetSelectedColorIndex()：按哪个 RadioButton.IsChecked 返回 RepositoryColor 枚举
    //   - HideParentContextMenu(ctrl)：向上遍历 Parent，遇到 ContextMenu 关闭
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF RepositoryManager.Repository → RepositoryColor 初始值（直接接受枚举值）
    //   - WPF RepositoryManager.Instance.UpdateRepositoryColor + Save → onSave 回调（Action<RepositoryColor>）
    //   - WPF NotificationCenter.Current.RaiseRepositoryColorChanged → onColorChanged 回调
    //   - WPF SolidColorBrush.Freeze() → spike 跳过（Avalonia 不需要 Freeze）
    //   - WPF RadioButton.Checked/Unchecked → IsCheckedChanged 事件
    //   - WPF FrameworkElement.Parent 向上遍历 → Avalonia VisualTree GetVisualParent
    //   - WPF ContextMenu.IsOpen = false → Avalonia ContextMenu.IsOpen = false
    public partial class RepositoryColorsUserControl : UserControl
    {
        // ===== 静态调色板（对照 WPF _repositoryBrushes[7]）=====
        // 索引 0 = Transparent（None 占位），1-6 = Red/Orange/Yellow/Green/Blue/Violet
        // 对照 WPF hex 值：#FF3B30 / #FF9502 / #FFCC00 / #64DA38 / #1CADF8 / #CB73E1
        private static readonly ISolidColorBrush[] _repositoryBrushes = new ISolidColorBrush[7]
        {
            Brushes.Transparent,
            new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30)),
            new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x02)),
            new SolidColorBrush(Color.FromRgb(0xFF, 0xCC, 0x00)),
            new SolidColorBrush(Color.FromRgb(0x64, 0xDA, 0x38)),
            new SolidColorBrush(Color.FromRgb(0x1C, 0xAD, 0xF8)),
            new SolidColorBrush(Color.FromRgb(0xCB, 0x73, 0xE1))
        };

        // ===== 私有字段（对照 WPF）=====
        private bool _initialized;
        private readonly Action<RepositoryColor> _onSave;
        private readonly Action _onColorChanged;

        // ===== 构造函数（对照 WPF: RepositoryColorsUserControl(RepositoryManager.Repository repository)）=====
        // spike 版接受 RepositoryColor 初始值 + onSave/onColorChanged 回调
        // （替代 WPF RepositoryManager.Instance.UpdateRepositoryColor + NotificationCenter.Current.RaiseRepositoryColorChanged）
        public RepositoryColorsUserControl() : this(RepositoryColor.None, null, null)
        {
        }

        public RepositoryColorsUserControl(RepositoryColor initialColor,
                                           Action<RepositoryColor> onSave = null,
                                           Action onColorChanged = null)
        {
            InitializeComponent();
            _onSave = onSave;
            _onColorChanged = onColorChanged;
            InitializeColorButtons(initialColor);
            _initialized = true;
        }

        // ===== 静态 GetBrush API（对照 WPF: public static SolidColorBrush GetBrush(RepositoryColor)）=====
        // 外部调用方（如 SidebarUserControl/RepositoryManagerUserControl）根据 RepositoryColor
        // 取对应 SolidColorBrush 渲染仓库前缀色块
        public static ISolidColorBrush GetBrush(RepositoryColor colorIndex)
        {
            if (colorIndex == RepositoryColor.None)
            {
                return null;
            }
            return _repositoryBrushes[(int)colorIndex];
        }

        // ===== ColorButton_Changed（对照 WPF）=====
        // 对照 WPF: private void ColorButton_Changed(object sender, RoutedEventArgs e)
        //   WPF: e.Handled = true; if (_initialized) { if (sender is RadioButton) UpdateRepositoryColor();
        //         HideParentContextMenu(sender); }
        //   Avalonia: RoutedEventArgs.Handled = true; 同样逻辑
        private void ColorButton_Changed(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_initialized)
            {
                if (sender is RadioButton)
                {
                    UpdateRepositoryColor();
                }
                HideParentContextMenu(sender);
            }
        }

        // ===== HideParentContextMenu（对照 WPF static HideParentContextMenu）=====
        // 对照 WPF: for (FrameworkElement fe = ctrl as FrameworkElement; fe != null; fe = fe.Parent as FrameworkElement)
        //            if (fe is ContextMenu cm) { cm.IsOpen = false; break; }
        //   Avalonia 11：IVisual 已移除，改用 Visual + GetVisualParent()；
        //   ContextMenu.IsOpen setter 不可访问，改用 Close()。
        private static void HideParentContextMenu(object ctrl)
        {
            Visual visual = ctrl as Visual;
            while (visual != null)
            {
                if (visual is ContextMenu contextMenu)
                {
                    contextMenu.Close();
                    break;
                }
                visual = visual.GetVisualParent();
            }
        }

        // ===== InitializeColorButtons（对照 WPF）=====
        // 对照 WPF: private void InitializeColorButtons(RepositoryColor colorIndex)
        //   WPF: Color0Button.Background = _repositoryBrushes[1]; ... 按枚举选中对应按钮
        //   Avalonia: 同样逻辑（注意 WPF Color0Button 对应 Red=枚举值1，因为 _repositoryBrushes[0] 是 Transparent 占位）
        private void InitializeColorButtons(RepositoryColor colorIndex)
        {
            Color0Button.Background = _repositoryBrushes[1];
            Color1Button.Background = _repositoryBrushes[2];
            Color2Button.Background = _repositoryBrushes[3];
            Color3Button.Background = _repositoryBrushes[4];
            Color4Button.Background = _repositoryBrushes[5];
            Color5Button.Background = _repositoryBrushes[6];

            switch (colorIndex)
            {
                case RepositoryColor.None:
                    NoColorButton.IsChecked = true;
                    break;
                case RepositoryColor.Red:
                    Color0Button.IsChecked = true;
                    break;
                case RepositoryColor.Orange:
                    Color1Button.IsChecked = true;
                    break;
                case RepositoryColor.Yellow:
                    Color2Button.IsChecked = true;
                    break;
                case RepositoryColor.Green:
                    Color3Button.IsChecked = true;
                    break;
                case RepositoryColor.Blue:
                    Color4Button.IsChecked = true;
                    break;
                case RepositoryColor.Violet:
                    Color5Button.IsChecked = true;
                    break;
            }
        }

        // ===== UpdateRepositoryColor（对照 WPF）=====
        // 对照 WPF: private void UpdateRepositoryColor()
        //   WPF: RepositoryManager.Instance.UpdateRepositoryColor(_repository.Path, GetSelectedColorIndex());
        //         RepositoryManager.Instance.Save();
        //         NotificationCenter.Current.RaiseRepositoryColorChanged(this, _repository);
        //   spike: 调用注入的 _onSave + _onColorChanged 回调
        private void UpdateRepositoryColor()
        {
            RepositoryColor color = GetSelectedColorIndex();
            _onSave?.Invoke(color);
            _onColorChanged?.Invoke();
        }

        // ===== GetSelectedColorIndex（对照 WPF）=====
        // 对照 WPF: private RepositoryColor GetSelectedColorIndex()
        //   WPF: 按 NoColorButton/Color0~5Button.IsChecked 返回对应 RepositoryColor
        //   Avalonia: 同样逻辑（RadioButton.IsChecked 是 bool? 类型，用 GetValueOrDefault() 取默认 false）
        private RepositoryColor GetSelectedColorIndex()
        {
            if (NoColorButton.IsChecked.GetValueOrDefault()) return RepositoryColor.None;
            if (Color0Button.IsChecked.GetValueOrDefault()) return RepositoryColor.Red;
            if (Color1Button.IsChecked.GetValueOrDefault()) return RepositoryColor.Orange;
            if (Color2Button.IsChecked.GetValueOrDefault()) return RepositoryColor.Yellow;
            if (Color3Button.IsChecked.GetValueOrDefault()) return RepositoryColor.Green;
            if (Color4Button.IsChecked.GetValueOrDefault()) return RepositoryColor.Blue;
            if (Color5Button.IsChecked.GetValueOrDefault()) return RepositoryColor.Violet;
            throw new Exception("Cannot reach here");
        }
    }
}
