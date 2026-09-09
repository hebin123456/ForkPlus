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
// 版本号说明（2026-09-09，v4.0.5）：ARM 版配置持久化兜底、内置 CJK 字体、
//   崩溃兜底日志、构建告警清零四项修复。v4.0.4 为愚人节彩蛋版本（404 Not Found，
//   无构建产物），实际修复内容全部包含在本版。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.5"；程序集标识与文件版本同为 4.0.5（.0）。
[assembly: AssemblyFileVersion("4.0.5")]
[assembly: AssemblyInformationalVersion("4.0.5")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.5.0")]
