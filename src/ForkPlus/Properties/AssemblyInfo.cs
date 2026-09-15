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
// 版本号说明（2026-09-15，v4.1.3）：FileDiff 行间距收紧——v4.1.2 为修左右行对齐
//   （CJK 行高差）装的行高同步器把含中文文件的全部行槽抬到 CJK 自然行高（根因：
//   全局回退 Noto Sans CJK SC 垂直度量 1.448em，Win DWrite 实测 1.619em，CJK 行
//   自然高超默认行槽），行间距过宽。本版把内嵌 CJK 子集字体度量收紧到 1.25em
//   （hhea/OS/2 win/typo 三处一致），CJK 自然高 16.25px@13px 回到默认行槽以内，
//   行间距恢复 v4.1.2 之前水平；左右行对齐与行号基线对齐保持不变（守卫测试
//   SideBySideRegressionTests 四条防线）。上一个正式版 v4.1.2 为凭据链路系列
//   修复 + FileDiff SideBySide 严格对齐系列。AssemblyVersion /
//   AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。App.Version
//   运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "4.1.3"；
//   程序集标识与文件版本同为 4.1.3（.0）。
[assembly: AssemblyFileVersion("4.1.3")]
[assembly: AssemblyInformationalVersion("4.1.3")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.1.3.0")]
