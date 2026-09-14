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
// 版本号说明（2026-09-14，v4.1.1）：升级区域新增"重置此版本"就地修复入口——版本损坏
//   又无新版本时，在"检查更新"窗口点"重置此版本"，两步确认（确认重置当前版本 + 是否
//   重置设置）后按当前版本+平台构造 GitHub 直链，复用 v4.1.0 的自动更新链路（下载 →
//   解压 → 关闭应用 → 备份替换 → 自动重启）就地修复；确认重置设置时 updater 在替换
//   成功后删除 settings.json。上一个正式版 v4.1.0 为自动更新全链路 + 下载进度条随时
//   取消 + 首次启动"更新内容"弹窗。AssemblyVersion / AssemblyFileVersion 只接受纯数字
//   （major.minor.build[.revision]）。App.Version 运行时优先读 InformationalVersion →
//   关于/更新检查/UserAgent 显示 "4.1.1"；程序集标识与文件版本同为 4.1.1（.0）。
[assembly: AssemblyFileVersion("4.1.1")]
[assembly: AssemblyInformationalVersion("4.1.1")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.1.1.0")]
