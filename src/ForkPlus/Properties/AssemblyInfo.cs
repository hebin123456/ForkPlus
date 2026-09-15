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
// 版本号说明（2026-09-15，v4.1.2）：凭据链路系列修复——credential.usehttppath=true
//   （git mm workspace 用户全局配置）下查不到 GCM 存量凭据反复弹窗；git mm 自有询问
//   变体（小写 + URL 不带引号 + 空 userinfo）解析失败；"记住密码"勾选后仍反复询问
//   （并入静默回填语义）；askpass 单词询问会话桥接。另存为补丁内容多时界面卡住（后台
//   执行 + 新增进度弹窗）；FileDiff SideBySide 左右不严格对齐系列（"\ No newline"
//   pragma 行对齐 / CJK 行高差 / 两水平滚动条末端失步）；loading 环形动画转不起来；
//   行号与代码垂直错位（基线对齐）；二进制差异高亮像素单侧显示。上一个正式版
//   v4.1.1 为"重置此版本"就地修复入口。AssemblyVersion / AssemblyFileVersion 只接受
//   纯数字（major.minor.build[.revision]）。App.Version 运行时优先读
//   InformationalVersion → 关于/更新检查/UserAgent 显示 "4.1.2"；程序集标识与文件
//   版本同为 4.1.2（.0）。
[assembly: AssemblyFileVersion("4.1.2")]
[assembly: AssemblyInformationalVersion("4.1.2")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.1.2.0")]
