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
// 版本号说明（2026-09-10，v4.0.8）：跨平台迁移期 UI/交互修复批次——切主题弹窗外圈配色、
//   TabControl 模板重建竞态（裸 TabControl 接入兜底释放）、交互式变基取消二次确认 owner、
//   切主题偶发卡死（Grid already has a visual parent）、git 2.39+ 凭据协议 v2 参数识别、
//   Windows 原生 Toast 通知恢复（AUMID 注册表 + PowerShell）、窗口位置/大小/最大化
//   状态恢复、窗口边缘 resize 光标、未暂存区选中文件夹可暂存、另存为补丁取消不关弹窗、
//   二分查找完成提示、git mm 命令输出收编到活动管理器、git mm 操作不锁界面、账号弹窗
//   嵌套模态 owner、标签页拖动换位置、显示所有标签/分支/贮藏交互修复。
//   上一版 v4.0.7 为 TextBox 系超宽文本拖选修复；v4.0.6 为崩溃/卡顿专项
//   （native 崩溃转储、UI 冻结看门狗、诊断包导出、reverse P/Invoke 防护等）。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示
//   "4.0.8"；程序集标识与文件版本同为 4.0.8（.0）。
[assembly: AssemblyFileVersion("4.0.8")]
[assembly: AssemblyInformationalVersion("4.0.8")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("4.0.8.0")]
