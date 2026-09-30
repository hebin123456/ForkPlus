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
// 版本号说明（2026-09-30，v4.3.0）：本版为新手引导 + 删除远端弹窗布局修复——
//   1) 新手引导（Getting Started）：首次启动自动弹出 12 步分步向导（欢迎/工具栏/
//      分支侧栏/提交历史与 Diff/提交变更/git mm 工作流/仓库树图/仓库统计/Reflog
//      时间轴/解决冲突/随心定制/结束），帮助菜单 "Getting Started" 可随时重看；
//      看过或跳过均算完成（OnboardingCompleted 持久化），置位在弹窗前——弹窗期间
//      崩溃/断电不会陷入每次启动重弹；8 语言翻译。
//   2) 删除远端弹窗（RemoveRemoteWindow）图标文字挤在一起：根 Grid 无 RowDefinitions
//      导致基类标题区、内容文本、命令预览全叠第 0 行，且第 0 列宽 Auto 而非 80
//      导致 logo 与远端名同列重叠——按其他弹窗通用布局修复（80 logo 列 + 标题行/
//      内容行分离，"Remote:" 标签 + 远端名），顺带移除与基类 DescriptionTextBlock
//      属性重名的死控件。
//   详见 RELEASE_NOTE v4.3.0。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "4.3.0"；
//   程序集标识与文件版本同为 4.3.0（.0）。
[assembly: AssemblyFileVersion("4.3.0")]
[assembly: AssemblyInformationalVersion("4.3.0")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.3.0.0")]
