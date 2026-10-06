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
// 版本号说明（2026-10-06，v5.0.2）：在 v5.0.1 插件管理器之上补齐插件的「发现」入口——
//   1) 新手引导向导新增「对比视图插件」步骤：介绍插件化架构与偏好设置 → 插件页，
//      并指向插件发布页下载更多插件（步骤总数 12 → 13，文案随 8 种界面语言发布）。
//   2) 偏好设置 → 插件页新增超链接，直达 ForkPlus-Plugins 的 Releases 页面下载最新插件。
//   3) 版本号 5.0.2 为 v5.0.1 的补丁迭代（文档/引导补充 + 无破坏性变更）。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.2"；
//   程序集标识与文件版本同为 5.0.2（.0）。
[assembly: AssemblyFileVersion("5.0.2")]
[assembly: AssemblyInformationalVersion("5.0.2")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.0.2.0")]
