// WPF → Avalonia 迁移期全局 using：
// 1) 全局引入 WpfCompat 命名空间（所有 shim/扩展免 using 即可用）
// 2) 把 WPF 命名但 Avalonia 仍存在/被 shim 替代的类型做全局别名，减少逐文件改动
// Migration note：收尾阶段评估删除本文件，改回标准 using。

global using ForkPlus.UI.WpfCompat;
global using PixelFormats = ForkPlus.UI.WpfCompat.WpfPixelFormats;
global using DataFormats = ForkPlus.UI.WpfCompat.WpfDataFormats;
global using ActualThemeVariant = Avalonia.Styling.ThemeVariant;
global using TextWrapping = Avalonia.Media.TextWrapping;
global using TextTrimming = Avalonia.Media.TextTrimming;
global using TextAlignment = Avalonia.Media.TextAlignment;
