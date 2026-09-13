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
// 版本号说明（2026-09-13，v4.1.0）：自动更新体系——AutoUpdater 子进程（下载/解压/
//   替换/重启，备份回滚），更新弹窗内联下载进度（3px 细条 + 速度，Footer 取消按钮
//   随时可取消，失败走 Footer 状态区），首次启动新版本弹"更新内容"（随包
//   RELEASE_NOTE.md 解析章节 + LastShownReleaseNotesVersion 记忆），风格全部复用
//   ForkPlusDialogWindow 组件框架。上一版 v4.0.12 为行号边距崩溃/主题错误日志/
//   二进制差异加载等修复。AssemblyVersion / AssemblyFileVersion 只接受纯数字
//   （major.minor.build[.revision]）。App.Version 运行时优先读 InformationalVersion →
//   关于/更新检查/UserAgent 显示 "4.1.0"；程序集标识与文件版本同为 4.1.0（.0）。
[assembly: AssemblyFileVersion("4.1.0")]
[assembly: AssemblyInformationalVersion("4.1.0")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.1.0.0")]
