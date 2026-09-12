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
// 版本号说明（2026-09-12，v4.0.12）：代码编辑器行号边距崩溃修复 + 主题切换错误日志修复——
//   CodeEditorLineNumberMargin.Render 在视觉行失效期（Redraw()/文档变更后、Measure 重建前）
//   直接读 TextView.VisualLines 抛 VisualLinesInvalidException，Avalonia 同步提交路径
//   （WndProc → Compositor.Commit → UpdateCore）把异常上抛成 AppDomain 致命终止
//   （crash-20260912-103134/-103202 双转储）；修复为与 TextView.Render 同款 VisualLinesValid
//   防御，Diff/Merge 行号边距同因加固。TextEditorContextMenu 样式初始化反射 WPF 内部类型
//   恒 null 引用（Avalonia.Controls 无 System.Windows.Documents.TextEditorContextMenu），
//   每次启动/切主题必刷 "Cannot initialize TextEditorContextMenu style" 错误日志；
//   修复为类型不存在时静默跳过 + 存在时幂等注册。上一版 v4.0.11 为二分查找通知条/
//   好/坏互斥/右键菜单快捷键修复；v4.0.10 为 SSH 密钥删除/滚动/统计/首启专项修复。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.12"；程序集标识与文件版本同为 4.0.12（.0）。
[assembly: AssemblyFileVersion("4.0.12")]
[assembly: AssemblyInformationalVersion("4.0.12")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.12.0")]
