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
// 版本号说明（2026-09-10，v4.0.7）：TextBox 系（含 PlaceholderTextBox/编辑型 ComboBox）
//   超宽文本无法拖选修复——模板补回 ScrollViewer（PART_ScrollViewer）。上一版
//   v4.0.6 为崩溃/卡顿专项：native 崩溃转储（createdump）、UI 冻结看门狗
//   （freeze-*.log + 冻结转储）、诊断包一键导出、reverse P/Invoke 回调防护
//   （FailFast 死亡区关闭）、文件对话框 PushFrame 等待、native 边界钳制。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.7"；程序集标识与文件版本同为 4.0.7（.0）。
[assembly: AssemblyFileVersion("4.0.7")]
[assembly: AssemblyInformationalVersion("4.0.7")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.7.0")]
