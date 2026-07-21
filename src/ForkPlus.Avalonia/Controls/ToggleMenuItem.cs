using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ToggleMenuItem（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ToggleMenuItem.cs（39 行）：
    //   - WPF ToggleMenuItem : MenuItem
    //   - 构造函数接收 string title + Action clickHandler + bool isChecked + Image icon
    //   - base.Header = PreferencesLocalization.Current(title)
    //   - base.IsChecked = isChecked
    //   - base.Icon = CloneIcon(icon)（深拷贝 Image 控件）
    //   - base.Click += delegate { clickHandler?.Invoke(); }
    //   - CloneIcon(Image)：复制 Source/Width/Height/Margin/Stretch/Alignment/SnapsToDevicePixels
    //
    // Avalonia 版差异（spike 简化策略，task spec：继承 MenuItem + IsChecked）：
    //   1. 基类 MenuItem → Avalonia.Controls.MenuItem（API 一致）
    //   2. WPF base.Header = PreferencesLocalization.Current(title)
    //      → spike 直接 Header = title（spike 不依赖 PreferencesLocalization）
    //   3. WPF base.IsChecked → Avalonia ToggleType + IsChecked
    //      （Avalonia 11 MenuItem.ToggleType = MenuItemToggleType.CheckBox 时支持 IsChecked）
    //   4. WPF Image (System.Windows.Controls.Image) → Avalonia Image（API 一致）
    //   5. WPF icon.Source (ImageSource) → Avalonia icon.Source (IImage)
    //   6. WPF icon.Stretch → Avalonia icon.Stretch（API 一致）
    //   7. WPF icon.SnapsToDevicePixels → Avalonia 无对应属性（spike 跳过）
    //   8. WPF base.Click (RoutedEventHandler) → Avalonia Click 事件
    //
    // spike 简化（task spec 关键 API）：
    //   - 继承 MenuItem + ToggleType=CheckBox + IsChecked
    //   - 构造函数接收 string title + Action clickHandler + bool isChecked + Image icon
    //   - Click 事件转发到注入的 Action
    //   - CloneIcon(Image) 深拷贝
    public class ToggleMenuItem : MenuItem
    {
        // 对照 WPF: public ToggleMenuItem(string title, Action clickHandler, bool isChecked, Image icon = null)
        //   base.Header = PreferencesLocalization.Current(title);
        //   base.IsChecked = isChecked;
        //   base.Icon = CloneIcon(icon);
        //   base.Click += delegate { clickHandler?.Invoke(); };
        public ToggleMenuItem(string title, Action clickHandler, bool isChecked, Image icon = null)
        {
            Header = title;
            // Avalonia 11：MenuItem 通过 ToggleType 启用 IsChecked 行为
            ToggleType = MenuItemToggleType.CheckBox;
            IsChecked = isChecked;
            Icon = CloneIcon(icon);
            Click += (s, e) => clickHandler?.Invoke();
        }

        // 对照 WPF: private static Image CloneIcon(Image icon)
        //   复制 Source/Width/Height/Margin/Stretch/Alignment/SnapsToDevicePixels
        // spike 版：跳过 SnapsToDevicePixels（Avalonia 无对应属性）
        private static Image CloneIcon(Image icon)
        {
            if (icon == null)
            {
                return null;
            }
            return new Image
            {
                Source = icon.Source,
                Width = icon.Width,
                Height = icon.Height,
                Margin = icon.Margin,
                Stretch = icon.Stretch,
                HorizontalAlignment = icon.HorizontalAlignment,
                VerticalAlignment = icon.VerticalAlignment,
            };
        }
    }
}
