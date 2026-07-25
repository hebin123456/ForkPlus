// 阶段 5：XmlnsDefinition 将 ForkPlus 程序集中的桥接类型命名空间映射到 Avalonia 默认 XAML 命名空间。
// 这样 XAML 文件无需额外 xmlns 声明即可使用 BitmapImage、Hyperlink、GridView、PasswordBox 等桥接类型。
// Avalonia XAML 编译器会同时搜索 Avalonia 程序集和本程序集中映射到 https://github.com/avaloniaui 的命名空间。
// 仅当类型名在 Avalonia 核心程序集中不存在时才会命中本程序集中的桥接类型，不会产生冲突。
using Avalonia.Metadata;

// BitmapImage 桥接类型（WPF System.Windows.Media.Imaging.BitmapImage 兼容）
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Avalonia.Media.Imaging")]

// Hyperlink 桥接类型（WPF System.Windows.Documents.Hyperlink 兼容）
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Avalonia.Controls.Documents")]

// PasswordBox 桥接类型（Avalonia 11.3 移除了 PasswordBox，本类继承 TextBox 提供兼容）
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Avalonia.Controls")]

// GridViewHeaderRowPresenter / GridViewRowPresenter 桥接类型（WPF 兼容）
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Avalonia.Controls.Primitives")]

// BooleanToVisibilityConverter 桥接类型（WPF System.Windows.Data.BooleanToVisibilityConverter 兼容）
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Avalonia.Media")]

// GridView / GridViewColumn / GridViewColumnHeader 桥接类型（WPF System.Windows.Controls.GridView 兼容）
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "ForkPlus.UI.Compatibility")]

// TabPanel / ResizeGrip / RichTextBox / AdornerDecorator / WindowChrome 桥接类型已在 Avalonia.Controls /
// Avalonia.Controls.Primitives / Avalonia.Media 命名空间下（Avalonia 核心命名空间，XAML 默认即可解析）。
// SystemColors / SystemParameters 桥接类型（WPF System.Windows.SystemColors / SystemParameters 兼容）
// 放在独立命名空间 Avalonia.SystemParams，需显式映射到默认 XAML 命名空间方可被 {x:Static} 解析。
[assembly: XmlnsDefinition("https://github.com/avaloniaui", "Avalonia.SystemParams")]
