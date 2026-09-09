using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

[assembly: ComVisible(false)]
[assembly: InternalsVisibleTo("ForkPlus.Tests")]
[assembly: AssemblyMetadata("SquirrelAwareVersion", "1")]
[assembly: AssemblyCompany("ForkPlus")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCopyright("Copyright © 2018")]
// 版本号说明（2026-09-09，v4.0.3）：键盘导航选中文件 diff 不刷新、界面重构后
//   大量对话框/控件国际化丢失、多平台 Release 资产导致更新下载链接错位——三项修复。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.3"；程序集标识与文件版本同为 4.0.3（.0）。
[assembly: AssemblyFileVersion("4.0.3")]
[assembly: AssemblyInformationalVersion("4.0.3")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.3.0")]
