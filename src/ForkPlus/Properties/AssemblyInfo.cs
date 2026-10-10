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
// 版本号说明（2026-10-09，v5.0.7）：贮藏右键菜单新增「另存为补丁」，并把远端分支
//   删除状态栏的硬编码英文文案国际化。导出用 git stash show -p --binary（二进制改动
//   完整可应用），贮藏含未跟踪文件时加 --include-untracked（git 2.32+），老版本 git
//   自动降级为仅导出已跟踪改动；交互复用工作区「另存为补丁」的选路径 + 进度弹窗 +
//   后台写盘，并记录 RecentPatchDirectory。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.7"；
//   程序集标识与文件版本同为 5.0.7（.0）。
// 版本号说明（2026-10-08，v5.0.6）：修复 Windows 上自动更新"点 Download 闪一下、
//   原地踏步"——AutoUpdateRunner 命令行拼装的 --install-dir 值以 \ 结尾（Windows 的
//   AppContext.BaseDirectory 必然如此），按 Windows 规则吃掉结束引号并把后随的
//   --restart-command 段吞进该值，updater 参数解析失败、在连进度管道之前以退出码 1
//   退出且一条消息都不发；主窗口又把"无消息早退"当用户取消静默复位。修复：拼命令行
//   时把结尾连续反斜杠翻倍；同时 UpdateAvailableWindow 读退出码，非用户取消且非 0
//   时按失败报错（与手动检查窗口的"重置此版本"口径一致）。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.6"；
//   程序集标识与文件版本同为 5.0.6（.0）。
// 版本号说明（2026-10-08，v5.0.5）：让文本差异也能命中对比视图插件——
//   文本文件（如 .json / .dbc）默认仍由内置文本编辑器渲染，但若插件被用户绑定、
//   或以精确扩展名声明认领，则由 FileDiffControl / CommitFileDiffControl 加载两侧
//   字节并转交 PluginDiffViewControl；候选经 DiffViewPluginRegistry.ResolveClaimed
//   解析（用户绑定 > 精确扩展名，不含 "*" 通配兜底，避免 Hex 兜底把文本抢走）。
//   同时把 MaxHexDiffSize 由 50MB 提到 100MB，扩大非图片二进制插件的字节供给范围。
//   AssemblyVersion / AssemblyFileVersion 只接受纯数字（major.minor.build[.revision]）。
//   App.Version 运行时优先读 InformationalVersion → 关于/更新检查/UserAgent 显示 "5.0.5"；
//   程序集标识与文件版本同为 5.0.5（.0）。
[assembly: AssemblyFileVersion("5.0.7")]
[assembly: AssemblyInformationalVersion("5.0.7")]
[assembly: AssemblyProduct("ForkPlus")]
[assembly: AssemblyTitle("ForkPlus")]
[assembly: AssemblyVersion("5.0.7.0")]
