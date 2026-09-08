using System;
using Avalonia;
using Avalonia.Media;
using ForkPlus.Settings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.UI
{
        /// <summary>
        /// 系统主题色读取（Avalonia 版）。
        /// 原 WPF 版走 WinRT UISettings（net10.0 下无投影）；改为
        /// Windows 上读注册表 AppsUseLightTheme + DWM ColorizationColor，
        /// 其他平台返回中性色。Migration note：跟随系统主题变化需平台各自实现。
        /// </summary>
        internal static class SystemThemeHelper
        {
                public static void SubscribeToSystemEvents()
                {
                        // WinRT ColorValuesChanged 事件无 net10.0 等价物：不做实时订阅
                }

                [Null]
                public static Brush GetSystemBrush(global::ForkPlus.UI.Theme.SystemColorType colorType)
                {
                        SolidColorBrush solidColorBrush = new SolidColorBrush(GetColor(ToSystemColor(colorType)));
                        return solidColorBrush;
                }

                private static global::Avalonia.Media.Color GetColor((byte a, byte r, byte g, byte b) color)
                {
                        return global::Avalonia.Media.Color.FromArgb(color.a, color.r, color.g, color.b);
                }

                private static (byte a, byte r, byte g, byte b) ToSystemColor(global::ForkPlus.UI.Theme.SystemColorType colorType)
                {
                        var accent = ReadAccentColor();
                        bool dark = IsSystemDarkBase();
                        double factor = colorType switch
                        {
                                global::ForkPlus.UI.Theme.SystemColorType.Accent => 1.0,
                                global::ForkPlus.UI.Theme.SystemColorType.Accent1 => dark ? 1.35 : 0.75,
                                global::ForkPlus.UI.Theme.SystemColorType.Accent2 => dark ? 1.6 : 0.55,
                                _ => 1.0,
                        };
                        return Scale(accent, factor);
                }

                private static bool IsSystemDarkBase()
                {
                        if (!OperatingSystem.IsWindows()) return ForkPlusSettings.Default.Theme.IsDarkBase();
                        try
                        {
                                var key = Microsoft.Win32.Registry.GetValue(
                                        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                                        "AppsUseLightTheme", 1);
                                return key is int i && i == 0;
                        }
                        catch
                        {
                                return ForkPlusSettings.Default.Theme.IsDarkBase();
                        }
                }

                private static (byte a, byte r, byte g, byte b) ReadAccentColor()
                {
                        if (OperatingSystem.IsWindows())
                        {
                                try
                                {
                                        // DWM 着色色（含透明度位），低 32 位为 0xAABBGGRR
                                        uint colorization = (uint)(Microsoft.Win32.Registry.GetValue(
                                                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM",
                                                "ColorizationColor", 0xFF0078D4) ?? 0xFF0078D4);
                                        return (0xFF, (byte)((colorization >> 16) & 0xFF), (byte)((colorization >> 8) & 0xFF), (byte)(colorization & 0xFF));
                                }
                                catch
                                {
                                }
                        }
                        return (0xFF, 0x00, 0x78, 0xD4); // Windows 默认强调色兜底
                }

                private static (byte a, byte r, byte g, byte b) Scale((byte a, byte r, byte g, byte b) c, double factor)
                {
                        byte S(byte v) => (byte)Math.Clamp(v * factor, 0, 255);
                        return (c.a, S(c.r), S(c.g), S(c.b));
                }
        }
}
